using VurduGololdu.API.Data;
using VurduGololdu.API.Services;
using Microsoft.EntityFrameworkCore;

namespace VurduGololdu.API.Services
{
    public class CleanupBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<CleanupBackgroundService> _logger;

        public CleanupBackgroundService(IServiceScopeFactory serviceScopeFactory, ILogger<CleanupBackgroundService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformCleanupTasks();

                    // Her 1 saatte bir çalışır
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cleanup background service error");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }

        private async Task PerformCleanupTasks()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var securityService = scope.ServiceProvider.GetRequiredService<ISecurityService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            _logger.LogInformation("Starting cleanup tasks...");

            // 1. Eski audit logları temizle (90 günden eski)
            await CleanupOldAuditLogs(context);

            // 2. VIP expiry notifications ve süresi dolmuş üyelikleri güncelle
            await CheckVipExpiryAndNotify(context, notificationService);

            // 3. Security cache temizliği
            if (securityService is SecurityService security)
            {
                security.CleanupExpiredEntries();
            }

            // 4. Süresi dolmuş token'ları temizle
            await CleanupExpiredTokens(context);

            // 5. Process pending notifications
            // await notificationService.ProcessPendingNotificationsAsync();

            // 6. Retry failed notifications
            // await notificationService.RetryFailedNotificationsAsync();

            // 7. Cleanup old notification logs (180 günden eski)
            await CleanupOldNotificationLogs(context);

            _logger.LogInformation("Cleanup tasks completed");
        }

        private async Task CleanupOldAuditLogs(ApplicationDbContext context)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-90);
            var oldLogsCount = await context.AuditLogs
                .Where(x => x.CreatedAt < cutoffDate)
                .CountAsync();

            if (oldLogsCount > 0)
            {
                await context.AuditLogs
                    .Where(x => x.CreatedAt < cutoffDate)
                    .ExecuteDeleteAsync();

                _logger.LogInformation("Cleaned up {Count} old audit logs", oldLogsCount);
            }
        }

        private async Task CheckVipExpiryAndNotify(ApplicationDbContext context, INotificationService notificationService)
        {
            // VIP üyeliği 3 gün içinde sona erecek kullanıcıları bul
            var soonToExpireDate = DateTime.UtcNow.AddDays(3);
            var soonToExpireUsers = await context.Users
                .Where(u => u.VipExpiryDate.HasValue &&
                           u.VipExpiryDate <= soonToExpireDate &&
                           u.VipExpiryDate > DateTime.UtcNow &&
                           u.NotifyOnVipExpiry &&
                           u.IsActive)
                .ToListAsync();

            // VIP expiry notifications gönder
            foreach (var user in soonToExpireUsers)
            {
                await notificationService.SendVipExpiryNotificationAsync(user.Id);
            }

            if (soonToExpireUsers.Any())
            {
                _logger.LogInformation("Sent VIP expiry notifications to {Count} users", soonToExpireUsers.Count);
            }

            // Süresi dolmuş VIP üyelikleri güncelle
            var expiredVipUsers = await context.Users
                .Where(u => u.VipExpiryDate.HasValue && u.VipExpiryDate <= DateTime.UtcNow)
                .ToListAsync();

            foreach (var user in expiredVipUsers)
            {
                user.VipExpiryDate = null;
                user.UpdatedAt = DateTime.UtcNow;
            }

            if (expiredVipUsers.Any())
            {
                await context.SaveChangesAsync();
                _logger.LogInformation("Updated {Count} expired VIP memberships", expiredVipUsers.Count);
            }
        }

        private async Task CleanupExpiredTokens(ApplicationDbContext context)
        {
            var expiredTokenUsers = await context.Users
                .Where(u => (u.RefreshTokenExpiry.HasValue && u.RefreshTokenExpiry <= DateTime.UtcNow) ||
                           (u.EmailVerificationTokenExpiry.HasValue && u.EmailVerificationTokenExpiry <= DateTime.UtcNow) ||
                           (u.PasswordResetTokenExpiry.HasValue && u.PasswordResetTokenExpiry <= DateTime.UtcNow))
                .ToListAsync();

            foreach (var user in expiredTokenUsers)
            {
                if (user.RefreshTokenExpiry.HasValue && user.RefreshTokenExpiry <= DateTime.UtcNow)
                {
                    user.RefreshToken = null;
                    user.RefreshTokenExpiry = null;
                }

                if (user.EmailVerificationTokenExpiry.HasValue && user.EmailVerificationTokenExpiry <= DateTime.UtcNow)
                {
                    user.EmailVerificationToken = null;
                    user.EmailVerificationTokenExpiry = null;
                }

                if (user.PasswordResetTokenExpiry.HasValue && user.PasswordResetTokenExpiry <= DateTime.UtcNow)
                {
                    user.PasswordResetToken = null;
                    user.PasswordResetTokenExpiry = null;
                }

                user.UpdatedAt = DateTime.UtcNow;
            }

            if (expiredTokenUsers.Any())
            {
                await context.SaveChangesAsync();
                _logger.LogInformation("Cleaned up expired tokens for {Count} users", expiredTokenUsers.Count);
            }
        }

        private async Task CleanupOldNotificationLogs(ApplicationDbContext context)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-180);
            var oldNotificationLogsCount = await context.NotificationLogs
                .Where(x => x.CreatedAt < cutoffDate)
                .CountAsync();

            if (oldNotificationLogsCount > 0)
            {
                await context.NotificationLogs
                    .Where(x => x.CreatedAt < cutoffDate)
                    .ExecuteDeleteAsync();

                _logger.LogInformation("Cleaned up {Count} old notification logs", oldNotificationLogsCount);
            }
        }
    }
}