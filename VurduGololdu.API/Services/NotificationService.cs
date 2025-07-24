using Microsoft.EntityFrameworkCore;
using VurduGololdu.API.Data;
using VurduGololdu.API.Models;

namespace VurduGololdu.API.Services
{
    public interface INotificationService
    {
        Task SendPasswordResetNotificationAsync(int userId, string resetToken);
        Task SendEmailVerificationNotificationAsync(int userId, string verificationToken);
        Task SendWelcomeNotificationAsync(int userId);
        Task SendVipExpiryNotificationAsync(int userId);
        Task SendVipUpgradeNotificationAsync(int userId);
        Task SendNewPredictionNotificationAsync(string predictionTitle, int predictionId, bool isPaid, int adminUserId);
        Task SendNewDailyPostNotificationAsync(string postTitle, int postId, int adminUserId);
        Task SendNewCommentNotificationAsync(int predictionId, string commentContent, int commentUserId);
        Task<bool> LogNotificationAsync(int userId, string type, string category, string subject, string content, string status, string? relatedLink = null, int? actorUserId = null, string? actorFirstName = null, string? actorLastName = null, string? actorProfileImageUrl = null);
        Task SendPasswordChangedNotificationAsync(int userId);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationService> _logger;
        private readonly IAuditLogService _auditLogService;

        public NotificationService(
            ApplicationDbContext context,
            ILogger<NotificationService> logger,
            IAuditLogService auditLogService)
        {
            _context = context;
            _logger = logger;
            _auditLogService = auditLogService;
        }

        public async Task SendPasswordResetNotificationAsync(int userId, string resetToken)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                // Sadece in-app notification
                await LogNotificationAsync(userId, "InApp", "PasswordReset",
                    "Şifre Sıfırlama", "Şifre sıfırlama işlemi başlatıldı",
                    "Sent");

