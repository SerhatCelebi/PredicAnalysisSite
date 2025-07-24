using System.ComponentModel.DataAnnotations;

namespace VurduGololdu.API.Models
{
    public class ContactMessage
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;
        
        [StringLength(20)]
        public string? Phone { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;
        
        [Required]
        public string Message { get; set; } = string.Empty;
        
        public bool IsRead { get; set; } = false;
        
        public bool IsReplied { get; set; } = false;
        
        public string? AdminReply { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ReadAt { get; set; }
        
        public DateTime? RepliedAt { get; set; }
        
        // Foreign keys
        public int? UserId { get; set; } // Opsiyonel - giriş yapmış kullanıcılar için
        public int? RepliedByUserId { get; set; }
        
        // Navigation properties
        public virtual User? User { get; set; }
        public virtual User? RepliedByUser { get; set; }
    }
} 