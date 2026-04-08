using System.Diagnostics;
using System.Net;
using System.Text.Json;
using OpenBanking.StableCoin.API.Models;
using OpenBanking.StableCoin.Domain.Exceptions;

namespace OpenBanking.StableCoin.API.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var method = context.Request.Method;
        var path = context.Request.Path;
        var customerId = context.User.FindFirst("customer_id")?.Value ?? "anonymous";

        var (statusCode, errorCode, title, detail) = ex switch
        {
            DomainException de => (HttpStatusCode.BadRequest, de.ErrorCode, "Business Rule Violation", de.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "UNAUTHORIZED", "Unauthorized", "Authentication is required."),
            OperationCanceledException => (HttpStatusCode.ServiceUnavailable, "REQUEST_CANCELLED", "Request Cancelled", "The request was cancelled."),
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "Internal Server Error",
                  "An unexpected error occurred. Please try again later.")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(ex,
                "Unhandled {ExceptionType} on {Method} {Path} for CustomerId={CustomerId}. TraceId={TraceId}",
                ex.GetType().Name, method, path, customerId, traceId);
        else
            _logger.LogWarning(ex,
                "Business exception {ErrorCode} ({ExceptionType}) on {Method} {Path} for CustomerId={CustomerId}. TraceId={TraceId}",
                errorCode, ex.GetType().Name, method, path, customerId, traceId);

        var response = new ApiErrorResponse
        {
            Type = $"https://api.openbanking.com/errors/{errorCode.ToLowerInvariant().Replace('_', '-')}",
            Title = title,
            Status = (int)statusCode,
            Detail = detail,
            ErrorCode = errorCode,
            TraceId = traceId
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers["X-Trace-Id"] = traceId;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOpts));
    }
}
