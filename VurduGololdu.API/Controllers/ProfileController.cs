using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using VurduGololdu.API.Data;
using VurduGololdu.API.DTOs;
using VurduGololdu.API.Services;
using VurduGololdu.API.Helpers;

namespace VurduGololdu.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProfileController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IS3Service _s3Service;

        public ProfileController(ApplicationDbContext context, IS3Service s3Service)
        {
            _context = context;
            _s3Service = s3Service;
        }

        [HttpPost("upload-profile-image")]
        public async Task<IActionResult> UploadProfileImage(IFormFile file)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("Geçersiz kullanıcı token'ı");
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("Dosya seçilmedi");
            }

            if (!_s3Service.IsValidImageFile(file))
            {
                return BadRequest("Geçersiz dosya formatı. Sadece JPG, PNG, WEBP dosyaları kabul edilir.");
            }

            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound("Kullanıcı bulunamadı");
                }

                // Eski profil resmini sil
                if (!string.IsNullOrEmpty(user.ProfileImageUrl))
                {
                    await _s3Service.DeleteFileAsync(user.ProfileImageUrl);
                }

                // Yeni profil resmini yükle
                var imageUrl = await _s3Service.UploadFileAsync(file, "profile-images");

                user.ProfileImageUrl = imageUrl;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Profil resmi başarıyla yüklendi",
                    profileImageUrl = imageUrl
                });
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Profile image upload error: {ex.Message}");
                return StatusCode(500, "Profil resmi yüklenirken hata oluştu");
            }
        }

        [HttpDelete("delete-profile-image")]
        public async Task<IActionResult> DeleteProfileImage()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("Geçersiz kullanıcı token'ı");
            }

            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound("Kullanıcı bulunamadı");
                }

                if (string.IsNullOrEmpty(user.ProfileImageUrl))
                {
                    return BadRequest("Silinecek profil resmi bulunamadı");
                }

                // S3'ten dosyayı sil
                await _s3Service.DeleteFileAsync(user.ProfileImageUrl);

                // Veritabanından URL'i kaldır
                user.ProfileImageUrl = null;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Profil resmi başarıyla silindi" });
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Profile image delete error: {ex.Message}");
                return StatusCode(500, "Profil resmi silinirken hata oluştu");
            }
        }

        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] ProfileUpdateDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("Geçersiz kullanıcı token'ı");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound("Kullanıcı bulunamadı");
                }

                // Alanları güncelle
                user.FirstName = dto.FirstName;
                user.LastName = dto.LastName;
                user.Phone = dto.Phone; // DTO'dan gelen değeri ata (null olabilir)

                user.UpdatedAt = DateTime.UtcNow;
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

                return Ok(new
                {
                    message = "Profil başarıyla güncellendi",
                    user = userDto
                });
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Profile update error: {ex.Message}");
                return StatusCode(500, "Profil güncellenirken hata oluştu");
            }
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        {
            try
            {
                var query = _context.Users.AsQueryable();

                // Arama filtresi
                if (!string.IsNullOrEmpty(search))
                {
                    search = search.ToLower();
                    query = query.Where(u =>
                        u.FirstName.ToLower().Contains(search) ||
                        u.LastName.ToLower().Contains(search) ||
                        u.Email.ToLower().Contains(search));
                }

                // Toplam kayıt sayısı
                var totalCount = await query.CountAsync();

                // Sayfalama
                var users = await query
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new UserListDto
                    {
                        Id = u.Id,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Email = u.Email,
                        ProfileImageUrl = u.ProfileImageUrl,
                        CreatedAt = u.CreatedAt,
                        IsVipActive = u.VipExpiryDate.HasValue && u.VipExpiryDate > DateTime.UtcNow,
                        IsBlocked = u.IsBlocked
                    })
                    .ToListAsync();

                return Ok(new
                {
                    users = users,
                    pagination = new
                    {
                        page = page,
                        pageSize = pageSize,
                        totalCount = totalCount,
                        totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                    }
                });
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Get users error: {ex.Message}");
                return StatusCode(500, "Kullanıcı listesi alınırken hata oluştu");
            }
        }

        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            try
            {
                var user = await _context.Users
                    .Where(u => u.Id == id)
                    .Select(u => new UserListDto
                    {
                        Id = u.Id,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Email = u.Email,
                        ProfileImageUrl = u.ProfileImageUrl,
                        CreatedAt = u.CreatedAt,
                        IsVipActive = u.VipExpiryDate.HasValue && u.VipExpiryDate > DateTime.UtcNow,
                        IsBlocked = u.IsBlocked
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound("Kullanıcı bulunamadı");
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Get user by id error: {ex.Message}");
                return StatusCode(500, "Kullanıcı bilgileri alınırken hata oluştu");
            }
        }

        [HttpGet("users/bulk")]
        public async Task<IActionResult> GetUsersByIds([FromQuery] string userIds)
        {
            try
            {
                if (string.IsNullOrEmpty(userIds))
                {
                    return BadRequest("userIds parametresi gerekli");
                }

                var userIdList = userIds.Split(',')
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Select(id => int.TryParse(id, out var parsedId) ? parsedId : 0)
                    .Where(id => id > 0)
                    .ToList();

                if (!userIdList.Any())
                {
                    return BadRequest("Geçerli kullanıcı ID'leri gerekli");
                }

                var users = await _context.Users
                    .Where(u => userIdList.Contains(u.Id))
                    .Select(u => new UserListDto
                    {
                        Id = u.Id,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Email = u.Email,
                        ProfileImageUrl = u.ProfileImageUrl,
                        CreatedAt = u.CreatedAt,
                        IsVipActive = u.VipExpiryDate.HasValue && u.VipExpiryDate > DateTime.UtcNow,
                        IsBlocked = u.IsBlocked
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Get users by ids error: {ex.Message}");
                return StatusCode(500, "Kullanıcı bilgileri alınırken hata oluştu");
            }
        }
    }
}