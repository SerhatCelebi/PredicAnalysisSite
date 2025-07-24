using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VurduGololdu.API.Models
{
    public class PaymentNotification
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string SenderName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string BankName { get; set; } = string.Empty;
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        
        [Required]
        public DateTime TransactionDate { get; set; }
        
        [StringLength(100)]
        public string? TransactionReference { get; set; }
        
        [StringLength(500)]
        public string? Note { get; set; }
        
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        
        public MembershipType MembershipType { get; set; } = MembershipType.Monthly;
        
        public string? AdminNote { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ProcessedAt { get; set; }
        
        // Foreign keys
        public int UserId { get; set; }
        public int? ProcessedByUserId { get; set; }
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual User? ProcessedByUser { get; set; }
    }
    
    public enum PaymentStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3
    }
    
    public enum MembershipType
    {
        Monthly = 1,      // 1 ay - ₺1.000
        ThreeMonths = 2,  // 3 ay - ₺2.250  
        SixMonths = 3     // 6 ay - ₺3.900
    }
} 