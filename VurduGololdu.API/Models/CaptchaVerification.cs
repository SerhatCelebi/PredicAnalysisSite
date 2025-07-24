using System.ComponentModel.DataAnnotations;

namespace VurduGololdu.API.Models
{
    public class CaptchaVerification
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string SessionId { get; set; } = string.Empty;
        
        [Required]
        [StringLength(10)]
        public string CaptchaCode { get; set; } = string.Empty;
        
        [Required]
        [StringLength(500)]
        public string CaptchaImageBase64 { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(5);
        
        public bool IsUsed { get; set; } = false;
        
        public bool IsVerified { get; set; } = false;
        
        public int AttemptCount { get; set; } = 0;
        
        public int MaxAttempts { get; set; } = 3;
        
        [StringLength(45)]
        public string? IpAddress { get; set; }
        
        [StringLength(500)]
        public string? UserAgent { get; set; }

        // Property alias for backward compatibility
        public string ImageBase64 => CaptchaImageBase64;
    }
} 