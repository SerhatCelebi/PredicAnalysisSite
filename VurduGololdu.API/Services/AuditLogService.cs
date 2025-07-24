using System.Security.Claims;
using System.Text.Json;
using VurduGololdu.API.Data;
using VurduGololdu.API.Models;
using VurduGololdu.API.Helpers;

namespace VurduGololdu.API.Services
{
    public interface IAuditLogService
    {
        Task LogAsync(string action, string entity, int? entityId = null, object? requestData = null,
                     object? responseData = null, AuditLogLevel level = AuditLogLevel.Info,
                     string? errorMessage = null);

        Task LogUserActionAsync(int userId, string action, string entity, int? entityId = null,
                               object? requestData = null, AuditLogLevel level = AuditLogLevel.Info);

        Task LogSystemActionAsync(string action, string entity, int? entityId = null,
                                 object? requestData = null, AuditLogLevel level = AuditLogLevel.Info);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string action, string entity, int? entityId = null,
                                  object? requestData = null, object? responseData = null,
                                  AuditLogLevel level = AuditLogLevel.Info, string? errorMessage = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext == null) return;

                var userId = GetCurrentUserId(httpContext);
                var user = userId.HasValue ? await _context.Users.FindAsync(userId.Value) : null;

                var auditLog = new AuditLog
                {
                    Action = action,
                    Entity = entity,
                    EntityId = entityId,
                    UserId = userId,
                    UserEmail = user?.Email,
                    UserName = user != null ? $"{user.FirstName} {user.LastName}" : "Anonymous",
                    IpAddress = GetClientIpAddress(httpContext),
                    UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
                    Endpoint = $"{httpContext.Request.Path}{httpContext.Request.QueryString}",
                    HttpMethod = httpContext.Request.Method,
                    RequestData = requestData != null ? JsonSerializer.Serialize(requestData) : null,
                    ResponseData = responseData != null ? JsonSerializer.Serialize(responseData) : null,
                    StatusCode = httpContext.Response.StatusCode,
                    Level = level,
                    ErrorMessage = errorMessage,
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log hatası sistem loglarına yazılabilir, ancak audit log hatası için tekrar audit log yazmaya çalışmayalım
                DebugConsole.Log($"Audit Log Error: {ex.Message}");
            }
        }

        public async Task LogUserActionAsync(int userId, string action, string entity, int? entityId = null,
                                           object? requestData = null, AuditLogLevel level = AuditLogLevel.Info)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var user = await _context.Users.FindAsync(userId);

                var auditLog = new AuditLog
                {
                    Action = action,
                    Entity = entity,
                    EntityId = entityId,
                    UserId = userId,
                    UserEmail = user?.Email,
                    UserName = user != null ? $"{user.FirstName} {user.LastName}" : "Unknown User",
                    IpAddress = httpContext != null ? GetClientIpAddress(httpContext) : "System",
                    UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
                    Endpoint = httpContext != null ? $"{httpContext.Request.Path}{httpContext.Request.QueryString}" : "System Action",
                    HttpMethod = httpContext?.Request.Method ?? "SYSTEM",
                    RequestData = requestData != null ? JsonSerializer.Serialize(requestData) : null,
                    StatusCode = httpContext?.Response.StatusCode ?? 200,
                    Level = level,
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"Audit Log Error: {ex.Message}");
            }
        }

        public async Task LogSystemActionAsync(string action, string entity, int? entityId = null,
                                             object? requestData = null, AuditLogLevel level = AuditLogLevel.Info)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    Action = action,
                    Entity = entity,
                    EntityId = entityId,
                    IpAddress = "System",
                    Endpoint = "System Action",
                    HttpMethod = "SYSTEM",
                    RequestData = requestData != null ? JsonSerializer.Serialize(requestData) : null,
                    StatusCode = 200,
                    Level = level,
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"Audit Log Error: {ex.Message}");
            }
        }

        private int? GetCurrentUserId(HttpContext httpContext)
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : null;
        }

        private string GetClientIpAddress(HttpContext httpContext)
        {
            // X-Forwarded-For header'ını kontrol et (proxy/load balancer kullanılıyorsa)
            var xForwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                return xForwardedFor.Split(',')[0].Trim();
            }

            // X-Real-IP header'ını kontrol et
            var xRealIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xRealIp))
            {
                return xRealIp;
            }

            // Remote IP Address'i al
            return httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }
    }
}