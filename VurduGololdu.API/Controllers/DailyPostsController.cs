using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VurduGololdu.API.Data;
using VurduGololdu.API.DTOs;
using VurduGololdu.API.Models;
using VurduGololdu.API.Services;

namespace VurduGololdu.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DailyPostsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IS3Service _s3Service;
        private readonly IAuditLogService _auditLogService;
        private readonly INotificationService _notificationService;

        public DailyPostsController(
            ApplicationDbContext context,
            IS3Service s3Service,
            IAuditLogService auditLogService,
            INotificationService notificationService)
        {
            _context = context;
            _s3Service = s3Service;
            _auditLogService = auditLogService;
            _notificationService = notificationService;
        }

        // 📋 Tüm günlük paylaşımları getir (herkese açık)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DailyPostSummaryDto>>> GetDailyPosts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? category = null,
            [FromQuery] bool featuredOnly = false)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var currentUserId = userId != null ? int.Parse(userId) : (int?)null;

                var query = _context.DailyPosts
                    .AsNoTracking()
                    .Include(p => p.Admin)
                    .Where(p => p.IsPublished)
                    .AsQueryable();

                // Kategori filtresi
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(p => p.Category == category);
                }

                // Öne çıkarılmış filtresi
                if (featuredOnly)
                {
                    query = query.Where(p => p.IsFeatured);
                }

                var totalCount = await query.CountAsync();

                var posts = await query
                    .OrderByDescending(p => p.IsFeatured)
                    .ThenByDescending(p => p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Kullanıcının beğendiği postları al
                var postIds = posts.Select(p => p.Id).ToList();

                List<int> userLikes;
                if (currentUserId.HasValue)
                {
                    var uid = currentUserId.Value;
                    userLikes = await _context.Likes
                        .Where(l => l.UserId == uid && l.DailyPostId.HasValue && postIds.Contains(l.DailyPostId.Value))
                        .Select(l => l.DailyPostId!.Value)
                        .ToListAsync();
                }
                else
                {
                    userLikes = new List<int>();
                }

                var result = posts.Select(p => new DailyPostSummaryDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Content = p.Content,
                    ImageUrl = p.ImageUrl,
                    Category = p.Category,
                    IsFeatured = p.IsFeatured,
                    CreatedAt = p.CreatedAt,
                    AdminName = p.Admin.FirstName + " " + p.Admin.LastName,
                    AdminProfileImageUrl = p.Admin.ProfileImageUrl,
                    ViewCount = p.ViewCount,
                    LikeCount = p.LikeCount,
                    CommentCount = p.CommentCount,
                    IsLikedByCurrentUser = userLikes.Contains(p.Id)
                }).ToList();

                Response.Headers["X-Total-Count"] = totalCount.ToString();
                Response.Headers["X-Page"] = page.ToString();
                Response.Headers["X-Page-Size"] = pageSize.ToString();

                return Ok(result);
            }
            catch (Exception ex)
            {
                await _auditLogService.LogAsync("ERROR", "DailyPosts", null, null, null,
                    AuditLogLevel.Error, $"GetDailyPosts Error: {ex.Message}");
                return StatusCode(500, new { message = "Günlük paylaşımlar yüklenirken hata oluştu" });
            }
        }

        // 🔍 Tek günlük paylaşım detayı getir
        [HttpGet("{id}")]
        public async Task<ActionResult<DailyPostDto>> GetDailyPost(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var currentUserId = userId != null ? int.Parse(userId) : (int?)null;

                var post = await _context.DailyPosts
                    .Include(p => p.Admin)
                    .Include(p => p.Likes)
                    .Include(p => p.Comments)
                    .FirstOrDefaultAsync(p => p.Id == id && p.IsPublished);

                if (post == null)
                {
                    return NotFound(new { message = "Paylaşım bulunamadı" });
                }

                // Görüntülenme sayısını artır
                post.ViewCount++;
                await _context.SaveChangesAsync();

                var result = new DailyPostDto
                {
                    Id = post.Id,
                    Title = post.Title,
                    Content = post.Content,
                    ImageUrl = post.ImageUrl,
                    Category = post.Category,
                    Tags = post.Tags,
                    IsPublished = post.IsPublished,
                    IsFeatured = post.IsFeatured,
                    CreatedAt = post.CreatedAt,
                    UpdatedAt = post.UpdatedAt,
                    AdminId = post.AdminId,
                    AdminName = post.Admin.FirstName + " " + post.Admin.LastName,
                    AdminProfileImageUrl = post.Admin.ProfileImageUrl,
                    ViewCount = post.ViewCount,
                    LikeCount = post.LikeCount,
                    CommentCount = post.CommentCount,
                    IsLikedByCurrentUser = currentUserId.HasValue && post.Likes != null &&
                        post.Likes.Any(l => l.UserId == currentUserId.Value && l.DailyPostId == post.Id)
                };

                await _auditLogService.LogAsync("VIEW", "DailyPost", post.Id,
                    null, null, AuditLogLevel.Info, null);

                return Ok(result);
            }
            catch (Exception ex)
            {
                await _auditLogService.LogAsync("ERROR", "DailyPosts", null, null, null,
                    AuditLogLevel.Error, $"GetDailyPost Error: {ex.Message}");
                return StatusCode(500, new { message = "Paylaşım yüklenirken hata oluştu" });
            }
        }

        // ➕ Yeni günlük paylaşım oluştur (Sadece Admin)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<DailyPostDto>> CreateDailyPost([FromForm] CreateDailyPostDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                string? imageUrl = null;
                if (dto.Image != null)
                {
                    imageUrl = await _s3Service.UploadFileAsync(dto.Image, "daily-posts");
                }

                var post = new DailyPost
                {
                    Title = dto.Title,
                    Content = dto.Content,
                    ImageUrl = imageUrl,
                    Category = dto.Category,
                    Tags = dto.Tags,
                    IsPublished = dto.IsPublished,
                    IsFeatured = dto.IsFeatured,
                    AdminId = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.DailyPosts.Add(post);
                await _context.SaveChangesAsync();

                // Admin bilgilerini yükle
                await _context.Entry(post)
                    .Reference(p => p.Admin)
                    .LoadAsync();

                var result = new DailyPostDto
                {
                    Id = post.Id,
                    Title = post.Title,
                    Content = post.Content,
                    ImageUrl = post.ImageUrl,
                    Category = post.Category,
                    Tags = post.Tags,
                    IsPublished = post.IsPublished,
                    IsFeatured = post.IsFeatured,
                    CreatedAt = post.CreatedAt,
                    AdminId = post.AdminId,
                    AdminName = post.Admin.FirstName + " " + post.Admin.LastName,
                    ViewCount = post.ViewCount,
                    LikeCount = post.LikeCount,
                    CommentCount = post.CommentCount
                };

                await _auditLogService.LogAsync("CREATE", "DailyPost", post.Id,
                    dto, null, AuditLogLevel.Info, null);

                // Send new daily post notification (sadece admin paylaşım yaptığında)
                var adminUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _notificationService.SendNewDailyPostNotificationAsync(post.Title, post.Id, adminUserId);

                return CreatedAtAction(nameof(GetDailyPost), new { id = post.Id }, result);
            }
            catch (Exception ex)
            {
                await _auditLogService.LogAsync("ERROR", "DailyPosts", null, null, null,
                    AuditLogLevel.Error, $"CreateDailyPost Error: {ex.Message}");
                return StatusCode(500, new { message = "Paylaşım oluşturulurken hata oluştu" });
            }
        }

        // ✏️ Günlük paylaşım güncelle (Sadece Admin)
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<DailyPostDto>> UpdateDailyPost(int id, [FromForm] UpdateDailyPostDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                var post = await _context.DailyPosts
                    .Include(p => p.Admin)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (post == null)
                {
                    return NotFound(new { message = "Paylaşım bulunamadı" });
                }

                // Resim güncelleme
                if (dto.Image != null)
                {
                    post.ImageUrl = await _s3Service.UploadFileAsync(dto.Image, "daily-posts");
                }

                post.Title = dto.Title;
                post.Content = dto.Content;
                post.Category = dto.Category;
                post.Tags = dto.Tags;
                post.IsPublished = dto.IsPublished;
                post.IsFeatured = dto.IsFeatured;
                post.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var result = new DailyPostDto
                {
                    Id = post.Id,
                    Title = post.Title,
                    Content = post.Content,
                    ImageUrl = post.ImageUrl,
                    Category = post.Category,
                    Tags = post.Tags,
                    IsPublished = post.IsPublished,
                    IsFeatured = post.IsFeatured,
                    CreatedAt = post.CreatedAt,
                    UpdatedAt = post.UpdatedAt,
                    AdminId = post.AdminId,
                    AdminName = post.Admin.FirstName + " " + post.Admin.LastName,
                    ViewCount = post.ViewCount,
                    LikeCount = post.LikeCount,
                    CommentCount = post.CommentCount
                };

                await _auditLogService.LogAsync("UPDATE", "DailyPost", post.Id,
                    dto, null, AuditLogLevel.Info, null);

                return Ok(result);
            }
            catch (Exception ex)
            {
                await _auditLogService.LogAsync("ERROR", "DailyPosts", null, null, null,
                    AuditLogLevel.Error, $"UpdateDailyPost Error: {ex.Message}");
                return StatusCode(500, new { message = "Paylaşım güncellenirken hata oluştu" });
            }
        }

        // 🗑️ Günlük paylaşım sil (Sadece Admin)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult> DeleteDailyPost(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                var post = await _context.DailyPosts
                    .Include(p => p.Likes)
                    .Include(p => p.Comments)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (post == null)
                {
                    return NotFound(new { message = "Paylaşım bulunamadı" });
                }

                // Önce bağlı Like kayıtlarını sil
                if (post.Likes != null && post.Likes.Any())
                {
                    _context.Likes.RemoveRange(post.Likes);
                }

                // Sonra bağlı Comment kayıtlarını sil
                if (post.Comments != null && post.Comments.Any())
                {
                    // Comment'lerin de Like'larını sil
                    var commentIds = post.Comments.Select(c => c.Id).ToList();
                    var commentLikes = await _context.Likes
                        .Where(l => l.CommentId.HasValue && commentIds.Contains(l.CommentId.Value))
                        .ToListAsync();

                    if (commentLikes.Any())
                    {
                        _context.Likes.RemoveRange(commentLikes);
                    }

                    _context.Comments.RemoveRange(post.Comments);
                }

                // S3'ten resmi sil (eğer varsa)
                if (!string.IsNullOrEmpty(post.ImageUrl))
                {
                    try
                    {
                        await _s3Service.DeleteFileAsync(post.ImageUrl);
                    }
                    catch (Exception s3Ex)
                    {
                        // S3 silme hatası logla ama işlemi durdurma
                        await _auditLogService.LogAsync("WARNING", "DailyPost", post.Id,
                            null, null, AuditLogLevel.Warning, $"S3 resim silme hatası: {s3Ex.Message}");
                    }
                }

                // Son olarak DailyPost'u sil
                _context.DailyPosts.Remove(post);
                await _context.SaveChangesAsync();

                var likeCountDeleted = post.Likes?.Count ?? 0;
                var commentCountDeleted = post.Comments?.Count ?? 0;

                await _auditLogService.LogAsync("DELETE", "DailyPost", post.Id,
                    null, null, AuditLogLevel.Info,
                    $"DailyPost silindi. {likeCountDeleted} beğeni, {commentCountDeleted} yorum ile birlikte.");

                return Ok(new
                {
                    message = "Paylaşım başarıyla silindi",
                    deletedLikes = likeCountDeleted,
                    deletedComments = commentCountDeleted
                });
            }
            catch (Exception ex)
            {
                await _auditLogService.LogAsync("ERROR", "DailyPosts", null, null, null,
                    AuditLogLevel.Error, $"DeleteDailyPost Error: {ex.Message}");
                return StatusCode(500, new
                {
                    message = "Paylaşım silinirken hata oluştu",
                    error = ex.Message
                });
            }
        }

        // 👍 Günlük paylaşımı beğen/beğenmekten vazgeç
        [HttpPost("{id}/like")]
        [Authorize]
        public async Task<ActionResult> ToggleLikeDailyPost(int id, [FromBody] LikeDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                var post = await _context.DailyPosts.FindAsync(id);
                if (post == null)
                {
                    return NotFound(new { message = "Paylaşım bulunamadı" });
                }

                var existingLike = await _context.Likes
                    .FirstOrDefaultAsync(l => l.UserId == userId && l.DailyPostId == id);

                if (existingLike != null)
                {
                    if (existingLike.Type == (LikeType)dto.Type)
                    {
                        // Aynı tip beğeni - kaldır
                        _context.Likes.Remove(existingLike);
                        post.LikeCount--;
                    }
                    else
                    {
                        // Farklı tip beğeni - güncelle
                        existingLike.Type = (LikeType)dto.Type;
                        existingLike.CreatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    // Yeni beğeni ekle
                    var like = new Like
                    {
                        UserId = userId,
                        DailyPostId = id,
                        Type = (LikeType)dto.Type,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Likes.Add(like);
                    post.LikeCount++;
                }

                await _context.SaveChangesAsync();

                await _auditLogService.LogAsync("LIKE", "DailyPost", post.Id,
                    dto, null, AuditLogLevel.Info, null);

                return Ok(new
                {
                    message = "Beğeni durumu güncellendi",
                    likeCount = post.LikeCount
                });
            }
            catch (Exception ex)
            {
                await _auditLogService.LogAsync("ERROR", "DailyPosts", null, null, null,
                    AuditLogLevel.Error, $"ToggleLikeDailyPost Error: {ex.Message}");
                return StatusCode(500, new { message = "Beğeni durumu güncellenirken hata oluştu" });
            }
        }

        // 📊 Admin için günlük paylaşım istatistikleri
        [HttpGet("admin/stats")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<DailyPostStatsDto>> GetDailyPostStats()
        {
            try
            {
                var stats = new DailyPostStatsDto
                {
                    TotalPosts = await _context.DailyPosts.CountAsync(),
                    PublishedPosts = await _context.DailyPosts.CountAsync(p => p.IsPublished),
                    FeaturedPosts = await _context.DailyPosts.CountAsync(p => p.IsFeatured),
                    TotalViews = await _context.DailyPosts.SumAsync(p => p.ViewCount),
                    TotalLikes = await _context.DailyPosts.SumAsync(p => p.LikeCount),
                    TotalComments = await _context.DailyPosts.SumAsync(p => p.CommentCount)
                };

                // Kategori bazlı istatistikler
                stats.CategoryStats = await _context.DailyPosts
                    .Where(p => !string.IsNullOrEmpty(p.Category))
                    .GroupBy(p => p.Category)
                    .Select(g => new CategoryStatsDto
                    {
                        Category = g.Key!,
                        PostCount = g.Count(),
                        TotalViews = g.Sum(p => p.ViewCount),
                        TotalLikes = g.Sum(p => p.LikeCount)
                    })
                    .OrderByDescending(c => c.PostCount)
                    .ToListAsync();

                return Ok(stats);
            }
            catch (Exception ex)
            {
                await _auditLogService.LogAsync("ERROR", "DailyPosts", null, null, null,
                    AuditLogLevel.Error, $"GetDailyPostStats Error: {ex.Message}");
                return StatusCode(500, new { message = "İstatistikler yüklenirken hata oluştu" });
            }
        }

        // 👥 Postu beğenen kullanıcıları getir
        [HttpGet("{id}/likes")]
        public async Task<ActionResult<PostLikersDto>> GetDailyPostLikes(int id)
        {
            try
            {
                var post = await _context.DailyPosts
                    .FirstOrDefaultAsync(p => p.Id == id && p.IsPublished);

                if (post == null)
                {
                    return NotFound(new { message = "Paylaşım bulunamadı" });
                }

                var likes = await _context.Likes
                    .Include(l => l.User)
                    .Where(l => l.DailyPostId == id)
                    .OrderByDescending(l => l.CreatedAt)
                    .ToListAsync();

                var likers = likes.Select(l => new PostLikerDto
                {
                    UserId = l.UserId,
                    UserName = l.User.FirstName + " " + l.User.LastName,
                    ProfileImageUrl = l.User.ProfileImageUrl,
                    LikeType = (int)l.Type,
                    LikeTypeName = l.Type.ToString(),
                    LikedAt = l.CreatedAt
                }).ToList();

                var result = new PostLikersDto
                {
                    PostId = post.Id,
                    PostTitle = post.Title,
                    TotalLikes = likes.Count,
                    Likers = likers,
                    LikeCount = likes.Count(l => l.Type == LikeType.Like),
                    LoveCount = likes.Count(l => l.Type == LikeType.Love),
                    LaughCount = likes.Count(l => l.Type == LikeType.Laugh),
                    AngryCount = likes.Count(l => l.Type == LikeType.Angry),
                    SadCount = likes.Count(l => l.Type == LikeType.Sad),
                    WowCount = likes.Count(l => l.Type == LikeType.Wow)
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                await _auditLogService.LogAsync("ERROR", "DailyPosts", null, null, null,
                    AuditLogLevel.Error, $"GetDailyPostLikes Error: {ex.Message}");
                return StatusCode(500, new { message = "Beğenenler yüklenirken hata oluştu" });
            }
        }

        // 🏷️ Kategorileri getir
        [HttpGet("categories")]
        public async Task<ActionResult<IEnumerable<string>>> GetCategories()
        {
            try
            {
                var categories = await _context.DailyPosts
                    .Where(p => !string.IsNullOrEmpty(p.Category) && p.IsPublished)
                    .Select(p => p.Category!)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                await _auditLogService.LogAsync("ERROR", "DailyPosts", null, null, null,
                    AuditLogLevel.Error, $"GetCategories Error: {ex.Message}");
                return StatusCode(500, new { message = "Kategoriler yüklenirken hata oluştu" });
            }
        }
    }
}