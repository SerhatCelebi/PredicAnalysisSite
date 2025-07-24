using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VurduGololdu.API.Models
{
    public class DailyAnalytics
    {
        public int Id { get; set; }
        
        public DateTime Date { get; set; }
        
        // Kullanıcı istatistikleri
        public int NewUserCount { get; set; } = 0;
        public int ActiveUserCount { get; set; } = 0;
        public int TotalUserCount { get; set; } = 0;
        
        // Tahmin istatistikleri
        public int NewPredictionCount { get; set; } = 0;
        public int CompletedPredictionCount { get; set; } = 0;
        public int CorrectPredictionCount { get; set; } = 0;
        public int TotalPredictionCount { get; set; } = 0;
        
        // Başarı oranları
        [Column(TypeName = "decimal(5,2)")]
        public decimal OverallSuccessRate { get; set; } = 0;
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal VipSuccessRate { get; set; } = 0;
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal NormalUserSuccessRate { get; set; } = 0;
        
        // Gelir istatistikleri
        [Column(TypeName = "decimal(18,2)")]
        public decimal DailyRevenue { get; set; } = 0;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRevenue { get; set; } = 0;
        
        public int NewVipUserCount { get; set; } = 0;
        public int ExpiredVipUserCount { get; set; } = 0;
        
        // Engagement istatistikleri
        public int TotalLikeCount { get; set; } = 0;
        public int TotalCommentCount { get; set; } = 0;
        public int TotalShareCount { get; set; } = 0;
        public int TotalViewCount { get; set; } = 0;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
    
    public class UserSuccessStats
    {
        public int Id { get; set; }
        
        public int UserId { get; set; }
        
        public int TotalPredictions { get; set; } = 0;
        public int CorrectPredictions { get; set; } = 0;
        public int IncorrectPredictions { get; set; } = 0;
        public int PendingPredictions { get; set; } = 0;
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal SuccessRate { get; set; } = 0;
        
        public int CurrentStreak { get; set; } = 0; // Ardışık doğru tahmin sayısı
        public int BestStreak { get; set; } = 0; // En iyi seri
        
        public int TotalLikes { get; set; } = 0;
        public int TotalComments { get; set; } = 0;
        public int TotalShares { get; set; } = 0;
        public int TotalViews { get; set; } = 0;
        
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
} 