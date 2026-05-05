using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, title) = exception switch
        {
            ArgumentException => (HttpStatusCode.BadRequest, "Bad Request"),
            InvalidOperationException => (HttpStatusCode.BadRequest, "Bad Request"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Not Found"),
            FileNotFoundException => (HttpStatusCode.NotFound, "Not Found"),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
            _ => (HttpStatusCode.InternalServerError, "Internal Server Error")
        };

        context.Response.StatusCode = (int)statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = statusCode == HttpStatusCode.InternalServerError
                ? "An unexpected error occurred. Please try again later."
                : exception.Message,
            Instance = context.Request.Path
        };

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
