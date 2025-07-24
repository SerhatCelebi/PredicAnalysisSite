using System.Diagnostics;
using VurduGololdu.API.Models;
using VurduGololdu.API.Services;

namespace VurduGololdu.API.Middleware
{
    public class PerformanceMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<PerformanceMiddleware> _logger;

        public PerformanceMiddleware(RequestDelegate next, IServiceScopeFactory serviceScopeFactory, ILogger<PerformanceMiddleware> logger)
        {
            _next = next;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                
                // Yavaş istekleri logla (2 saniyeden uzun)
                if (stopwatch.ElapsedMilliseconds > 2000)
                {
                    await LogSlowRequest(context, stopwatch.ElapsedMilliseconds);
                }
                
                // Çok yavaş istekleri kritik olarak logla (5 saniyeden uzun)
                if (stopwatch.ElapsedMilliseconds > 5000)
                {
                    await LogCriticalSlowRequest(context, stopwatch.ElapsedMilliseconds);
                }
            }
        }

        private async Task LogSlowRequest(HttpContext context, long duration)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

                await auditLogService.LogAsync(
                    "SlowRequest",
                    "Performance",
                    requestData: new
                    {
                        path = context.Request.Path.Value,
                        method = context.Request.Method,
                        duration,
                        statusCode = context.Response.StatusCode,
                        userAgent = context.Request.Headers.UserAgent.ToString()
                    },
                    level: AuditLogLevel.Warning
                );

                _logger.LogWarning("Slow request detected: {Method} {Path} took {Duration}ms", 
                    context.Request.Method, context.Request.Path, duration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging slow request");
            }
        }

        private async Task LogCriticalSlowRequest(HttpContext context, long duration)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

                await auditLogService.LogAsync(
                    "CriticalSlowRequest",
                    "Performance",
                    requestData: new
                    {
                        path = context.Request.Path.Value,
                        method = context.Request.Method,
                        duration,
                        statusCode = context.Response.StatusCode,
                        userAgent = context.Request.Headers.UserAgent.ToString(),
                        queryString = context.Request.QueryString.ToString()
                    },
                    level: AuditLogLevel.Critical
                );

                _logger.LogCritical("Critical slow request: {Method} {Path} took {Duration}ms", 
                    context.Request.Method, context.Request.Path, duration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging critical slow request");
            }
        }
    }
} 