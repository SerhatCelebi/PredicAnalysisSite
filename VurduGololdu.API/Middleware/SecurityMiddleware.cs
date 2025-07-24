using VurduGololdu.API.Services;
using System.Security.Claims;
using VurduGololdu.API.Models;

namespace VurduGololdu.API.Middleware
{
    public class SecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<SecurityMiddleware> _logger;

        public SecurityMiddleware(RequestDelegate next, IServiceScopeFactory serviceScopeFactory, ILogger<SecurityMiddleware> logger)
        {
            _next = next;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var securityService = scope.ServiceProvider.GetRequiredService<ISecurityService>();

            var ipAddress = GetClientIpAddress(context);

            // 1. IP engelli mi kontrol et
            if (await securityService.IsIpBlockedAsync(ipAddress))
            {
                context.Response.StatusCode = 429; // Too Many Requests
                await context.Response.WriteAsync("IP temporarily blocked due to suspicious activity");
                return;
            }

            // 2. Rate limiting kontrol et
            var action = GetActionFromPath(context.Request.Path, context.Request.Method);
            if (!await securityService.CheckRateLimitAsync(ipAddress, action))
            {
                context.Response.StatusCode = 429; // Too Many Requests
                await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
                return;
            }

            // 3. SuperAdmin özel güvenlik kontrolü (sadece kritik işlemler için log)
            await CheckSuperAdminSecurity(context, securityService, ipAddress);

            // 4. Şüpheli aktivite kontrolü
            await CheckForSuspiciousActivity(context, securityService, ipAddress);

            await _next(context);
        }

        private async Task CheckSuperAdminSecurity(HttpContext context, ISecurityService securityService, string ipAddress)
        {
            var userRole = context.User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = GetCurrentUserId(context);

            if (userRole == "SuperAdmin" && userId.HasValue)
            {
                var pathValue = context.Request.Path.Value?.ToLower();

                // Sadece kritik SuperAdmin işlemlerini logla
                if (pathValue != null && (
                    pathValue.Contains("grant-admin") ||
                    pathValue.Contains("revoke-admin") ||
                    pathValue.Contains("change-role-super")))
                {
                    var action = pathValue.Contains("grant-admin") ? "SuperAdmin: Admin yetkisi verme" :
                                pathValue.Contains("revoke-admin") ? "SuperAdmin: Admin yetkisi alma" :
                                "SuperAdmin: Rol değiştirme";

                    await securityService.LogSuspiciousActivityAsync(ipAddress, $"SuperAdmin kritik işlem: {action}", userId);

                    _logger.LogWarning($"SuperAdmin kritik işlem - User: {userId}, IP: {ipAddress}, Action: {action}");
                }

                // Normal SuperAdmin erişimlerini loglama (gereksiz yük oluşturuyor)
                // _logger.LogInformation($"SuperAdmin erişimi - User: {userId}, IP: {ipAddress}, Path: {pathValue}");
            }
        }

        private string GetClientIpAddress(HttpContext context)
        {
            // X-Forwarded-For header'ını kontrol et
            var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                return xForwardedFor.Split(',')[0].Trim();
            }

            // X-Real-IP header'ını kontrol et
            var xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xRealIp))
            {
                return xRealIp;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        private string GetActionFromPath(PathString path, string method)
        {
            var pathValue = path.Value?.ToLower();
            var methodLower = method.ToLower();

            if (pathValue == null) return "unknown";

            if (pathValue.Contains("/auth/login")) return "login";
            if (pathValue.Contains("/auth/register")) return "register";
            if (pathValue.Contains("/auth/forgot-password")) return "forgot-password";
            // POST /api/predictions
            if (pathValue.StartsWith("/api/predictions") && methodLower == "post") return "create-prediction";
            if (pathValue.StartsWith("/api/comments") && methodLower == "post") return "create-comment";
            if (pathValue.Contains("upload")) return "upload-file";

            // SuperAdmin özel işlemleri
            if (pathValue.Contains("grant-admin")) return "grant-admin";
            if (pathValue.Contains("revoke-admin")) return "revoke-admin";
            if (pathValue.Contains("change-role-super")) return "change-role-super";

            return "general";
        }

        private async Task CheckForSuspiciousActivity(HttpContext context, ISecurityService securityService, string ipAddress)
        {
            var userAgent = context.Request.Headers.UserAgent.ToString();
            var userId = GetCurrentUserId(context);

            // Bot veya otomatik araç kontrolü
            if (IsBot(userAgent))
            {
                await securityService.LogSuspiciousActivityAsync(ipAddress, "Bot/Automated tool detected", userId);
            }

            // SQL Injection denemeleri
            if (ContainsSqlInjectionAttempt(context.Request.QueryString.Value))
            {
                await securityService.LogSuspiciousActivityAsync(ipAddress, "SQL injection attempt", userId);
            }

            // XSS denemeleri
            if (ContainsXssAttempt(context.Request.QueryString.Value))
            {
                await securityService.LogSuspiciousActivityAsync(ipAddress, "XSS attempt", userId);
            }
        }

        private int? GetCurrentUserId(HttpContext context)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : null;
        }

        private bool IsBot(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return true;

            var botIndicators = new[] { "bot", "crawler", "spider", "scraper", "curl", "wget", "python", "java" };
            return botIndicators.Any(indicator => userAgent.ToLower().Contains(indicator));
        }

        private bool ContainsSqlInjectionAttempt(string? input)
        {
            if (string.IsNullOrEmpty(input)) return false;

            var sqlPatterns = new[]
            {
                "union select", "drop table", "insert into", "delete from",
                "update set", "exec(", "execute(", "sp_", "xp_",
                "'; --", "' or '1'='1", "' or 1=1", "admin'--"
            };

            return sqlPatterns.Any(pattern => input.ToLower().Contains(pattern));
        }

        private bool ContainsXssAttempt(string? input)
        {
            if (string.IsNullOrEmpty(input)) return false;

            var xssPatterns = new[]
            {
                "<script", "javascript:", "onload=", "onerror=",
                "onclick=", "onmouseover=", "alert(", "document.cookie"
            };

            return xssPatterns.Any(pattern => input.ToLower().Contains(pattern));
        }
    }
}