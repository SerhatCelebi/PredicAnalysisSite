using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VurduGololdu.API.Data;
using VurduGololdu.API.DTOs;
using VurduGololdu.API.Models;

namespace VurduGololdu.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentNotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PaymentNotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("packages")]
        public IActionResult GetMembershipPackages()
        {
            var packages = new List<MembershipPackageDto>
            {
                new() { Type = 1, Name = "Aylık Abonelik", Price = 1000, DurationInMonths = 1, Description = "1 ay boyunca tüm ücretli tahminlere erişim" },
                new() { Type = 2, Name = "3 Aylık Abonelik", Price = 2250, DurationInMonths = 3, Description = "3 ay boyunca tüm ücretli tahminlere erişim" },
                new() { Type = 3, Name = "6 Aylık Abonelik", Price = 3900, DurationInMonths = 6, Description = "6 ay boyunca tüm ücretli tahminlere erişim" }
            };

            return Ok(packages);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreatePaymentNotification(PaymentNotificationDto dto)
        {
            var userId = GetCurrentUserId()!.Value;

            // Üyelik paket kontrolü
            var membershipType = (MembershipType)dto.MembershipType;
            var expectedAmount = membershipType switch
            {
                MembershipType.Monthly => 1000m,
                MembershipType.ThreeMonths => 2250m,
                MembershipType.SixMonths => 3900m,
                _ => throw new ArgumentException("Geçersiz üyelik türü")
            };

            if (dto.Amount != expectedAmount)
            {
                return BadRequest($"Bu üyelik türü için beklenen tutar: ₺{expectedAmount}");
            }

            var notification = new PaymentNotification
            {
                SenderName = dto.SenderName,
                BankName = dto.BankName,
                Amount = dto.Amount,
                TransactionDate = dto.TransactionDate,
                TransactionReference = dto.TransactionReference,
                Note = dto.Note,
                MembershipType = membershipType,
                UserId = userId,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.PaymentNotifications.Add(notification);
            await _context.SaveChangesAsync();

            var packageName = membershipType switch
            {
                MembershipType.Monthly => "Aylık Abonelik",
                MembershipType.ThreeMonths => "3 Aylık Abonelik",
                MembershipType.SixMonths => "6 Aylık Abonelik",
                _ => "Bilinmeyen Paket"
            };

            return Ok(new { message = $"{packageName} için ödeme bildirimi başarıyla gönderildi. İncelenip onaylandığında VIP üyeliğiniz aktif olacak." });
        }

        [HttpGet("my-notifications")]
        [Authorize]
        public async Task<IActionResult> GetMyNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userId = GetCurrentUserId()!.Value;

            var query = _context.PaymentNotifications
                .Where(p => p.UserId == userId);

            var totalCount = await query.CountAsync();
            var notifications = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    Id = p.Id,
                    SenderName = p.SenderName,
                    BankName = p.BankName,
                    Amount = p.Amount,
                    TransactionDate = p.TransactionDate,
                    TransactionReference = p.TransactionReference,
                    Note = p.Note,
                    Status = p.Status.ToString(),
                    AdminNote = p.AdminNote,
                    CreatedAt = p.CreatedAt,
                    ProcessedAt = p.ProcessedAt
                })
                .ToListAsync();

            return Ok(new
            {
                notifications,
                totalCount,
                currentPage = page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        [HttpGet("pending")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPendingNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var query = _context.PaymentNotifications
                .Include(p => p.User)
                .Where(p => p.Status == PaymentStatus.Pending);

            var totalCount = await query.CountAsync();
            var notifications = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    Id = p.Id,
                    SenderName = p.SenderName,
                    BankName = p.BankName,
                    Amount = p.Amount,
                    TransactionDate = p.TransactionDate,
                    TransactionReference = p.TransactionReference,
                    Note = p.Note,
                    Status = p.Status.ToString(),
                    CreatedAt = p.CreatedAt,
                    User = new
                    {
                        Id = p.User.Id,
                        FirstName = p.User.FirstName,
                        LastName = p.User.LastName,
                        Email = p.User.Email,
                        Phone = p.User.Phone
                    }
                })
                .ToListAsync();

            return Ok(new
            {
                notifications,
                totalCount,
                currentPage = page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        [HttpPost("{id}/approve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApprovePayment(int id, [FromBody] string? adminNote = null)
        {
            var notification = await _context.PaymentNotifications
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (notification == null)
            {
                return NotFound("Ödeme bildirimi bulunamadı");
            }

            if (notification.User.Role == UserRole.Admin)
            {
                return BadRequest("Admin kullanıcıları için ödeme onayı yapılamaz veya rolleri bu şekilde değiştirilemez.");
            }

            var adminUserId = GetCurrentUserId()!.Value;

            notification.Status = PaymentStatus.Approved;
            notification.AdminNote = adminNote;
            notification.ProcessedAt = DateTime.UtcNow;
            notification.ProcessedByUserId = adminUserId;

            // Kullanıcıyı VIP yap ve süre belirle
            notification.User.Role = UserRole.VipUser;
            
            var currentExpiry = notification.User.VipExpiryDate ?? DateTime.UtcNow;
            var startDate = currentExpiry > DateTime.UtcNow ? currentExpiry : DateTime.UtcNow;
            
            notification.User.VipExpiryDate = notification.MembershipType switch
            {
                MembershipType.Monthly => startDate.AddMonths(1),
                MembershipType.ThreeMonths => startDate.AddMonths(3),
                MembershipType.SixMonths => startDate.AddMonths(6),
                _ => startDate.AddMonths(1)
            };

            await _context.SaveChangesAsync();

            return Ok(new { message = "Ödeme onaylandı ve kullanıcı VIP üye oldu" });
        }

        [HttpPost("{id}/reject")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RejectPayment(int id, [FromBody] string? adminNote = null)
        {
            var notification = await _context.PaymentNotifications.FindAsync(id);
            if (notification == null)
            {
                return NotFound("Ödeme bildirimi bulunamadı");
            }

            var adminUserId = GetCurrentUserId()!.Value;

            notification.Status = PaymentStatus.Rejected;
            notification.AdminNote = adminNote;
            notification.ProcessedAt = DateTime.UtcNow;
            notification.ProcessedByUserId = adminUserId;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Ödeme reddedildi" });
        }

        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllNotifications(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10,
            [FromQuery] PaymentStatus? status = null)
        {
            var query = _context.PaymentNotifications
                .Include(p => p.User)
                .Include(p => p.ProcessedByUser)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }

            var totalCount = await query.CountAsync();
            var notifications = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    Id = p.Id,
                    SenderName = p.SenderName,
                    BankName = p.BankName,
                    Amount = p.Amount,
                    TransactionDate = p.TransactionDate,
                    TransactionReference = p.TransactionReference,
                    Note = p.Note,
                    Status = p.Status.ToString(),
                    AdminNote = p.AdminNote,
                    CreatedAt = p.CreatedAt,
                    ProcessedAt = p.ProcessedAt,
                    User = new
                    {
                        Id = p.User.Id,
                        FirstName = p.User.FirstName,
                        LastName = p.User.LastName,
                        Email = p.User.Email,
                        Phone = p.User.Phone
                    },
                    ProcessedByUser = p.ProcessedByUser != null ? new
                    {
                        Id = p.ProcessedByUser.Id,
                        FirstName = p.ProcessedByUser.FirstName,
                        LastName = p.ProcessedByUser.LastName
                    } : null
                })
                .ToListAsync();

            return Ok(new
            {
                notifications,
                totalCount,
                currentPage = page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : null;
        }
    }
} 