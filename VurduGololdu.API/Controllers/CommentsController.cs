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
    public class CommentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IS3Service _s3Service;
        private readonly INotificationService _notificationService;

        public CommentsController(ApplicationDbContext context, IS3Service s3Service, INotificationService notificationService)
        {
            _context = context;
            _s3Service = s3Service;
            _notificationService = notificationService;
        }

        [HttpGet("prediction/{predictionId}")]
        public async Task<IActionResult> GetCommentsByPrediction(int predictionId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var prediction = await _context.Predictions.FindAsync(predictionId);
            if (prediction == null)
            {
                return NotFound("Tahmin bulunamadı");
            }

            // Ücretli tahmin ise VIP kontrolü
            var currentUserId = GetCurrentUserId();
            if (prediction.IsPaid)
            {
                if (!currentUserId.HasValue || !await IsVipUser(currentUserId.Value))
                {
                    return Forbid("Bu içeriğin yorumlarını görüntülemek için VIP üyelik gerekli");
                }
            }

            var query = _context.Comments
                .AsNoTracking()
                .Include(c => c.User)
                .Where(c => c.PredictionId == predictionId && c.IsApproved && c.IsActive);

            var totalCount = await query.CountAsync();
            var comments = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CommentDto
                {
                    Id = c.Id,
                    Content = c.Content,
                    ImageUrl = c.ImageUrl,
                    CreatedAt = c.CreatedAt,
                    User = new UserDto
                    {
                        Id = c.User.Id,
                        FirstName = c.User.FirstName,
                        LastName = c.User.LastName,
                        Role = c.User.Role.ToString(),
                        CreatedAt = c.User.CreatedAt,
                        ProfileImageUrl = c.User.ProfileImageUrl,
                        IsVipActive = c.User.VipExpiryDate.HasValue && c.User.VipExpiryDate > DateTime.UtcNow,
                        IsBlocked = c.User.IsBlocked
                    },
                    IsLikedByCurrentUser = currentUserId.HasValue && c.Likes != null && c.Likes.Any(l => l.UserId == currentUserId.Value),
                    LikeCount = c.Likes != null ? c.Likes.Count : 0
                })
                .ToListAsync();

            return Ok(new
            {
                comments,
                totalCount,
                currentPage = page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        [HttpPost("prediction/{predictionId}")]
        [Authorize]
        public async Task<IActionResult> CreateComment(int predictionId, [FromForm] CreateCommentDto dto)
        {
            var userId = GetCurrentUserId()!.Value;
            var prediction = await _context.Predictions.FindAsync(predictionId);

            if (prediction == null)
            {
                return NotFound("Tahmin bulunamadı");
            }

            // Ücretli tahmin ise VIP kontrolü
            if (prediction.IsPaid && !await IsVipUser(userId))
            {
                return Forbid("Bu içeriğe yorum yapmak için VIP üyelik gerekli");
            }

            string? imageUrl = null;
            if (dto.Image != null)
            {
                if (!_s3Service.IsValidImageFile(dto.Image))
                {
                    return BadRequest("Geçersiz resim dosyası");
                }

                imageUrl = await _s3Service.UploadFileAsync(dto.Image, "comments");
            }

            var comment = new Comment
            {
                Content = dto.Content,
                ImageUrl = imageUrl,
                UserId = userId,
                PredictionId = predictionId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                IsApproved = false // Admin onayı bekliyor
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Yorum gönderildi. Admin onayından sonra yayınlanacak." });
        }

        [HttpPost("{id}/like")]
        [Authorize]
        public async Task<IActionResult> LikeComment(int id, LikeDto dto)
        {
            var userId = GetCurrentUserId()!.Value;
            var comment = await _context.Comments
                .Include(c => c.Prediction)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null)
            {
                return NotFound("Yorum bulunamadı");
            }

            // Ücretli tahmin ise VIP kontrolü
            if (comment.Prediction != null && comment.Prediction.IsPaid && !await IsVipUser(userId))
            {
                return Forbid("Bu yorumu beğenmek için VIP üyelik gerekli");
            }

            var existingLike = await _context.Likes
                .FirstOrDefaultAsync(l => l.UserId == userId && l.CommentId == id);

            if (existingLike != null)
            {
                // Aynı tip beğeni ise kaldır, farklı ise güncelle
                if (existingLike.Type == (LikeType)dto.Type)
                {
                    _context.Likes.Remove(existingLike);
                }
                else
                {
                    existingLike.Type = (LikeType)dto.Type;
                }
            }
            else
            {
                var like = new Like
                {
                    UserId = userId,
                    CommentId = id,
                    Type = (LikeType)dto.Type,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Likes.Add(like);
            }

            await _context.SaveChangesAsync();
            var likeCount = await _context.Likes.CountAsync(l => l.CommentId == id);

            return Ok(new { message = "Beğeni durumu güncellendi", likeCount });
        }

        [HttpGet("pending")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPendingComments([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var query = _context.Comments
                .AsNoTracking()
                .Include(c => c.User)
                .Include(c => c.Prediction)
                .Include(c => c.DailyPost)
                .Where(c => !c.IsApproved && c.IsActive);

            var totalCount = await query.CountAsync();
            var comments = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    Id = c.Id,
                    Content = c.Content,
                    ImageUrl = c.ImageUrl,
                    CreatedAt = c.CreatedAt,
                    User = new
                    {
                        Id = c.User.Id,
                        FirstName = c.User.FirstName,
                        LastName = c.User.LastName,
                        Email = c.User.Email
                    },
                    Prediction = c.Prediction == null ? null : new
                    {
                        Id = c.Prediction.Id,
                        Title = c.Prediction.Title
                    },
                    DailyPost = c.DailyPost == null ? null : new
                    {
                        Id = c.DailyPost.Id,
                        Title = c.DailyPost.Title
                    }
                })
                .ToListAsync();

            return Ok(new
            {
                comments,
                totalCount,
                currentPage = page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        [HttpPost("{id}/approve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveComment(int id)
        {
            var comment = await _context.Comments
                .Include(c => c.Prediction)
                .Include(c => c.DailyPost)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null)
            {
                return NotFound("Yorum bulunamadı");
            }

            var adminUserId = GetCurrentUserId()!.Value;

            comment.IsApproved = true;
            comment.ApprovedAt = DateTime.UtcNow;
            comment.ApprovedByUserId = adminUserId;

            // Yorum sayısını artır
            if (comment.PredictionId.HasValue && comment.Prediction != null)
            {
                comment.Prediction.CommentCount++;
            }
            else if (comment.DailyPostId.HasValue)
            {
                var post = await _context.DailyPosts.FindAsync(comment.DailyPostId.Value);
                if (post != null) post.CommentCount++;
            }

            await _context.SaveChangesAsync();

            // Send new comment notification - KALDIRILDI (sadece admin paylaşımları için bildirim)
            if (comment.PredictionId.HasValue)
            {
                var commentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _notificationService.SendNewCommentNotificationAsync(comment.PredictionId.Value, comment.Content, commentUserId);
            }

            return Ok(new { message = "Yorum onaylandı" });
        }

        [HttpPost("{id}/reject")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RejectComment(int id)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null)
            {
                return NotFound("Yorum bulunamadı");
            }

            comment.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Yorum reddedildi" });
        }

        [HttpGet("dailypost/{dailyPostId}")]
        public async Task<IActionResult> GetCommentsByDailyPost(int dailyPostId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var post = await _context.DailyPosts.FindAsync(dailyPostId);
            if (post == null)
            {
                return NotFound("Paylaşım bulunamadı");
            }

            var currentUserId = GetCurrentUserId();

            var query = _context.Comments
                .AsNoTracking()
                .Include(c => c.User)
                .Where(c => c.DailyPostId == dailyPostId && c.IsApproved && c.IsActive);

            var totalCount = await query.CountAsync();
            var comments = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CommentDto
                {
                    Id = c.Id,
                    Content = c.Content,
                    ImageUrl = c.ImageUrl,
                    CreatedAt = c.CreatedAt,
                    User = new UserDto
                    {
                        Id = c.User.Id,
                        FirstName = c.User.FirstName,
                        LastName = c.User.LastName,
                        Role = c.User.Role.ToString(),
                        CreatedAt = c.User.CreatedAt,
                        ProfileImageUrl = c.User.ProfileImageUrl,
                        IsVipActive = c.User.VipExpiryDate.HasValue && c.User.VipExpiryDate > DateTime.UtcNow,
                        IsBlocked = c.User.IsBlocked
                    },
                    IsLikedByCurrentUser = currentUserId.HasValue && c.Likes != null && c.Likes.Any(l => l.UserId == currentUserId.Value),
                    LikeCount = c.Likes != null ? c.Likes.Count : 0
                })
                .ToListAsync();

            return Ok(new
            {
                comments,
                totalCount,
                currentPage = page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        [HttpPost("dailypost/{dailyPostId}")]
        [Authorize]
        public async Task<IActionResult> CreateCommentForDailyPost(int dailyPostId, [FromForm] CreateCommentDto dto)
        {
            var userId = GetCurrentUserId()!.Value;
            var post = await _context.DailyPosts.FindAsync(dailyPostId);
            if (post == null)
            {
                return NotFound("Paylaşım bulunamadı");
            }

            string? imageUrl = null;
            if (dto.Image != null)
            {
                if (!_s3Service.IsValidImageFile(dto.Image))
                {
                    return BadRequest("Geçersiz resim dosyası");
                }

                imageUrl = await _s3Service.UploadFileAsync(dto.Image, "comments");
            }

            var comment = new Comment
            {
                Content = dto.Content,
                ImageUrl = imageUrl,
                UserId = userId,
                DailyPostId = dailyPostId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                IsApproved = false // Admin onayı bekliyor
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Yorum gönderildi. Admin onayından sonra yayınlanacak." });
        }

        // 👥 Yorumu beğenen kullanıcıları getir
        [HttpGet("{id}/likes")]
        public async Task<IActionResult> GetCommentLikes(int id)
        {
            var comment = await _context.Comments
                .Include(c => c.Prediction)
                .FirstOrDefaultAsync(c => c.Id == id && c.IsApproved && c.IsActive);

            if (comment == null)
            {
                return NotFound("Yorum bulunamadı");
            }

            // Ücretli tahmin ise VIP kontrolü
            var currentUserId = GetCurrentUserId();
            if (comment.Prediction != null && comment.Prediction.IsPaid)
            {
                if (!currentUserId.HasValue)
                {
                    return Unauthorized("Bu içeriği görüntülemek için VIP üyelik gerekli");
                }

                var isVipUser = await IsVipUser(currentUserId.Value);
                if (!isVipUser)
                {
                    return Forbid("Bu içeriği görüntülemek için VIP üyelik gerekli");
                }
            }

            var likes = await _context.Likes
                .Include(l => l.User)
                .Where(l => l.CommentId == id)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            var likers = likes.Select(l => new CommentLikerDto
            {
                UserId = l.UserId,
                UserName = l.User.FirstName + " " + l.User.LastName,
                ProfileImageUrl = l.User.ProfileImageUrl,
                LikeType = (int)l.Type,
                LikeTypeName = l.Type.ToString(),
                LikedAt = l.CreatedAt
            }).ToList();

            var result = new CommentLikersDto
            {
                CommentId = comment.Id,
                CommentContent = comment.Content.Length > 100 ? comment.Content.Substring(0, 100) + "..." : comment.Content,
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

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : null;
        }

        private async Task<bool> IsVipUser(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            if (user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin)
                return true;

            if (user.Role == UserRole.VipUser)
            {
                // VIP süresi kontrol et
                if (user.VipExpiryDate.HasValue && user.VipExpiryDate > DateTime.UtcNow)
                {
                    return true;
                }
                else
                {
                    // VIP süresi dolmuş, normal kullanıcıya dönüştür
                    user.Role = UserRole.NormalUser;
                    user.VipExpiryDate = null;
                    await _context.SaveChangesAsync();
                    return false;
                }
            }

            return false;
        }
    }
}