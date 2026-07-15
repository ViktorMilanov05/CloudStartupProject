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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = exception switch
        {
            ArgumentException => (HttpStatusCode.BadRequest, "Bad Request"),
            InvalidOperationException => (HttpStatusCode.BadRequest, "Bad Request"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Not Found"),
            FileNotFoundException => (HttpStatusCode.NotFound, "Not Found"),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
            _ => (HttpStatusCode.InternalServerError, "Internal Server Error")
        };

        var traceId = context.TraceIdentifier;

        // Unexpected (500) errors are logged at Error with the full exception so support
        // can correlate the client-facing traceId to the exact server-side failure.
        // Expected (4xx) errors are logged at Warning to avoid noise but still traceable.
        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception,
                "Unhandled exception. TraceId: {TraceId}, Method: {Method}, Path: {Path}",
                traceId, context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning(
                "Request failed with {StatusCode} ({Title}): {Message}. TraceId: {TraceId}, Method: {Method}, Path: {Path}",
                (int)statusCode, title, exception.Message, traceId, context.Request.Method, context.Request.Path);
        }

        // If the response has already started, we cannot safely rewrite it.
        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            // Never leak internal exception detail on 500s; expected errors carry a safe message.
            Detail = statusCode == HttpStatusCode.InternalServerError
                ? "An unexpected error occurred. Please reference the traceId when contacting support."
                : exception.Message,
            Instance = context.Request.Path
        };
        problemDetails.Extensions["traceId"] = traceId;

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
