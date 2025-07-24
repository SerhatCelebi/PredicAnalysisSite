using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VurduGololdu.API.Data;
using VurduGololdu.API.DTOs;
using VurduGololdu.API.Models;
using VurduGololdu.API.Extensions;
using VurduGololdu.API.Helpers;

namespace VurduGololdu.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] UserRole? role = null,
            [FromQuery] bool? isBlocked = null,
            [FromQuery] string? search = null)
        {
            var query = _context.Users
                .Include(u => u.BlockedByUser)
                .AsQueryable();

            if (role.HasValue)
                query = query.Where(u => u.Role == role.Value);

            if (isBlocked.HasValue)
                query = query.Where(u => u.IsBlocked == isBlocked.Value);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u =>
                    u.FirstName.Contains(search) ||
                    u.LastName.Contains(search) ||
                    u.Email.Contains(search));
            }

            var totalCount = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Phone = u.Phone,
                    Role = u.Role.ToString(),
                    IsEmailVerified = u.IsEmailVerified,
                    IsActive = u.IsActive,
                    IsBlocked = u.IsBlocked,
                    BlockReason = u.BlockReason,
                    BlockedAt = u.BlockedAt,
                    VipExpiryDate = u.VipExpiryDate,
                    IsVipActive = u.VipExpiryDate.HasValue && u.VipExpiryDate > DateTime.UtcNow,
                    CreatedAt = u.CreatedAt,
                    BlockedByUser = u.BlockedByUser != null ? new
                    {
                        Id = u.BlockedByUser.Id,
                        FirstName = u.BlockedByUser.FirstName,
                        LastName = u.BlockedByUser.LastName
                    } : null
                })
                .ToListAsync();

            return Ok(new
            {
                users,
                totalCount,
                currentPage = page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUserDetails(int id)
        {
            var user = await _context.Users
                .Include(u => u.BlockedByUser)
                .Include(u => u.PaymentNotifications)
                .Include(u => u.Predictions)
                .Include(u => u.Comments)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound("Kullanıcı bulunamadı");

            var userDetails = new
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role.ToString(),
                IsEmailVerified = user.IsEmailVerified,
                IsActive = user.IsActive,
                IsBlocked = user.IsBlocked,
                BlockReason = user.BlockReason,
                BlockedAt = user.BlockedAt,
                VipExpiryDate = user.VipExpiryDate,
                IsVipActive = user.VipExpiryDate.HasValue && user.VipExpiryDate > DateTime.UtcNow,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                BlockedByUser = user.BlockedByUser != null ? new
                {
                    Id = user.BlockedByUser.Id,
                    FirstName = user.BlockedByUser.FirstName,
                    LastName = user.BlockedByUser.LastName
                } : null,
                Stats = new
                {
                    TotalPayments = user.PaymentNotifications.Count,
                    ApprovedPayments = user.PaymentNotifications.Count(p => p.Status == PaymentStatus.Approved),
                    TotalPredictions = user.Predictions.Count,
                    TotalComments = user.Comments.Count
                }
            };

            return Ok(userDetails);
        }

        [HttpPost("users/{id}/block")]
        public async Task<IActionResult> BlockUser(int id, BlockUserDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound("Kullanıcı bulunamadı");

            if (user.Role == UserRole.Admin)
                return BadRequest("Admin kullanıcıları engellenemez");

            var adminUserId = User.GetCurrentUserId()!.Value;

            user.IsBlocked = true;
            user.BlockReason = dto.Reason;
            user.BlockedAt = DateTime.UtcNow;
            user.BlockedByUserId = adminUserId;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Kullanıcı başarıyla engellendi" });
        }

        [HttpPost("users/{id}/unblock")]
        public async Task<IActionResult> UnblockUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound("Kullanıcı bulunamadı");

            user.IsBlocked = false;
            user.BlockReason = null;
            user.BlockedAt = null;
            user.BlockedByUserId = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Kullanıcının engeli kaldırıldı" });
        }

        [HttpPost("users/{id}/grant-vip")]
        public async Task<IActionResult> GrantVipMembership(int id, GrantVipDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound("Kullanıcı bulunamadı");

            if (user.Role == UserRole.Admin)
            {
                return BadRequest("Admin kullanıcılarına VIP üyelik verilemez veya rolleri bu şekilde değiştirilemez.");
            }

            // Debug: Gelen değeri kontrol et
            DebugConsole.Log($"🔍 GrantVip Debug - Received MembershipType: {dto.MembershipType}");

            // Enum casting kontrolü
            if (!Enum.IsDefined(typeof(MembershipType), dto.MembershipType))
            {
                DebugConsole.Log($"🚨 Invalid MembershipType value: {dto.MembershipType}");
                return BadRequest($"Geçersiz üyelik tipi: {dto.MembershipType}. Geçerli değerler: 1 (Aylık), 2 (3 Aylık), 3 (6 Aylık)");
            }

            var membershipType = (MembershipType)dto.MembershipType;
            DebugConsole.Log($"✅ Converted to enum: {membershipType} (value: {(int)membershipType})");

            user.Role = UserRole.VipUser;

            var currentExpiry = user.VipExpiryDate ?? DateTime.UtcNow;
            var startDate = currentExpiry > DateTime.UtcNow ? currentExpiry : DateTime.UtcNow;

            // Switch statement'ı daha güvenli hale getir
            var monthsToAdd = membershipType switch
            {
                MembershipType.Monthly => 1,
                MembershipType.ThreeMonths => 3,
                MembershipType.SixMonths => 6,
                _ => throw new ArgumentException($"Desteklenmeyen üyelik tipi: {membershipType}")
            };

            user.VipExpiryDate = startDate.AddMonths(monthsToAdd);
            user.UpdatedAt = DateTime.UtcNow;

            DebugConsole.Log($"✅ VIP Expiry set to: {user.VipExpiryDate} (added {monthsToAdd} months)");

            await _context.SaveChangesAsync();

            var packageName = membershipType switch
            {
                MembershipType.Monthly => "Aylık",
                MembershipType.ThreeMonths => "3 Aylık",
                MembershipType.SixMonths => "6 Aylık",
                _ => throw new ArgumentException($"Desteklenmeyen üyelik tipi: {membershipType}")
            };

            return Ok(new
            {
                message = $"Kullanıcıya {packageName} VIP üyelik verildi",
                vipExpiryDate = user.VipExpiryDate,
                membershipType = (int)membershipType,
                monthsAdded = monthsToAdd
            });
        }

        [HttpPost("users/{id}/change-role")]
        public async Task<IActionResult> ChangeUserRole(int id, [FromBody] int newRole)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound("Kullanıcı bulunamadı");

            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var currentUserId = User.GetCurrentUserId()!.Value;

            // Güvenlik kontrolleri
            if (user.Role == UserRole.SuperAdmin)
                return BadRequest("SuperAdmin kullanıcılarının rolü değiştirilemez");

            if (user.Role == UserRole.Admin && currentUserRole != "SuperAdmin")
                return BadRequest("Admin kullanıcılarının rolü sadece SuperAdmin tarafından değiştirilebilir");

            if (user.Id == currentUserId)
                return BadRequest("Kendi rolünüzü değiştiremezsiniz");

            if (!Enum.IsDefined(typeof(UserRole), newRole))
                return BadRequest("Geçersiz rol");

            var targetRole = (UserRole)newRole;

            // SuperAdmin ve Admin rolü verme yetkisi kontrolü
            if (targetRole == UserRole.SuperAdmin)
                return BadRequest("SuperAdmin rolü bu endpoint ile verilemez");

            if (targetRole == UserRole.Admin && currentUserRole != "SuperAdmin")
                return BadRequest("Admin rolü sadece SuperAdmin tarafından verilebilir");

            var oldRole = user.Role;
            user.Role = targetRole;
            user.UpdatedAt = DateTime.UtcNow;

            // Eğer VIP'ten normal kullanıcıya dönüyorsa VIP tarihini sıfırla
            if (oldRole == UserRole.VipUser && user.Role == UserRole.NormalUser)
            {
                user.VipExpiryDate = null;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Kullanıcının rolü {oldRole} -> {user.Role} olarak değiştirildi"
            });
        }

        [HttpPost("users/{id}/revoke-vip")]
        public async Task<IActionResult> RevokeVipMembership(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound("Kullanıcı bulunamadı");

            if (user.Role != UserRole.VipUser)
                return BadRequest("Kullanıcı zaten VIP üye değil");

            user.Role = UserRole.NormalUser;
            user.VipExpiryDate = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Kullanıcının VIP üyeliği iptal edildi" });
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetAdminStats()
        {
            var totalUsers = await _context.Users.CountAsync();
            var activeUsers = await _context.Users.CountAsync(u => u.IsActive && !u.IsBlocked);
            var blockedUsers = await _context.Users.CountAsync(u => u.IsBlocked);
            var vipUsers = await _context.Users.CountAsync(u => u.Role == UserRole.VipUser);
            var activeVipUsers = await _context.Users.CountAsync(u =>
                u.Role == UserRole.VipUser &&
                u.VipExpiryDate.HasValue &&
                u.VipExpiryDate > DateTime.UtcNow);

            var totalPredictions = await _context.Predictions.CountAsync(p => p.IsActive);
            var pendingComments = await _context.Comments.CountAsync(c => !c.IsApproved && c.IsActive);
            var pendingPayments = await _context.PaymentNotifications.CountAsync(p => p.Status == PaymentStatus.Pending);
            var unreadMessages = await _context.ContactMessages.CountAsync(m => !m.IsRead);

            var today = DateTime.Today;
            var todayRegistrations = await _context.Users.CountAsync(u => u.CreatedAt.Date == today);
            var todayPayments = await _context.PaymentNotifications.CountAsync(p => p.CreatedAt.Date == today);

            return Ok(new
            {
                users = new
                {
                    total = totalUsers,
                    active = activeUsers,
                    blocked = blockedUsers,
                    vip = vipUsers,
                    activeVip = activeVipUsers,
                    todayRegistrations
                },
                content = new
                {
                    totalPredictions,
                    pendingComments,
                    pendingPayments,
                    unreadMessages,
                    todayPayments
                }
            });
        }

        [HttpGet("vip-expiring")]
        public async Task<IActionResult> GetExpiringVipUsers([FromQuery] int days = 7)
        {
            var expiryDate = DateTime.UtcNow.AddDays(days);

            var expiringUsers = await _context.Users
                .Where(u => u.Role == UserRole.VipUser &&
                           u.VipExpiryDate.HasValue &&
                           u.VipExpiryDate <= expiryDate &&
                           u.VipExpiryDate > DateTime.UtcNow)
                .Select(u => new
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    VipExpiryDate = u.VipExpiryDate,
                    DaysRemaining = (int)(u.VipExpiryDate!.Value - DateTime.UtcNow).TotalDays
                })
                .OrderBy(u => u.VipExpiryDate)
                .ToListAsync();

            return Ok(expiringUsers);
        }

        // GetCurrentUserId kaldırıldı – Extensions kullanılacak

        // Sadece SuperAdmin yetkisi gerektiren endpoint'ler
        [HttpPost("users/{id}/grant-admin")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GrantAdminRole(int id, GrantAdminDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound("Kullanıcı bulunamadı");

            var superAdminId = User.GetCurrentUserId()!.Value;
            if (user.Id == superAdminId)
                return BadRequest("Kendi yetkilerinizi değiştiremezsiniz");

            if (user.Role == UserRole.SuperAdmin)
                return BadRequest("SuperAdmin kullanıcılarının rolü değiştirilemez");

            if (user.Role == UserRole.Admin)
                return BadRequest("Kullanıcı zaten Admin rolünde");

            var oldRole = user.Role;
            user.Role = UserRole.Admin;
            user.UpdatedAt = DateTime.UtcNow;

            // VIP üyeliğini kaldır
            if (user.VipExpiryDate.HasValue)
            {
                user.VipExpiryDate = null;
            }

            await _context.SaveChangesAsync();

            // Audit log kaydı
            var superAdmin = await _context.Users.FindAsync(superAdminId);
            var logMessage = $"SuperAdmin '{superAdmin?.FirstName} {superAdmin?.LastName}' kullanıcı '{user.FirstName} {user.LastName}' (ID: {user.Id}) için admin yetkisi verdi. Önceki rol: {oldRole}, Sebep: {dto.Reason}";

            return Ok(new
            {
                message = $"Kullanıcıya admin yetkisi verildi. {oldRole} -> Admin",
                user = new
                {
                    id = user.Id,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    role = user.Role.ToString(),
                    updatedAt = user.UpdatedAt
                },
                reason = dto.Reason,
                note = dto.Note
            });
        }

        [HttpPost("users/{id}/revoke-admin")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> RevokeAdminRole(int id, RevokeAdminDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound("Kullanıcı bulunamadı");

            var superAdminId = User.GetCurrentUserId()!.Value;
            if (user.Id == superAdminId)
                return BadRequest("Kendi yetkilerinizi değiştiremezsiniz");

            if (user.Role == UserRole.SuperAdmin)
                return BadRequest("SuperAdmin kullanıcılarının rolü değiştirilemez");

            if (user.Role != UserRole.Admin)
                return BadRequest("Kullanıcı zaten Admin rolünde değil");

            var oldRole = user.Role;
            user.Role = dto.ConvertToNormalUser ? UserRole.NormalUser : UserRole.VipUser;
            user.UpdatedAt = DateTime.UtcNow;

            // VIP kullanıcıya çeviriyorsa VIP tarihini ayarla
            if (!dto.ConvertToNormalUser)
            {
                user.VipExpiryDate = DateTime.UtcNow.AddMonths(1); // 1 ay VIP
            }

            await _context.SaveChangesAsync();

            // Audit log kaydı
            var superAdmin = await _context.Users.FindAsync(superAdminId);
            var logMessage = $"SuperAdmin '{superAdmin?.FirstName} {superAdmin?.LastName}' kullanıcı '{user.FirstName} {user.LastName}' (ID: {user.Id}) için admin yetkisini kaldırdı. Yeni rol: {user.Role}, Sebep: {dto.Reason}";

            return Ok(new
            {
                message = $"Kullanıcının admin yetkisi kaldırıldı. {oldRole} -> {user.Role}",
                user = new
                {
                    id = user.Id,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    role = user.Role.ToString(),
                    updatedAt = user.UpdatedAt,
                    vipExpiryDate = user.VipExpiryDate
                },
                reason = dto.Reason,
                note = dto.Note
            });
        }

        [HttpPost("users/{id}/change-role-super")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ChangeUserRoleSuper(int id, RoleChangeDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound("Kullanıcı bulunamadı");

            var superAdminId = User.GetCurrentUserId()!.Value;
            if (user.Id == superAdminId)
                return BadRequest("Kendi yetkilerinizi değiştiremezsiniz");

            if (user.Role == UserRole.SuperAdmin)
                return BadRequest("SuperAdmin kullanıcılarının rolü değiştirilemez");

            if (!Enum.IsDefined(typeof(UserRole), dto.NewRole))
                return BadRequest("Geçersiz rol değeri");

            var newRole = (UserRole)dto.NewRole;

            // SuperAdmin rolüne sadece başka SuperAdmin atayabilir (güvenlik)
            if (newRole == UserRole.SuperAdmin)
                return BadRequest("SuperAdmin rolü bu endpoint ile verilemez");

            var oldRole = user.Role;
            user.Role = newRole;
            user.UpdatedAt = DateTime.UtcNow;

            // Rol değişimlerine göre ayarlamalar
            if (newRole == UserRole.NormalUser)
            {
                user.VipExpiryDate = null;
            }
            else if (newRole == UserRole.VipUser && oldRole != UserRole.VipUser)
            {
                user.VipExpiryDate = DateTime.UtcNow.AddMonths(1); // 1 ay VIP
            }

            await _context.SaveChangesAsync();

            // Audit log kaydı
            var superAdmin = await _context.Users.FindAsync(superAdminId);
            var logMessage = $"SuperAdmin '{superAdmin?.FirstName} {superAdmin?.LastName}' kullanıcı '{user.FirstName} {user.LastName}' (ID: {user.Id}) rolünü değiştirdi. {oldRole} -> {newRole}, Sebep: {dto.Reason}";

            return Ok(new
            {
                message = $"Kullanıcı rolü değiştirildi. {oldRole} -> {newRole}",
                user = new
                {
                    id = user.Id,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    role = user.Role.ToString(),
                    updatedAt = user.UpdatedAt,
                    vipExpiryDate = user.VipExpiryDate
                },
                reason = dto.Reason
            });
        }

        [HttpGet("admins")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetAllAdmins()
        {
            var admins = await _context.Users
                .Where(u => u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin)
                .OrderBy(u => u.Role)
                .ThenBy(u => u.FirstName)
                .Select(u => new AdminListDto
                {
                    UserId = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Role = u.Role.ToString(),
                    CreatedAt = u.CreatedAt,
                    LastLoginDate = u.LastLoginDate,
                    IsActive = u.IsActive,
                    IsBlocked = u.IsBlocked,
                    ProfileImageUrl = u.ProfileImageUrl
                })
                .ToListAsync();

            return Ok(new
            {
                admins,
                totalCount = admins.Count,
                superAdminCount = admins.Count(a => a.Role == "SuperAdmin"),
                adminCount = admins.Count(a => a.Role == "Admin")
            });
        }

        [HttpGet("role-history/{userId}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetUserRoleHistory(int userId)
        {
            var roleHistory = await _context.AuditLogs
                .Where(a => a.UserId == userId &&
                           (a.Action.Contains("GrantAdmin") ||
                            a.Action.Contains("RevokeAdmin") ||
                            a.Action.Contains("ChangeRole")))
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Action,
                    a.CreatedAt,
                    a.UserName,
                    a.IpAddress,
                    a.UserAgent
                })
                .ToListAsync();

            return Ok(roleHistory);
        }

        // Şifre sıfırlama talepleri
        [HttpGet("password-reset-requests")]
        public async Task<IActionResult> GetPasswordResetRequests(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool? isApproved = null,
            [FromQuery] bool? isCompleted = null)
        {
            var query = _context.PasswordResetRequests
                .Include(r => r.ApprovedByUser)
                .AsQueryable();

            if (isApproved.HasValue)
                query = query.Where(r => r.IsApproved == isApproved.Value);

            if (isCompleted.HasValue)
                query = query.Where(r => r.IsCompleted == isCompleted.Value);

            var totalCount = await query.CountAsync();
            var requests = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.Email,
                    r.UserName,
                    r.Reason,
                    r.CreatedAt,
                    r.IsApproved,
                    r.ApprovedAt,
                    r.IsCompleted,
                    r.CompletedAt,
                    ApprovedBy = r.ApprovedByUser != null ? new
                    {
                        Id = r.ApprovedByUser.Id,
                        Name = $"{r.ApprovedByUser.FirstName} {r.ApprovedByUser.LastName}"
                    } : null
                })
                .ToListAsync();

            return Ok(new
            {
                requests,
                totalCount,
                currentPage = page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        [HttpPost("password-reset-requests/{id}/approve")]
        public async Task<IActionResult> ApprovePasswordResetRequest(int id)
        {
            var request = await _context.PasswordResetRequests.FindAsync(id);
            if (request == null)
                return NotFound("Şifre sıfırlama talebi bulunamadı");

            if (request.IsApproved)
                return BadRequest("Bu talep zaten onaylanmış");

            if (request.IsCompleted)
                return BadRequest("Bu talep zaten tamamlanmış");

            var adminUserId = User.GetCurrentUserId()!.Value;

            // Reset token oluştur
            request.ResetToken = Guid.NewGuid().ToString();
            request.ResetTokenExpiry = DateTime.UtcNow.AddHours(24);
            request.IsApproved = true;
            request.ApprovedAt = DateTime.UtcNow;
            request.ApprovedByUserId = adminUserId;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Şifre sıfırlama talebi onaylandı",
                resetToken = request.ResetToken,
                expiryDate = request.ResetTokenExpiry
            });
        }

        [HttpPost("password-reset-requests/{id}/reject")]
        public async Task<IActionResult> RejectPasswordResetRequest(int id, [FromBody] string reason)
        {
            var request = await _context.PasswordResetRequests.FindAsync(id);
            if (request == null)
                return NotFound("Şifre sıfırlama talebi bulunamadı");

            if (request.IsApproved)
                return BadRequest("Bu talep zaten onaylanmış");

            if (request.IsCompleted)
                return BadRequest("Bu talep zaten tamamlanmış");

            var adminUserId = User.GetCurrentUserId()!.Value;

            request.IsApproved = false;
            request.IsCompleted = true;
            request.CompletedAt = DateTime.UtcNow;
            request.ApprovedByUserId = adminUserId;
            request.Reason = $"Reddedildi: {reason}";

            await _context.SaveChangesAsync();

            return Ok(new { message = "Şifre sıfırlama talebi reddedildi" });
        }

        [HttpPost("password-reset-requests/{id}/complete")]
        public async Task<IActionResult> CompletePasswordResetRequest(int id)
        {
            var request = await _context.PasswordResetRequests.FindAsync(id);
            if (request == null)
                return NotFound("Şifre sıfırlama talebi bulunamadı");

            if (!request.IsApproved)
                return BadRequest("Bu talep henüz onaylanmamış");

            if (request.IsCompleted)
                return BadRequest("Bu talep zaten tamamlanmış");

            request.IsCompleted = true;
            request.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Şifre sıfırlama talebi tamamlandı" });
        }
    }
}