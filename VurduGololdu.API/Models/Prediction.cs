using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VurduGololdu.API.Models
{
    public class Prediction
    {
        public int Id { get; set; }
        
        [Required]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        public string Content { get; set; } = string.Empty;
        
        public bool IsPaid { get; set; } = false;
        
        public string? ImageUrls { get; set; } // JSON olarak birden fazla resim URL'si
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public int ViewCount { get; set; } = 0;
        
        public int LikeCount { get; set; } = 0;
        
        public int CommentCount { get; set; } = 0;
        
        // Tahmin durumu ve paylaşım özellikleri
        public PredictionStatus Status { get; set; } = PredictionStatus.Pending;
        public bool IsShared { get; set; } = false;
        public DateTime? ResultDate { get; set; } // Tahminin sonuçlanacağı tarih
        public bool? IsCorrect { get; set; } // Tahmin doğru mu? (null = henüz belli değil)
        public string? ResultNote { get; set; } // Sonuç açıklaması
        
        // Başarı ve öne çıkarma
        public bool IsFeatured { get; set; } = false; // En başarılı tahmin olarak seçildi mi?
        public bool IsPinned { get; set; } = false; // Başa sabitlendi mi?
        public DateTime? PinnedAt { get; set; }
        public int? PinnedByUserId { get; set; } // Kim sabitledi (admin)
        
        // Paylaşım istatistikleri
        public int ShareCount { get; set; } = 0;
        public DateTime? LastSharedAt { get; set; }
        
        // Foreign keys
        public int UserId { get; set; }
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual User? PinnedByUser { get; set; }
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public virtual ICollection<Like> Likes { get; set; } = new List<Like>();
    }
    
    public enum PredictionStatus
    {
        Pending = 1,    // Beklemede
        Active = 2,     // Aktif
        Completed = 3,  // Tamamlandı
        Cancelled = 4   // İptal edildi
    }
} 