using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VurduGololdu.API.Data;
using VurduGololdu.API.DTOs;
using VurduGololdu.API.Models;
using VurduGololdu.API.Services;
using VurduGololdu.API.Helpers;

namespace VurduGololdu.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly IConfiguration _configuration;
        private readonly IAuditLogService _auditLogService;
        private readonly INotificationService _notificationService;
        private readonly ICaptchaService _captchaService;

        public AuthController(ApplicationDbContext context, IJwtService jwtService, IConfiguration configuration, IAuditLogService auditLogService, INotificationService notificationService, ICaptchaService captchaService)
        {
            _context = context;
            _jwtService = jwtService;
            _configuration = configuration;
            _auditLogService = auditLogService;
            _notificationService = notificationService;
            _captchaService = captchaService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            // 🤖 Captcha doğrulaması
            var isCaptchaValid = await _captchaService.VerifyCaptchaAsync(dto.CaptchaSessionId, dto.CaptchaCode);
            if (!isCaptchaValid)
            {
                await _auditLogService.LogAsync("RegisterCaptchaFailed", "User", null,
                    new { email = dto.Email, sessionId = dto.CaptchaSessionId }, AuditLogLevel.Warning);
                return BadRequest("Captcha doğrulaması başarısız. Lütfen tekrar deneyin.");
            }

            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return BadRequest("Bu email adresi zaten kullanılıyor");
            }

            var user = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Phone = dto.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = UserRole.NormalUser,
                IsEmailVerified = true, // Email doğrulama kaldırıldı - direkt aktif
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Audit log
            await _auditLogService.LogUserActionAsync(user.Id, "Register", "User", user.Id, new { email = dto.Email });

            // Sadece hoş geldin bildirimi gönder
            await _notificationService.SendWelcomeNotificationAsync(user.Id);

            var userDto = new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role.ToString(),
                IsEmailVerified = user.IsEmailVerified,
                CreatedAt = user.CreatedAt,
                VipExpiryDate = user.VipExpiryDate,
                IsVipActive = user.VipExpiryDate.HasValue && user.VipExpiryDate > DateTime.UtcNow,
                IsBlocked = user.IsBlocked
            };

            return Ok(new { message = "Kullanıcı başarıyla oluşturuldu. Hesabınız aktif.", user = userDto });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive);

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                // Update failed login attempts if user exists
                if (user != null)
                {
                    user.FailedLoginAttempts++;
                    user.LastFailedLoginDate = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                // Failed login attempt log
                await _auditLogService.LogAsync("LoginFailed", "User", null, new { email = dto.Email },
                    level: AuditLogLevel.Warning);
                return Unauthorized("Geçersiz email veya şifre");
            }

            if (user.IsBlocked)
            {
                // Blocked user login attempt log
                await _auditLogService.LogUserActionAsync(user.Id, "LoginBlocked", "User", user.Id,
                    new { email = dto.Email, blockReason = user.BlockReason }, AuditLogLevel.Warning);
                return Forbid($"Hesabınız engellenmiştir. Sebep: {user.BlockReason}");
            }

            var token = _jwtService.GenerateToken(user);
            var refreshToken = _jwtService.GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(int.Parse(_configuration["Jwt:RefreshTokenExpireDays"]!));
            user.LastLoginDate = DateTime.UtcNow;
            user.FailedLoginAttempts = 0; // Reset failed attempts on successful login
            await _context.SaveChangesAsync();

            // Successful login log
            await _auditLogService.LogUserActionAsync(user.Id, "Login", "User", user.Id, new { email = dto.Email });

            var userDto = new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role.ToString(),
                IsEmailVerified = user.IsEmailVerified,
                CreatedAt = user.CreatedAt,
                VipExpiryDate = user.VipExpiryDate,
                IsVipActive = user.VipExpiryDate.HasValue && user.VipExpiryDate > DateTime.UtcNow,
                IsBlocked = user.IsBlocked,
                ProfileImageUrl = user.ProfileImageUrl
            };

            return Ok(new AuthResponseDto
            {
                Token = token,
                RefreshToken = refreshToken,
                TokenExpiry = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["Jwt:ExpireMinutes"]!)),
                User = userDto
            });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken(RefreshTokenDto dto)
        {
            var principal = _jwtService.GetPrincipalFromExpiredToken(dto.Token);
            if (principal == null)
            {
                return BadRequest("Geçersiz token");
            }

            var userId = int.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || user.RefreshToken != dto.RefreshToken || user.RefreshTokenExpiry <= DateTime.UtcNow)
            {
                return BadRequest("Geçersiz refresh token");
            }

            var newToken = _jwtService.GenerateToken(user);
            var newRefreshToken = _jwtService.GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(int.Parse(_configuration["Jwt:RefreshTokenExpireDays"]!));
            await _context.SaveChangesAsync();

            var userDto = new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role.ToString(),
                IsEmailVerified = user.IsEmailVerified,
                CreatedAt = user.CreatedAt,
                VipExpiryDate = user.VipExpiryDate,
                IsVipActive = user.VipExpiryDate.HasValue && user.VipExpiryDate > DateTime.UtcNow,
                IsBlocked = user.IsBlocked,
                ProfileImageUrl = user.ProfileImageUrl
            };

            return Ok(new AuthResponseDto
            {
                Token = newToken,
                RefreshToken = newRefreshToken,
                TokenExpiry = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["Jwt:ExpireMinutes"]!)),
                User = userDto
            });
        }

        // Email doğrulama endpoint'i kaldırıldı - artık gerekli değil

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive);

            if (user == null)
            {
                // Güvenlik nedeniyle her zaman başarılı mesajı döndür
                return Ok(new { message = "Şifre sıfırlama talebiniz alındı. Admin onayından sonra size bilgi verilecek." });
            }

            // Mevcut bekleyen talep var mı kontrol et
            var existingRequest = await _context.PasswordResetRequests
                .FirstOrDefaultAsync(r => r.Email == dto.Email && !r.IsCompleted);

            if (existingRequest != null)
            {
                return BadRequest("Bu email için zaten bekleyen bir şifre sıfırlama talebi var.");
            }

            // Yeni şifre sıfırlama talebi oluştur
            var resetRequest = new PasswordResetRequest
            {
                Email = dto.Email,
                UserName = $"{user.FirstName} {user.LastName}",
                Reason = "Kullanıcı tarafından talep edildi",
                CreatedAt = DateTime.UtcNow
            };

            _context.PasswordResetRequests.Add(resetRequest);
            await _context.SaveChangesAsync();

            // Audit log
            await _auditLogService.LogAsync("PasswordResetRequested", "User", user.Id,
                new { email = dto.Email, requestId = resetRequest.Id });

            return Ok(new { message = "Şifre sıfırlama talebiniz alındı. Admin onayından sonra size bilgi verilecek." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            // Admin onaylı şifre sıfırlama talebini bul
            var resetRequest = await _context.PasswordResetRequests
                .FirstOrDefaultAsync(r => r.ResetToken == dto.Token &&
                                         r.ResetTokenExpiry > DateTime.UtcNow &&
                                         r.IsApproved &&
                                         !r.IsCompleted);

            if (resetRequest == null)
            {
                return BadRequest("Geçersiz veya süresi dolmuş şifre sıfırlama kodu");
            }

            // Kullanıcıyı bul
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == resetRequest.Email && u.IsActive);

            if (user == null)
            {
                return BadRequest("Kullanıcı bulunamadı");
            }

            // Şifreyi güncelle
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            // Reset request'i tamamla
            resetRequest.IsCompleted = true;
            resetRequest.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Audit log
            await _auditLogService.LogUserActionAsync(user.Id, "PasswordReset", "User", user.Id,
                new { requestId = resetRequest.Id });

            return Ok(new { message = "Şifre başarıyla güncellendi" });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("Geçersiz kullanıcı token'ı");
            }

            var user = await _context.Users.FindAsync(userId);

            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiry = null;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Başarıyla çıkış yapıldı" });
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            // Debug: Tüm claims'leri logla
            DebugConsole.Log("🔍 JWT Claims Debug:");
            foreach (var claim in User.Claims)
            {
                DebugConsole.Log($"   {claim.Type}: {claim.Value}");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            DebugConsole.Log($"🔍 User ID Claim: {userIdClaim}");

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                DebugConsole.Log("🚨 Invalid user token in GetProfile");
                return Unauthorized("Geçersiz kullanıcı token'ı");
            }

            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound("Kullanıcı bulunamadı");
            }

            var userDto = new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role.ToString(),
                IsEmailVerified = user.IsEmailVerified,
                CreatedAt = user.CreatedAt,
                VipExpiryDate = user.VipExpiryDate,
                IsVipActive = user.VipExpiryDate.HasValue && user.VipExpiryDate > DateTime.UtcNow,
                IsBlocked = user.IsBlocked,
                ProfileImageUrl = user.ProfileImageUrl
            };

            return Ok(userDto);
        }

        [HttpGet("test-auth")]
        [Authorize]
        public IActionResult TestAuth()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value;

            return Ok(new
            {
                message = "Authentication başarılı!",
                userId = userIdClaim,
                email = userEmail,
                name = userName,
                allClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
            });
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return Unauthorized("Kullanıcı bulunamadı");
            }

            // Mevcut şifre doğrulama
            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            {
                await _auditLogService.LogUserActionAsync(user.Id, "ChangePasswordFailed", "User", user.Id,
                    new { reason = "CurrentPasswordMismatch" }, AuditLogLevel.Warning);
                return BadRequest("Mevcut şifre hatalı");
            }

            // Yeni şifre mevcut şifreyle aynı olmamalı
            if (BCrypt.Net.BCrypt.Verify(dto.NewPassword, user.PasswordHash))
            {
                return BadRequest("Yeni şifre mevcut şifreyle aynı olamaz");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();

            await _auditLogService.LogUserActionAsync(user.Id, "ChangePassword", "User", user.Id, null);

            // Kullanıcıya bilgilendirme email'i gönder
            await _notificationService.SendPasswordChangedNotificationAsync(user.Id);

            return Ok(new { message = "Şifre başarıyla değiştirildi" });
        }

        [HttpPost("check-email")]
        public async Task<IActionResult> CheckEmailAvailability([FromBody] CheckEmailDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Email))
                {
                    return BadRequest(new { message = "Email adresi boş olamaz" });
                }

                // Email formatını kontrol et
                if (!IsValidEmail(dto.Email))
                {
                    return BadRequest(new { message = "Geçersiz email formatı" });
                }

                // Email'in sistemde var olup olmadığını kontrol et
                var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower());

                return Ok(new
                {
                    email = dto.Email,
                    isAvailable = !emailExists,
                    message = emailExists ? "Bu email adresi zaten kullanılıyor" : "Email adresi kullanılabilir"
                });
            }
            catch (Exception ex)
            {
                await _auditLogService.LogAsync("ERROR", "Auth", null, null, null,
                    AuditLogLevel.Error, $"CheckEmailAvailability Error: {ex.Message}");
                return StatusCode(500, new { message = "Email kontrolü sırasında hata oluştu" });
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}