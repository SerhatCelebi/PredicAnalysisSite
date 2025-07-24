using System.ComponentModel.DataAnnotations;

namespace VurduGololdu.API.DTOs
{
    public class CreatePredictionDto
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public bool IsPaid { get; set; } = false;

        public List<IFormFile>? Images { get; set; }
    }

    public class UpdatePredictionDto
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public bool IsPaid { get; set; } = false;
    }

    public class PredictionDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsPaid { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int ViewCount { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public UserDto User { get; set; } = null!;
        public bool IsLikedByCurrentUser { get; set; }
    }

    public class PredictionListDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsPaid { get; set; }
        public string? FirstImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ViewCount { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int UserId { get; set; }
        public UserDto? User { get; set; }
        public bool IsLikedByCurrentUser { get; set; } = false;
    }

    public class CommentDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserDto User { get; set; } = null!;
        public bool IsLikedByCurrentUser { get; set; }
        public int LikeCount { get; set; }
    }

    public class CreateCommentDto
    {
        [Required]
        public string Content { get; set; } = string.Empty;

        public IFormFile? Image { get; set; }
    }

    public class LikeDto
    {
        [Required]
        public int Type { get; set; } // LikeType enum değeri
    }

    public class PaymentNotificationDto
    {
        [Required]
        [StringLength(100)]
        public string SenderName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string BankName { get; set; } = string.Empty;

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; }

        [StringLength(100)]
        public string? TransactionReference { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        [Required]
        public int MembershipType { get; set; } // 1=Aylık, 2=3Aylık, 3=6Aylık
    }

    public class MembershipPackageDto
    {
        public int Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int DurationInMonths { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class BlockUserDto
    {
        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    public class GrantVipDto
    {
        [Required]
        public int MembershipType { get; set; } // 1=Aylık, 2=3Aylık, 3=6Aylık
    }

    public class ContactMessageDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Phone { get; set; }

        [Required]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;
    }

    public class PagedResponse<T>
    {
        public IEnumerable<T> Data { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int Size { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;
    }

    // Tahmin paylaşım sistemi için yeni DTOs
    public class SharePredictionDto
    {
        public string Platform { get; set; } = string.Empty; // "facebook", "twitter", "whatsapp", "copy"
        public string? Message { get; set; }
    }

    public class PredictionResultDto
    {
        [Required]
        public bool IsCorrect { get; set; }

        [StringLength(500)]
        public string? ResultNote { get; set; }

        public DateTime? ResultDate { get; set; }
    }

    public class PinPredictionDto
    {
        [Required]
        public bool IsPinned { get; set; }

        [StringLength(200)]
        public string? Reason { get; set; }
    }

    public class FeaturedPredictionDto
    {
        [Required]
        public bool IsFeatured { get; set; }

        [StringLength(200)]
        public string? Reason { get; set; }
    }

    // Captcha DTOs
    public class CaptchaRequestDto
    {
        [Required]
        [StringLength(100)]
        public string SessionId { get; set; } = string.Empty;
    }

    public class CaptchaVerifyDto
    {
        [Required]
        [StringLength(100)]
        public string SessionId { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string CaptchaCode { get; set; } = string.Empty;
    }

    public class CaptchaResponseDto
    {
        public string SessionId { get; set; } = string.Empty;
        public string ImageBase64 { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    // Tahmin beğenen kullanıcı bilgileri
    public class PredictionLikerDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public int LikeType { get; set; } // 1:Like, 2:Love, 3:Laugh, 4:Angry, 5:Sad, 6:Wow
        public string LikeTypeName { get; set; } = string.Empty;
        public DateTime LikedAt { get; set; }
    }

    // Tahmin beğenenlerin listesi response'u
    public class PredictionLikersDto
    {
        public int PredictionId { get; set; }
        public string PredictionTitle { get; set; } = string.Empty;
        public int TotalLikes { get; set; }
        public List<PredictionLikerDto> Likers { get; set; } = new();

        // Reaksiyon türü bazlı sayım
        public int LikeCount { get; set; }
        public int LoveCount { get; set; }
        public int LaughCount { get; set; }
        public int AngryCount { get; set; }
        public int SadCount { get; set; }
        public int WowCount { get; set; }
    }

    // Yorum beğenen kullanıcı bilgileri
    public class CommentLikerDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public int LikeType { get; set; } // 1:Like, 2:Love, 3:Laugh, 4:Angry, 5:Sad, 6:Wow
        public string LikeTypeName { get; set; } = string.Empty;
        public DateTime LikedAt { get; set; }
    }

    // Yorum beğenenlerin listesi response'u
    public class CommentLikersDto
    {
        public int CommentId { get; set; }
        public string CommentContent { get; set; } = string.Empty;
        public int TotalLikes { get; set; }
        public List<CommentLikerDto> Likers { get; set; } = new();

        // Reaksiyon türü bazlı sayım
        public int LikeCount { get; set; }
        public int LoveCount { get; set; }
        public int LaughCount { get; set; }
        public int AngryCount { get; set; }
        public int SadCount { get; set; }
        public int WowCount { get; set; }
    }

    // Süper Admin Rol Yönetimi DTOs
    public class RoleChangeDto
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        [Range(0, 3)]
        public int NewRole { get; set; } // 0:SuperAdmin, 1:Admin, 2:VipUser, 3:NormalUser

        [StringLength(500)]
        public string? Reason { get; set; }
    }

    public class GrantAdminDto
    {
        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Note { get; set; }
    }

    public class RevokeAdminDto
    {
        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        public bool ConvertToNormalUser { get; set; } = true; // false ise VipUser olur

        [StringLength(200)]
        public string? Note { get; set; }
    }

    public class SuperAdminActionDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // "GrantAdmin", "RevokeAdmin", "ChangeRole"
        public string? OldRole { get; set; }
        public string? NewRole { get; set; }
        public string? Reason { get; set; }
        public string? Note { get; set; }
        public DateTime ActionDate { get; set; }
        public string SuperAdminName { get; set; } = string.Empty;
    }

    public class AdminListDto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public bool IsActive { get; set; }
        public bool IsBlocked { get; set; }
        public string? ProfileImageUrl { get; set; }
    }
}