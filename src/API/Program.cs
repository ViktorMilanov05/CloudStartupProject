using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.IO.Compression;
using System.Threading.RateLimiting;
using System.Text;
using API.Middleware;
using API.Hubs;
using API.Services;
using Application.Interfaces;
using Application.Mappings;
using Domain.Entities;
using FluentValidation;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// ── ASP.NET Identity ─────────────────────────────────────────────────────────
builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ── JWT Authentication ───────────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwtSettings["Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret)
    || jwtSecret.Length < 32
    || jwtSecret.Contains("CHANGE-THIS", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Configuration error: 'Jwt:Secret' is missing, shorter than 32 characters, or still set to the " +
        "placeholder value. Set a strong, unique secret (e.g. `openssl rand -base64 32`) before starting the application.");
}
var key = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role"
        };

        // Allow JWT token from query string for SignalR
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── AutoMapper ───────────────────────────────────────────────────────────────
builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<MappingProfile>();
});

// ── Application Services ────────────────────────────────────────────────────
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<INotificationPusher, SignalRNotificationPusher>();
builder.Services.AddScoped<JwtService>();

// ── Background Cleanup Services ──────────────────────────────────────────────
builder.Services.AddHostedService<TokenCleanupService>();
builder.Services.AddHostedService<NotificationCleanupService>();
builder.Services.AddHostedService<OrphanedImageCleanupService>();

// ── FluentValidation ─────────────────────────────────────────────────────────
builder.Services.AddValidatorsFromAssemblyContaining<MappingProfile>();

// ── Controllers ──────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Response Compression ─────────────────────────────────────────────────────
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);

// ── Rate Limiting ────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Auth endpoints: 10 requests per minute per IP
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// ── Request Size Limit (for file uploads) ────────────────────────────────────
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 30 * 1024 * 1024; // 30 MB (above 25 MB attachment limit to allow overhead)
});

// ── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ── OpenAPI / Swagger ────────────────────────────────────────────────────────
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
                BearerFormat = "JWT",
                Description = "Enter your JWT token"
            }
        };

        // Apply globally to all operations
        foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations ?? []))
        {
            operation.Value.Security ??= [];
            operation.Value.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        }

        return Task.CompletedTask;
    });
});

var app = builder.Build();

// ── Auto-migrate database on startup (with distributed lock for multi-instance) ─
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var dbCreator = db.GetService<IRelationalDatabaseCreator>();
    if (!await dbCreator.ExistsAsync())
    {
        await dbCreator.CreateAsync();
        Log.Information("Database created.");
    }

    // Use a separate connection with a transaction for the distributed lock
    var connectionString = db.Database.GetConnectionString();
    await using var lockConnection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
    await lockConnection.OpenAsync();
    await using var transaction = lockConnection.BeginTransaction();
    await using var lockCommand = lockConnection.CreateCommand();
    lockCommand.Transaction = transaction;
    lockCommand.CommandText = "EXEC @result = sp_getapplock @Resource = 'DbMigration', @LockMode = 'Exclusive', @LockTimeout = 60000; SELECT @result;";
    var resultParam = lockCommand.CreateParameter();
    resultParam.ParameterName = "@result";
    resultParam.DbType = System.Data.DbType.Int32;
    resultParam.Direction = System.Data.ParameterDirection.Output;
    lockCommand.Parameters.Add(resultParam);
    var lockResult = (int)(await lockCommand.ExecuteScalarAsync())!;
    if (lockResult >= 0)
    {
        Log.Information("Acquired migration lock. Applying pending migrations...");
        db.Database.Migrate();
        Log.Information("Database migrations applied successfully.");
        transaction.Commit();
    }
    else
    {
        Log.Warning("Could not acquire migration lock (result: {Result}). Another instance is handling migration.", lockResult);
        // Wait briefly for the other instance to finish, then continue
        await Task.Delay(TimeSpan.FromSeconds(30));
    }
}

// ── Middleware Pipeline ──────────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

// ── Security Headers ─────────────────────────────────────────────────────────
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-XSS-Protection"] = "0";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; connect-src 'self' wss: ws:; font-src 'self'; object-src 'none'; frame-ancestors 'none';";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "CloudStartupProject API v1");
    });
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseResponseCompression();
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