                await _auditLogService.LogUserActionAsync(userId, "PasswordResetNotificationSent", "User", userId,
                    "Password reset notification sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset notification for user {UserId}", userId);
            }
        }

        public async Task SendEmailVerificationNotificationAsync(int userId, string verificationToken)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                // Sadece in-app notification
                await LogNotificationAsync(userId, "InApp", "EmailVerification",
                    "Email Doğrulama", "Email doğrulama işlemi başlatıldı",
                    "Sent");

                await _auditLogService.LogUserActionAsync(userId, "EmailVerificationNotificationSent", "User", userId,
                    "Email verification notification sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email verification notification for user {UserId}", userId);
            }
        }

        public async Task SendWelcomeNotificationAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                // Sadece in-app notification
                await LogNotificationAsync(userId, "InApp", "Welcome",
                    "Hoş Geldiniz", "VurduGololdu ailesine hoş geldiniz!",
                    "Sent");

                await _auditLogService.LogUserActionAsync(userId, "WelcomeNotificationSent", "User", userId,
                    "Welcome notification sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send welcome notification for user {UserId}", userId);
            }
        }

        public async Task SendVipExpiryNotificationAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null || !user.VipExpiryDate.HasValue) return;

                // Sadece in-app notification
                await LogNotificationAsync(userId, "InApp", "VipExpiry",
                    "VIP Üyelik Sona Eriyor", $"VIP üyeliğiniz {user.VipExpiryDate.Value:dd/MM/yyyy} tarihinde sona erecek",
                    "Sent");

                await _auditLogService.LogUserActionAsync(userId, "VipExpiryNotificationSent", "User", userId,
                    "VIP expiry notification sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VIP expiry notification for user {UserId}", userId);
            }
        }

        public async Task SendVipUpgradeNotificationAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null || !user.VipExpiryDate.HasValue) return;

                // Sadece in-app notification
                await LogNotificationAsync(userId, "InApp", "VipUpgrade",
                    "VIP Üyelik Aktif", $"VIP üyeliğiniz {user.VipExpiryDate.Value:dd/MM/yyyy} tarihine kadar aktif",
                    "Sent");

                await _auditLogService.LogUserActionAsync(userId, "VipUpgradeNotificationSent", "User", userId,
                    "VIP upgrade notification sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VIP upgrade notification for user {UserId}", userId);
            }
        }

        public async Task SendNewPredictionNotificationAsync(string predictionTitle, int predictionId, bool isPaid, int adminUserId)
        {
            try
            {
                // Admin bilgilerini al
                var admin = await _context.Users.FindAsync(adminUserId);
                if (admin == null) return;

                List<User> users;
                if (isPaid)
                {
                    // Sadece VIP kullanıcılar
                    users = await _context.Users
                        .Where(u => u.NotifyOnNewPredictions && u.IsActive && u.VipExpiryDate != null && u.VipExpiryDate > DateTime.Now)
                        .ToListAsync();
                }
                else
                {
                    // Tüm aktif kullanıcılar
                    users = await _context.Users
                        .Where(u => u.NotifyOnNewPredictions && u.IsActive)
                        .ToListAsync();
                }

                var relatedLink = $"https://vurdugololdu.com/predictions/{predictionId}";

                // Send in-app notifications only
                foreach (var user in users)
                {
                    await LogNotificationAsync(
                        userId: user.Id,
                        type: "InApp",
                        category: "NewPrediction",
                        subject: "Yeni Tahmin Yayınlandı",
                        content: $"Yeni bir tahmin yayınlandı: {predictionTitle}",
                        status: "Sent",
                        relatedLink: relatedLink,
                        actorUserId: admin.Id,
                        actorFirstName: admin.FirstName,
                        actorLastName: admin.LastName,
                        actorProfileImageUrl: admin.ProfileImageUrl
                    );
                }

                _logger.LogInformation("New prediction notification sent to {UserCount} users for prediction {PredictionId} (VIP: {IsPaid})", users.Count, predictionId, isPaid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send new prediction notification for prediction {PredictionId}", predictionId);
            }
        }

        public async Task SendNewDailyPostNotificationAsync(string postTitle, int postId, int adminUserId)
        {
            try
            {
                // Admin bilgilerini al
                var admin = await _context.Users.FindAsync(adminUserId);
                if (admin == null) return;

                // Get users who want to be notified about new daily posts
                var users = await _context.Users
                    .Where(u => u.NotifyOnDailyPosts && u.IsActive)
                    .ToListAsync();

                var relatedLink = $"https://vurdugololdu.com/daily-posts/{postId}";

                // Send in-app notifications only
                foreach (var user in users)
                {
                    await LogNotificationAsync(
                        userId: user.Id,
                        type: "InApp",
                        category: "NewDailyPost",
                        subject: "Yeni Günlük Yayınlandı",
                        content: $"Yeni bir günlük paylaşım yayınlandı: {postTitle}",
                        status: "Sent",
                        relatedLink: relatedLink,
                        actorUserId: admin.Id,
                        actorFirstName: admin.FirstName,
                        actorLastName: admin.LastName,
                        actorProfileImageUrl: admin.ProfileImageUrl
                    );
                }

                _logger.LogInformation("New daily post notification sent to {UserCount} users for post {PostId}", users.Count, postId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send new daily post notification for post {PostId}", postId);
            }
        }

        public async Task SendNewCommentNotificationAsync(int predictionId, string commentContent, int commentUserId)
        {
            try
            {
                // Get prediction owner
                var prediction = await _context.Predictions
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == predictionId);

                if (prediction?.User == null) return;

                // Get comment author info
                var commentAuthor = await _context.Users.FindAsync(commentUserId);
                if (commentAuthor == null) return;

                var relatedLink = $"https://vurdugololdu.com/predictions/{predictionId}";

                // Send in-app notification to prediction owner
                await LogNotificationAsync(
                    userId: prediction.User.Id,
                    type: "InApp",
                    category: "NewComment",
                    subject: "Yeni Yorum",
                    content: $"Tahmininize yeni bir yorum yapıldı: {commentContent.Substring(0, Math.Min(100, commentContent.Length))}...",
                    status: "Sent",
                    relatedLink: relatedLink,
                    actorUserId: commentAuthor.Id,
                    actorFirstName: commentAuthor.FirstName,
                    actorLastName: commentAuthor.LastName,
                    actorProfileImageUrl: commentAuthor.ProfileImageUrl
                );

                _logger.LogInformation("New comment notification sent to user {UserId} for prediction {PredictionId}", prediction.User.Id, predictionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send new comment notification for prediction {PredictionId}", predictionId);
            }
        }

        public async Task<bool> LogNotificationAsync(int userId, string type, string category, string subject, string content, string status, string? relatedLink = null, int? actorUserId = null, string? actorFirstName = null, string? actorLastName = null, string? actorProfileImageUrl = null)
        {
            try
            {
                var notification = new NotificationLog
                {
                    UserId = userId,
                    Type = type,
                    Category = category,
                    Subject = subject,
                    Content = content,
                    Status = status,
                    RelatedLink = relatedLink,
                    ActorUserId = actorUserId,
                    ActorFirstName = actorFirstName,
                    ActorLastName = actorLastName,
                    ActorProfileImageUrl = actorProfileImageUrl,
                    CreatedAt = DateTime.UtcNow
                };

                _context.NotificationLogs.Add(notification);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log notification for user {UserId}", userId);
                return false;
            }
        }

        public async Task SendPasswordChangedNotificationAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return;

                // Sadece in-app notification
                await LogNotificationAsync(userId, "InApp", "PasswordChanged",
                    "Şifre Değiştirildi", "Hesabınızın şifresi başarıyla değiştirildi",
                    "Sent");

                await _auditLogService.LogUserActionAsync(userId, "PasswordChangedNotificationSent", "User", userId,
                    "Password changed notification sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password changed notification for user {UserId}", userId);
            }
        }
    }
}