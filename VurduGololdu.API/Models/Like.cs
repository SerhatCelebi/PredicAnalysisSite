using System.ComponentModel.DataAnnotations;

namespace VurduGololdu.API.Models
{
    public class Like
    {
        public int Id { get; set; }
        
        public LikeType Type { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Foreign keys
        public int UserId { get; set; }
        public int? PredictionId { get; set; }
        public int? CommentId { get; set; }
        public int? DailyPostId { get; set; }
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Prediction? Prediction { get; set; }
        public virtual Comment? Comment { get; set; }
        public virtual DailyPost? DailyPost { get; set; }
    }
    
    public enum LikeType
    {
        Like = 1,
        Love = 2,
        Laugh = 3,
        Angry = 4,
        Sad = 5,
        Wow = 6
    }
} 