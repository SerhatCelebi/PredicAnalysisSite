using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace VurduGololdu.API.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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
            var traceId = context.TraceIdentifier;
            _logger.LogError(ex, "Unhandled exception occurred. TraceId: {TraceId}", traceId);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";

            var responseObj = new
            {
                success = false,
                message = "Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.",
                traceId
            };

            var json = JsonSerializer.Serialize(responseObj);
            await context.Response.WriteAsync(json);
        }
    }
}