using System.ComponentModel.DataAnnotations;

namespace VurduGololdu.API.Models
{
    public class NotificationLog
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Type { get; set; } = string.Empty; // Email, Push, SMS

        [Required]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty; // PasswordReset, VipExpiry, NewPrediction, etc.

        [Required]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        public string? Content { get; set; }

        [Required]
        [StringLength(100)]
        public string Status { get; set; } = string.Empty; // Sent, Read

        public string? ErrorMessage { get; set; }

        // Yeni alanlar - Link ve kullanıcı bilgileri
        [StringLength(500)]
        public string? RelatedLink { get; set; } // Paylaşım/yorum linki

        [StringLength(100)]
        public string? ActorFirstName { get; set; } // İşlemi yapan kullanıcının adı

        [StringLength(100)]
        public string? ActorLastName { get; set; } // İşlemi yapan kullanıcının soyadı

        [StringLength(500)]
        public string? ActorProfileImageUrl { get; set; } // İşlemi yapan kullanıcının profil resmi

        public int? ActorUserId { get; set; } // İşlemi yapan kullanıcının ID'si

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReadAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
    }

    public static class NotificationType
    {
        public const string InApp = "InApp";
    }

    public static class NotificationStatus
    {
        public const string Sent = "Sent";
        public const string Read = "Read";
    }

    public static class NotificationCategory
    {
        public const string PasswordReset = "PasswordReset";
        public const string VipExpiry = "VipExpiry";
        public const string NewPrediction = "NewPrediction";
        public const string NewDailyPost = "NewDailyPost";
        public const string NewComment = "NewComment";
        public const string Welcome = "Welcome";
        public const string VipUpgrade = "VipUpgrade";
        public const string PasswordChanged = "PasswordChanged";
    }
}