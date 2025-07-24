using System.ComponentModel.DataAnnotations;

namespace VurduGololdu.API.Models
{
    public class PasswordResetRequest
    {
        public int Id { get; set; }

        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string UserName { get; set; } = string.Empty;

        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ApprovedAt { get; set; }

        public int? ApprovedByUserId { get; set; }

        public string? ResetToken { get; set; }

        public DateTime? ResetTokenExpiry { get; set; }

        public bool IsApproved { get; set; } = false;

        public bool IsCompleted { get; set; } = false;

        public DateTime? CompletedAt { get; set; }

        // Navigation properties
        public virtual User? ApprovedByUser { get; set; }
    }
}