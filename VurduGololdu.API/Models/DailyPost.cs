using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VurduGololdu.API.Models
{
    public class DailyPost
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Content { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        [StringLength(100)]
        public string? Category { get; set; } // Örn: "Yemek", "Seyahat", "Düşünceler", "Teknoloji"

        [StringLength(500)]
        public string? Tags { get; set; } // Virgülle ayrılmış etiketler

        public bool IsPublished { get; set; } = true;
        public bool IsFeatured { get; set; } = false; // Öne çıkarılmış içerik

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Admin bilgileri
        [Required]
        public int AdminId { get; set; }
        
        [ForeignKey("AdminId")]
        public User Admin { get; set; } = null!;

        // İstatistikler
        public int ViewCount { get; set; } = 0;
        public int LikeCount { get; set; } = 0;
        public int CommentCount { get; set; } = 0;

        // Navigation properties
        public virtual ICollection<Like> Likes { get; set; } = new List<Like>();
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
} 