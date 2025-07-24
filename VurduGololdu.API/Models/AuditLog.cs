using System.ComponentModel.DataAnnotations;

namespace VurduGololdu.API.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty; // Login, CreatePrediction, ApproveComment vs.
        
        [Required]
        [StringLength(50)]
        public string Entity { get; set; } = string.Empty; // User, Prediction, Comment vs.
        
        public int? EntityId { get; set; } // İlgili entity'nin ID'si
        
        public int? UserId { get; set; } // İşlemi yapan kullanıcı
        
        [StringLength(100)]
        public string? UserEmail { get; set; }
        
        [StringLength(200)]
        public string? UserName { get; set; }
        
        [Required]
        [StringLength(45)]
        public string IpAddress { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? UserAgent { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Endpoint { get; set; } = string.Empty; // API endpoint
        
        [Required]
        [StringLength(10)]
        public string HttpMethod { get; set; } = string.Empty; // GET, POST, PUT, DELETE
        
        public string? RequestData { get; set; } // JSON formatında request data
        
        public string? ResponseData { get; set; } // JSON formatında response data
        
        public int StatusCode { get; set; } // HTTP status code
        
        public long Duration { get; set; } // İşlem süresi (millisecond)
        
        [StringLength(1000)]
        public string? ErrorMessage { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public AuditLogLevel Level { get; set; } = AuditLogLevel.Info;
        
        // Navigation property
        public virtual User? User { get; set; }
    }
    
    public enum AuditLogLevel
    {
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }
} 