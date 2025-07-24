using System.Collections.Concurrent;
using VurduGololdu.API.Models;
using VurduGololdu.API.Services;

namespace VurduGololdu.API.Services
{
    public interface ISecurityService
    {
        Task<bool> IsIpBlockedAsync(string ipAddress);
        Task<bool> CheckRateLimitAsync(string ipAddress, string action);
        Task LogSuspiciousActivityAsync(string ipAddress, string activity, int? userId = null);
        Task<bool> DetectBruteForceAsync(string ipAddress, string email);
        Task BlockIpTemporarilyAsync(string ipAddress, TimeSpan duration, string reason);
    }

    public class SecurityService : ISecurityService
    {
        private readonly IAuditLogService _auditLogService;
        private readonly ILogger<SecurityService> _logger;

        // Memory cache for rate limiting (production'da Redis kullanılabilir)
        private static readonly ConcurrentDictionary<string, List<DateTime>> _rateLimitCache = new();
        private static readonly ConcurrentDictionary<string, DateTime> _blockedIps = new();
        private static readonly ConcurrentDictionary<string, int> _failedAttempts = new();

        public SecurityService(IAuditLogService auditLogService, ILogger<SecurityService> logger)
        {
            _auditLogService = auditLogService;
            _logger = logger;
        }

        public async Task<bool> IsIpBlockedAsync(string ipAddress)
        {
            if (_blockedIps.TryGetValue(ipAddress, out var blockedUntil))
            {
                if (DateTime.UtcNow < blockedUntil)
                {
                    await _auditLogService.LogAsync(
                        "BlockedIpAccess",
                        "Security",
                        requestData: new { ipAddress, blockedUntil },
                        level: AuditLogLevel.Warning
                    );
                    return true;
                }
                else
                {
                    // Süre dolmuş, IP'yi kaldır
                    _blockedIps.TryRemove(ipAddress, out _);
                    _failedAttempts.TryRemove(ipAddress, out _);
                }
            }
            return false;
        }

        public async Task<bool> CheckRateLimitAsync(string ipAddress, string action)
        {
            // Dummy await to ensure the method remains truly asynchronous and to silence CS1998 warnings
            await Task.Yield();

            var key = $"{ipAddress}:{action}";
            var now = DateTime.UtcNow;

            // Son 1 dakikadaki istekleri al
            var requests = _rateLimitCache.GetOrAdd(key, _ => new List<DateTime>());

            bool exceeded = false;
            int currentCount = 0;
            int limit;

            lock (requests)
            {
                // 1 dakikadan eski istekleri temizle
                requests.RemoveAll(r => r < now.AddMinutes(-1));

                limit = GetRateLimitForAction(action);
                currentCount = requests.Count;

                if (currentCount >= limit)
                {
                    exceeded = true;
                }
                else
                {
                    requests.Add(now);
                }
            }

            if (exceeded)
            {
                await LogSuspiciousActivityAsync(ipAddress, $"RateLimitExceeded:{action}");

                if (currentCount > limit * 2)
                {
                    await BlockIpTemporarilyAsync(ipAddress, TimeSpan.FromMinutes(15), "Rate limit exceeded");
                }

                return false;
            }

            return true;
        }

        public async Task LogSuspiciousActivityAsync(string ipAddress, string activity, int? userId = null)
        {
            await _auditLogService.LogAsync(
                "SuspiciousActivity",
                "Security",
                entityId: userId,
                requestData: new { ipAddress, activity, timestamp = DateTime.UtcNow },
                level: AuditLogLevel.Warning
            );

            _logger.LogWarning("Suspicious activity detected: {Activity} from IP: {IpAddress}, User: {UserId}",
                activity, ipAddress, userId);
        }

        public async Task<bool> DetectBruteForceAsync(string ipAddress, string email)
        {
            var key = $"{ipAddress}:{email}";
            var attempts = _failedAttempts.GetOrAdd(key, 0);

            _failedAttempts[key] = attempts + 1;

            if (attempts >= 5) // 5 başarısız deneme
            {
                await LogSuspiciousActivityAsync(ipAddress, $"BruteForceDetected:{email}");
                await BlockIpTemporarilyAsync(ipAddress, TimeSpan.FromMinutes(30), "Brute force attack detected");
                return true;
            }

            if (attempts >= 3) // 3 başarısız deneme
            {
                await LogSuspiciousActivityAsync(ipAddress, $"MultipleFailedLogins:{email}");
            }

            return false;
        }

        public async Task BlockIpTemporarilyAsync(string ipAddress, TimeSpan duration, string reason)
        {
            var blockedUntil = DateTime.UtcNow.Add(duration);
            _blockedIps[ipAddress] = blockedUntil;

            await _auditLogService.LogAsync(
                "IpBlocked",
                "Security",
                requestData: new { ipAddress, duration = duration.TotalMinutes, reason, blockedUntil },
                level: AuditLogLevel.Critical
            );

            _logger.LogCritical("IP {IpAddress} blocked temporarily until {BlockedUntil}. Reason: {Reason}",
                ipAddress, blockedUntil, reason);
        }

        private int GetRateLimitForAction(string action)
        {
            return action.ToLower() switch
            {
                "login" => 10,          // 1 dakikada max 10 login denemesi
                "register" => 5,        // 1 dakikada max 5 kayıt
                "forgot-password" => 3, // 1 dakikada max 3 şifre sıfırlama
                "create-prediction" => 20, // 1 dakikada max 20 tahmin
                "create-comment" => 30, // 1 dakikada max 30 yorum
                "upload-file" => 10,    // 1 dakikada max 10 dosya
                _ => 300                // Genel işlemler için 300 istek/dk
            };
        }

        // Background service ile periyodik temizlik
        public void CleanupExpiredEntries()
        {
            var now = DateTime.UtcNow;

            // Süresi dolmuş rate limit kayıtlarını temizle
            foreach (var kvp in _rateLimitCache.ToList())
            {
                var requests = kvp.Value;
                lock (requests)
                {
                    requests.RemoveAll(r => r < now.AddMinutes(-5));
                    if (!requests.Any())
                    {
                        _rateLimitCache.TryRemove(kvp.Key, out _);
                    }
                }
            }

            // Süresi dolmuş IP blokları temizle
            foreach (var kvp in _blockedIps.ToList())
            {
                if (kvp.Value < now)
                {
                    _blockedIps.TryRemove(kvp.Key, out _);
                    _failedAttempts.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}