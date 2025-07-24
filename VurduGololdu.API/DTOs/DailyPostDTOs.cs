using System.ComponentModel.DataAnnotations;

namespace VurduGololdu.API.DTOs
{
    public class CreateDailyPostDto
    {
        [Required(ErrorMessage = "Başlık gereklidir")]
        [StringLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "İçerik gereklidir")]
        [StringLength(2000, ErrorMessage = "İçerik en fazla 2000 karakter olabilir")]
        public string Content { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Kategori en fazla 100 karakter olabilir")]
        public string? Category { get; set; }

        [StringLength(500, ErrorMessage = "Etiketler en fazla 500 karakter olabilir")]
        public string? Tags { get; set; }

        public bool IsPublished { get; set; } = true;
        public bool IsFeatured { get; set; } = false;

        public IFormFile? Image { get; set; }
    }

    public class UpdateDailyPostDto
    {
        [Required(ErrorMessage = "Başlık gereklidir")]
        [StringLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "İçerik gereklidir")]
        [StringLength(2000, ErrorMessage = "İçerik en fazla 2000 karakter olabilir")]
        public string Content { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Kategori en fazla 100 karakter olabilir")]
        public string? Category { get; set; }

        [StringLength(500, ErrorMessage = "Etiketler en fazla 500 karakter olabilir")]
        public string? Tags { get; set; }

        public bool IsPublished { get; set; } = true;
        public bool IsFeatured { get; set; } = false;

        public IFormFile? Image { get; set; }
    }

    public class DailyPostDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string? Category { get; set; }
        public string? Tags { get; set; }
        public bool IsPublished { get; set; }
        public bool IsFeatured { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int AdminId { get; set; }
        public string AdminName { get; set; } = string.Empty;
        public string? AdminProfileImageUrl { get; set; }
        public int ViewCount { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public bool IsLikedByCurrentUser { get; set; } = false;
        public List<string> TagList => Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim()).ToList() ?? new List<string>();
    }

    public class DailyPostSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string? Category { get; set; }
        public bool IsFeatured { get; set; }
        public DateTime CreatedAt { get; set; }
        public string AdminName { get; set; } = string.Empty;
        public string? AdminProfileImageUrl { get; set; }
        public int ViewCount { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public bool IsLikedByCurrentUser { get; set; } = false;

        // İçeriği kısalt (150 karakter)
        public string ShortContent => Content.Length > 150
            ? Content.Substring(0, 150) + "..."
            : Content;
    }

    public class DailyPostStatsDto
    {
        public int TotalPosts { get; set; }
        public int PublishedPosts { get; set; }
        public int FeaturedPosts { get; set; }
        public int TotalViews { get; set; }
        public int TotalLikes { get; set; }
        public int TotalComments { get; set; }
        public List<CategoryStatsDto> CategoryStats { get; set; } = new();
    }

    public class CategoryStatsDto
    {
        public string Category { get; set; } = string.Empty;
        public int PostCount { get; set; }
        public int TotalViews { get; set; }
        public int TotalLikes { get; set; }
    }

    // Beğenen kullanıcı bilgileri
    public class PostLikerDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public int LikeType { get; set; } // 1:Like, 2:Love, 3:Laugh, 4:Angry, 5:Sad, 6:Wow
        public string LikeTypeName { get; set; } = string.Empty;
        public DateTime LikedAt { get; set; }
    }

    // Beğenenlerin listesi response'u
    public class PostLikersDto
    {
        public int PostId { get; set; }
        public string PostTitle { get; set; } = string.Empty;
        public int TotalLikes { get; set; }
        public List<PostLikerDto> Likers { get; set; } = new();

        // Reaksiyon türü bazlı sayım
        public int LikeCount { get; set; }
        public int LoveCount { get; set; }
        public int LaughCount { get; set; }
        public int AngryCount { get; set; }
        public int SadCount { get; set; }
        public int WowCount { get; set; }
    }
}