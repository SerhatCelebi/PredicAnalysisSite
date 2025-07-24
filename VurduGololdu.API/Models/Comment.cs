using System.ComponentModel.DataAnnotations;

namespace VurduGololdu.API.Models
{
    public class Comment
    {
        public int Id { get; set; }
        
        [Required]
        public string Content { get; set; } = string.Empty;
        
        public string? ImageUrl { get; set; } // Tek resim max 2MB
        
        public bool IsApproved { get; set; } = false;
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
        
        public DateTime? ApprovedAt { get; set; }
        
        public int? ApprovedByUserId { get; set; }
        
        // Foreign keys
        public int UserId { get; set; }
        public int? PredictionId { get; set; }
        public int? DailyPostId { get; set; }
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Prediction? Prediction { get; set; }
        public virtual DailyPost? DailyPost { get; set; }
        public virtual User? ApprovedByUser { get; set; }
        public virtual ICollection<Like> Likes { get; set; } = new List<Like>();
    }
} 