using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using VurduGololdu.API.Models;
using VurduGololdu.API.Services;

namespace VurduGololdu.API.Middleware
{
    public class AuditLogMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<AuditLogMiddleware> _logger;

        public AuditLogMiddleware(RequestDelegate next, IServiceScopeFactory serviceScopeFactory, ILogger<AuditLogMiddleware> logger)
        {
            _next = next;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var originalBodyStream = context.Response.Body;

            try
            {
                // Request body'yi oku
                var requestBody = await ReadRequestBodyAsync(context.Request);

                // Response body'yi yakalamak için MemoryStream kullan
                using var responseBodyStream = new MemoryStream();
                context.Response.Body = responseBodyStream;

                // Next middleware'ı çalıştır
                await _next(context);

                stopwatch.Stop();

                // Response body'yi oku
                var responseBody = await ReadResponseBodyAsync(responseBodyStream);

                // Original stream'e response'ı kopyala
                responseBodyStream.Seek(0, SeekOrigin.Begin);
                await responseBodyStream.CopyToAsync(originalBodyStream);

                // Audit log kaydet
                await LogRequestAsync(context, requestBody, responseBody, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Hata durumunda da log kaydet
                await LogRequestAsync(context, null, null, stopwatch.ElapsedMilliseconds, ex);

                throw;
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }

        private async Task<string?> ReadRequestBodyAsync(HttpRequest request)
        {
            try
            {
                if (request.ContentLength == null || request.ContentLength <= 0)
                    return null;

                // Büyük dosya uploadları için body'yi loglama
                if (request.ContentLength > 1024 * 1024) // 1MB'dan büyükse
                    return $"[Large content: {request.ContentLength} bytes]";

                request.EnableBuffering();
                var buffer = new byte[Convert.ToInt32(request.ContentLength)];
#pragma warning disable CA2022 // Avoid inexact read overload
                await request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), CancellationToken.None);
#pragma warning restore CA2022
                request.Body.Seek(0, SeekOrigin.Begin);

                var requestBody = Encoding.UTF8.GetString(buffer);

                // Hassas bilgileri filtrele (şifre, token vs.)
                return FilterSensitiveData(requestBody);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Request body okunamadı");
                return "[Request body okunamadı]";
            }
        }

        private async Task<string?> ReadResponseBodyAsync(MemoryStream responseBodyStream)
        {
            try
            {
                if (responseBodyStream.Length == 0)
                    return null;

                // Büyük response'lar için özet
                if (responseBodyStream.Length > 1024 * 1024) // 1MB'dan büyükse
                    return $"[Large response: {responseBodyStream.Length} bytes]";

                responseBodyStream.Seek(0, SeekOrigin.Begin);
                var responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();

                return FilterSensitiveData(responseBody);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Response body okunamadı");
                return "[Response body okunamadı]";
            }
        }

        private string FilterSensitiveData(string data)
        {
            if (string.IsNullOrEmpty(data))
                return data;

            try
            {
                // JSON ise parse et ve hassas alanları filtrele
                var jsonDoc = JsonDocument.Parse(data);
                return FilterJsonSensitiveData(jsonDoc.RootElement).ToString();
            }
            catch
            {
                // JSON değilse hassas kelimeler için temel filtreleme
                var filtered = data;
                var sensitiveFields = new[] { "password", "token", "secret", "key", "authorization" };

                foreach (var field in sensitiveFields)
                {
                    // Case insensitive replacement with pattern
                    var pattern = $@"""{field}"":\s*""[^""]*""";
                    filtered = System.Text.RegularExpressions.Regex.Replace(
                        filtered, pattern, $@"""{field}"":""[FILTERED]""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }

                return filtered;
            }
        }

        private JsonElement FilterJsonSensitiveData(JsonElement element)
        {
            var sensitiveFields = new[] { "password", "token", "secret", "key", "authorization", "refreshtoken" };

            if (element.ValueKind == JsonValueKind.Object)
            {
                var filteredObject = new Dictionary<string, object>();

                foreach (var property in element.EnumerateObject())
                {
                    if (sensitiveFields.Any(sf => property.Name.ToLower().Contains(sf.ToLower())))
                    {
                        filteredObject[property.Name] = "[FILTERED]";
                    }
                    else
                    {
                        filteredObject[property.Name] = FilterJsonSensitiveData(property.Value);
                    }
                }

                return JsonSerializer.SerializeToElement(filteredObject);
            }

            return element;
        }

        private async Task LogRequestAsync(HttpContext context, string? requestBody, string? responseBody,
                                         long duration, Exception? exception = null)
        {
            try
            {
                // API endpoint'leri değilse loglama
                if (!context.Request.Path.StartsWithSegments("/api"))
                    return;

                // Health check ve swagger endpoint'lerini loglama
                var excludePaths = new[] { "/api/health", "/swagger", "/favicon.ico" };
                if (excludePaths.Any(path => context.Request.Path.StartsWithSegments(path)))
                    return;

                using var scope = _serviceScopeFactory.CreateScope();
                var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

                var action = GetActionName(context);
                var entity = GetEntityName(context);
                var level = exception != null ? AuditLogLevel.Error : AuditLogLevel.Info;
                var errorMessage = exception?.Message;

                await auditLogService.LogAsync(
                    action: action,
                    entity: entity,
                    requestData: requestBody,
                    responseData: responseBody,
                    level: level,
                    errorMessage: errorMessage
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit log kaydedilemedi");
            }
        }

        private string GetActionName(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower();
            var method = context.Request.Method.ToUpper();

            if (path == null) return "Unknown";

            // Path'ten action çıkar
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length >= 3) // /api/controller/action
            {
                var controller = segments[1]; // controller
                var action = segments[2]; // action
                return $"{method}_{controller}_{action}";
            }
            else if (segments.Length >= 2) // /api/controller
            {
                var controller = segments[1];
                return $"{method}_{controller}";
            }

            return $"{method}_{path.Replace("/", "_")}";
        }

        private string GetEntityName(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower();

            if (path == null) return "Unknown";

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length >= 2)
            {
                var controller = segments[1];

                // Controller isimlerini entity isimlerine çevir
                return controller switch
                {
                    "auth" => "User",
                    "predictions" => "Prediction",
                    "comments" => "Comment",
                    "paymentnotifications" => "PaymentNotification",
                    "contact" => "ContactMessage",
                    "admin" => "Admin",
                    "auditlog" => "AuditLog",
                    _ => controller
                };
            }

            return "Unknown";
        }

        private int? GetCurrentUserId(HttpContext context)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : null;
        }
    }
}