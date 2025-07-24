using System.ComponentModel.DataAnnotations;

namespace VurduGololdu.API.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [StringLength(20)] // Phone artık isteğe bağlı
        public string? Phone { get; set; }

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; } = UserRole.NormalUser;

        public bool IsEmailVerified { get; set; } = false;

        public string? EmailVerificationToken { get; set; }

        public DateTime? EmailVerificationTokenExpiry { get; set; }

        public string? PasswordResetToken { get; set; }

        public DateTime? PasswordResetTokenExpiry { get; set; }

        public string? RefreshToken { get; set; }

        public DateTime? RefreshTokenExpiry { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsBlocked { get; set; } = false;

        public DateTime? VipExpiryDate { get; set; }

        // Profil resmi
        [StringLength(500)]
        public string? ProfileImageUrl { get; set; }

        public int? BlockedByUserId { get; set; }

        public string? BlockReason { get; set; }

        public DateTime? BlockedAt { get; set; }

        // Notification settings
        public bool NotifyOnNewPredictions { get; set; } = true;
        public bool NotifyOnComments { get; set; } = true;
        public bool NotifyOnVipExpiry { get; set; } = true;
        public bool NotifyOnDailyPosts { get; set; } = true;

        // Push notification tokens removed for website-only version

        // Login tracking
        public DateTime? LastLoginDate { get; set; }
        public DateTime? LastFailedLoginDate { get; set; }
        public int FailedLoginAttempts { get; set; } = 0;

        // Navigation properties
        public virtual User? BlockedByUser { get; set; }
        public virtual ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public virtual ICollection<Like> Likes { get; set; } = new List<Like>();
        public virtual ICollection<PaymentNotification> PaymentNotifications { get; set; } = new List<PaymentNotification>();
        public virtual ICollection<NotificationLog> NotificationLogs { get; set; } = new List<NotificationLog>();
    }

    public enum UserRole
    {
        SuperAdmin = 0,
        Admin = 1,
        VipUser = 2,
        NormalUser = 3
    }
}