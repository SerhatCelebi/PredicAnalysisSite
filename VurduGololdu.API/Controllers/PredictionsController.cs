using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using VurduGololdu.API.Data;
using VurduGololdu.API.DTOs;
using VurduGololdu.API.Models;
using VurduGololdu.API.Services;
using VurduGololdu.API.Extensions;

namespace VurduGololdu.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PredictionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IS3Service _s3Service;
        private readonly INotificationService _notificationService;
        private readonly ICacheService _cacheService;

        public PredictionsController(ApplicationDbContext context, IS3Service s3Service, INotificationService notificationService, ICacheService cacheService)
        {
            _context = context;
            _s3Service = s3Service;
            _notificationService = notificationService;
            _cacheService = cacheService;
        }

        [HttpGet]
        public async Task<IActionResult> GetPredictions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool onlyFree = false)
        {
            // Cache key oluştur
            var currentUserId = User.GetCurrentUserId();
            var isVipUser = currentUserId.HasValue && await IsVipUser(currentUserId.Value);
            var cacheKey = onlyFree || !isVipUser ?
                $"{CacheKeys.FreePredictions}:page:{page}:size:{pageSize}" :
                $"{CacheKeys.AllPredictions}:page:{page}:size:{pageSize}";

            // Cache'den kontrol et
            var cachedResult = await _cacheService.GetAsync<object>(cacheKey);
            if (cachedResult != null)
            {
                return Ok(cachedResult);
            }

            var query = _context.Predictions
                .AsNoTracking()
                .Include(p => p.User)
                .Where(p => p.IsActive);

            // Eğer kullanıcı VIP değilse veya giriş yapmamışsa sadece ücretsiz tahminleri göster
            if (!isVipUser || onlyFree)
            {
                query = query.Where(p => !p.IsPaid);
            }

            var totalCount = await query.CountAsync();
            var predictions = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Kullanıcının beğendiği tahminleri al
            var predictionIds = predictions.Select(p => p.Id).ToList();

            List<int> userLikes;
            if (currentUserId.HasValue)
            {
                var uid = currentUserId.Value;
                userLikes = await _context.Likes
                    .Where(l => l.UserId == uid && l.PredictionId.HasValue && predictionIds.Contains(l.PredictionId.Value))
                    .Select(l => l.PredictionId!.Value)
                    .ToListAsync();
            }
            else
            {
                userLikes = new List<int>();
            }

            var predictionDtos = predictions.Select(p => new PredictionListDto
            {
                Id = p.Id,
                Title = p.Title,
                Content = p.Content.Length > 200 ? p.Content.Substring(0, 200) + "..." : p.Content,
                IsPaid = p.IsPaid,
                FirstImageUrl = GetFirstImageUrl(p.ImageUrls),
                CreatedAt = p.CreatedAt,
                ViewCount = p.ViewCount,
                LikeCount = p.LikeCount,
                CommentCount = p.CommentCount,
                UserName = $"{p.User.FirstName} {p.User.LastName}",
                UserId = p.User.Id,
                User = new UserDto
                {
                    Id = p.User.Id,
                    FirstName = p.User.FirstName,
                    LastName = p.User.LastName,
                    Email = p.User.Email,
                    Role = p.User.Role.ToString(),
                    CreatedAt = p.User.CreatedAt,
                    ProfileImageUrl = p.User.ProfileImageUrl,
                    IsVipActive = p.User.VipExpiryDate.HasValue && p.User.VipExpiryDate > DateTime.UtcNow,
                    IsBlocked = p.User.IsBlocked
                },
                IsLikedByCurrentUser = userLikes.Contains(p.Id)
            }).ToList();

            var result = new
            {
                predictions = predictionDtos,
                totalCount,
                currentPage = page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            // Cache'e kaydet (5 dakika)
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPrediction(int id)
        {
            // Cache key oluştur
            var cacheKey = string.Format(CacheKeys.PredictionDetail, id);

            // Cache'den kontrol et
            var cachedPrediction = await _cacheService.GetAsync<object>(cacheKey);
            if (cachedPrediction != null)
            {
                // View count'u artır ama cache'i güncellemeden
                var prediction = await _context.Predictions.FindAsync(id);
                if (prediction != null)
                {
                    prediction.ViewCount++;
                    await _context.SaveChangesAsync();
                }
                return Ok(cachedPrediction);
            }

            var predictionData = await _context.Predictions
                .Include(p => p.User)
                .Include(p => p.Comments.Where(c => c.IsApproved && c.IsActive))
                    .ThenInclude(c => c.User)
                .Include(p => p.Likes)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (predictionData == null)
            {
                return NotFound("Tahmin bulunamadı");
            }

            // Ücretli tahmin ise ve kullanıcı VIP değilse erişim engelle
            var currentUserId = User.GetCurrentUserId();
            if (predictionData.IsPaid)
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

            // Görüntülenme sayısını artır
            predictionData.ViewCount++;
            await _context.SaveChangesAsync();

            var imageUrls = string.IsNullOrEmpty(predictionData.ImageUrls)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(predictionData.ImageUrls) ?? new List<string>();

            var isLikedByCurrentUser = currentUserId.HasValue &&
                predictionData.Likes.Any(l => l.UserId == currentUserId.Value);

            var predictionDto = new PredictionDto
            {
                Id = predictionData.Id,
                Title = predictionData.Title,
                Content = predictionData.Content,
                IsPaid = predictionData.IsPaid,
                ImageUrls = imageUrls,
                CreatedAt = predictionData.CreatedAt,
                UpdatedAt = predictionData.UpdatedAt,
                ViewCount = predictionData.ViewCount,
                LikeCount = predictionData.LikeCount,
                CommentCount = predictionData.CommentCount,
                User = new UserDto
                {
                    Id = predictionData.User.Id,
                    FirstName = predictionData.User.FirstName,
                    LastName = predictionData.User.LastName,
                    Email = predictionData.User.Email,
                    Role = predictionData.User.Role.ToString(),
                    CreatedAt = predictionData.User.CreatedAt,
                    ProfileImageUrl = predictionData.User.ProfileImageUrl,
                    IsVipActive = predictionData.User.VipExpiryDate.HasValue && predictionData.User.VipExpiryDate > DateTime.UtcNow,
                    IsBlocked = predictionData.User.IsBlocked
                },
                IsLikedByCurrentUser = isLikedByCurrentUser
            };

            // Cache'e kaydet (10 dakika)
            await _cacheService.SetAsync(cacheKey, predictionDto, TimeSpan.FromMinutes(10));

            return Ok(predictionDto);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreatePrediction([FromForm] CreatePredictionDto dto)
        {
            var userId = User.GetCurrentUserId()!.Value;

            // Resim yükleme
            var imageUrls = new List<string>();
            if (dto.Images != null && dto.Images.Any())
            {
                foreach (var image in dto.Images)
                {
                    if (_s3Service.IsValidImageFile(image))
                    {
                        var imageUrl = await _s3Service.UploadFileAsync(image, "predictions");
                        imageUrls.Add(imageUrl);
                    }
                }
            }

            var prediction = new Prediction
            {
                Title = dto.Title,
                Content = dto.Content,
                IsPaid = dto.IsPaid,
                ImageUrls = imageUrls.Any() ? JsonSerializer.Serialize(imageUrls) : null,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Predictions.Add(prediction);
            await _context.SaveChangesAsync();

            // Cache'i temizle
            _cacheService.ClearPredictionsCache();

            // Send new prediction notification
            var adminUserId = User.GetCurrentUserId()!.Value;
            await _notificationService.SendNewPredictionNotificationAsync(prediction.Title, prediction.Id, prediction.IsPaid, adminUserId);

            return Ok(new { message = "Tahmin başarıyla oluşturuldu", predictionId = prediction.Id });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdatePrediction(int id, UpdatePredictionDto dto)
        {
            var prediction = await _context.Predictions.FindAsync(id);
            if (prediction == null)
            {
                return NotFound("Tahmin bulunamadı");
            }

            prediction.Title = dto.Title;
            prediction.Content = dto.Content;
            prediction.IsPaid = dto.IsPaid;
            prediction.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Cache'i temizle
            _cacheService.ClearPredictionsCache();
            await _cacheService.RemoveAsync(string.Format(CacheKeys.PredictionDetail, id));

            return Ok(new { message = "Tahmin başarıyla güncellendi" });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeletePrediction(int id)
        {
            var prediction = await _context.Predictions.FindAsync(id);
            if (prediction == null)
            {
                return NotFound("Tahmin bulunamadı");
            }

            prediction.IsActive = false;
            await _context.SaveChangesAsync();

            // Cache'i temizle
            _cacheService.ClearPredictionsCache();
            await _cacheService.RemoveAsync(string.Format(CacheKeys.PredictionDetail, id));

            return Ok(new { message = "Tahmin başarıyla silindi" });
        }

        [HttpPost("{id}/like")]
        [Authorize]
        public async Task<IActionResult> LikePrediction(int id, LikeDto dto)
        {
            var userId = User.GetCurrentUserId()!.Value;
            var prediction = await _context.Predictions.FindAsync(id);

            if (prediction == null)
            {
                return NotFound("Tahmin bulunamadı");
            }

            // Ücretli tahmin ise VIP kontrolü
            if (prediction.IsPaid && !await IsVipUser(userId))
            {
                return Forbid("Bu içeriği beğenmek için VIP üyelik gerekli");
            }

            var existingLike = await _context.Likes
                .FirstOrDefaultAsync(l => l.UserId == userId && l.PredictionId == id);

            if (existingLike != null)
            {
                // Aynı tip beğeni ise kaldır, farklı ise güncelle
                if (existingLike.Type == (LikeType)dto.Type)
                {
                    _context.Likes.Remove(existingLike);
                    prediction.LikeCount--;
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
                    PredictionId = id,
                    Type = (LikeType)dto.Type,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Likes.Add(like);
                prediction.LikeCount++;
            }

            await _context.SaveChangesAsync();

            // Cache'den tahmin detayını kaldır (like count değişti)
            await _cacheService.RemoveAsync(string.Format(CacheKeys.PredictionDetail, id));

            return Ok(new { message = "Beğeni durumu güncellendi", likeCount = prediction.LikeCount });
        }

        // Eski GetCurrentUserId metodu kaldırıldı; ClaimsPrincipalExtensions kullanılacak

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

        private static string? GetFirstImageUrl(string? imageUrls)
        {
            if (string.IsNullOrEmpty(imageUrls))
                return null;

            try
            {
                var urls = JsonSerializer.Deserialize<List<string>>(imageUrls);
                return urls?.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        // 🎯 TAHMIN PAYLAŞIM SİSTEMİ
        [HttpPost("{id}/share")]
        [Authorize]
        public async Task<IActionResult> SharePrediction(int id, SharePredictionDto dto)
        {
            var prediction = await _context.Predictions.FindAsync(id);
            if (prediction == null)
            {
                return NotFound("Tahmin bulunamadı");
            }

            // Ücretli tahmin ise VIP kontrolü
            var userId = User.GetCurrentUserId()!.Value;
            if (prediction.IsPaid && !await IsVipUser(userId))
            {
                return Forbid("Bu içeriği paylaşmak için VIP üyelik gerekli");
            }

            // Paylaşım sayısını artır
            prediction.ShareCount++;
            prediction.LastSharedAt = DateTime.UtcNow;
            prediction.IsShared = true;

            await _context.SaveChangesAsync();

            // Cache'i temizle
            await _cacheService.RemoveAsync(string.Format(CacheKeys.PredictionDetail, id));

            var shareUrl = $"https://vurdugololdu.com/predictions/{id}";
            var shareText = $"VurduGololdu - {prediction.Title}";

            return Ok(new
            {
                message = "Tahmin paylaşım sayısı güncellendi",
                shareCount = prediction.ShareCount,
                shareUrl,
                shareText,
                platform = dto.Platform
            });
        }

        // 📊 TAHMİN SONUCU BELİRLEME (Admin)
        [HttpPut("{id}/result")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetPredictionResult(int id, PredictionResultDto dto)
        {
            var prediction = await _context.Predictions
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prediction == null)
            {
                return NotFound("Tahmin bulunamadı");
            }

            prediction.IsCorrect = dto.IsCorrect;
            prediction.ResultNote = dto.ResultNote;
            prediction.ResultDate = dto.ResultDate ?? DateTime.UtcNow;
            prediction.Status = PredictionStatus.Completed;
            prediction.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Kullanıcı istatistiklerini güncelle
            var analyticsService = HttpContext.RequestServices.GetRequiredService<IAnalyticsService>();
            await analyticsService.UpdateUserSuccessStatsAsync(prediction.UserId);

            // Cache'i temizle
            await _cacheService.RemoveAsync(string.Format(CacheKeys.PredictionDetail, id));

            // Sonuç bildirimi gönder
            // var resultMessage = dto.IsCorrect ? "Tebrikler! Tahmininiz doğru çıktı!" : "Tahmininiz maalesef tutmadı.";
            // await _notificationService.SendEmailAsync(
            //     prediction.User.Email,
            //     "Tahmin Sonucu",
            //     $"{resultMessage} Tahmin: {prediction.Title}. {dto.ResultNote}"
            // );

            return Ok(new
            {
                message = "Tahmin sonucu başarıyla belirlendi",
                isCorrect = dto.IsCorrect,
                resultNote = dto.ResultNote
            });
        }

        // 📌 TAHMİN SABİTLEME (Admin)
        [HttpPut("{id}/pin")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PinPrediction(int id, PinPredictionDto dto)
        {
            var prediction = await _context.Predictions.FindAsync(id);
            if (prediction == null)
            {
                return NotFound("Tahmin bulunamadı");
            }

            var adminUserId = User.GetCurrentUserId()!.Value;

            if (dto.IsPinned)
            {
                // Diğer sabitlenmiş tahminleri kaldır (tek seferde bir tahmin sabitlenebilir)
                var pinnedPredictions = await _context.Predictions
                    .Where(p => p.IsPinned)
                    .ToListAsync();

                foreach (var pinnedPrediction in pinnedPredictions)
                {
                    pinnedPrediction.IsPinned = false;
                    pinnedPrediction.PinnedAt = null;
                    pinnedPrediction.PinnedByUserId = null;
                }

                prediction.IsPinned = true;
                prediction.PinnedAt = DateTime.UtcNow;
                prediction.PinnedByUserId = adminUserId;
            }
            else
            {
                prediction.IsPinned = false;
                prediction.PinnedAt = null;
                prediction.PinnedByUserId = null;
            }

            await _context.SaveChangesAsync();

            // Cache'i temizle
            _cacheService.ClearPredictionsCache();

            return Ok(new
            {
                message = dto.IsPinned ? "Tahmin başa sabitlendi" : "Tahmin sabitleme kaldırıldı",
                isPinned = prediction.IsPinned,
                reason = dto.Reason
            });
        }

        // ⭐ EN BAŞARILI TAHMİN SEÇME (Admin)
        [HttpPut("{id}/featured")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetFeaturedPrediction(int id, FeaturedPredictionDto dto)
        {
            var prediction = await _context.Predictions
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prediction == null)
            {
                return NotFound("Tahmin bulunamadı");
            }

            if (dto.IsFeatured)
            {
                // Sadece doğru tahminler öne çıkarılabilir
                if (prediction.IsCorrect != true)
                {
                    return BadRequest("Sadece doğru tahminler öne çıkarılabilir");
                }

                // Diğer öne çıkarılmış tahminleri kaldır
                var featuredPredictions = await _context.Predictions
                    .Where(p => p.IsFeatured)
                    .ToListAsync();

                foreach (var featuredPrediction in featuredPredictions)
                {
                    featuredPrediction.IsFeatured = false;
                }

                prediction.IsFeatured = true;

                // Başarılı tahmin bildirimi gönder
                // await _notificationService.SendEmailAsync(
                //     prediction.User.Email,
                //     "Tebrikler! Tahmininiz Öne Çıkarıldı",
                //     $"Sayın {prediction.User.FirstName}, '{prediction.Title}' adlı tahmininiz en başarılı tahmin olarak seçildi ve öne çıkarıldı!"
                // );
            }
            else
            {
                prediction.IsFeatured = false;
            }

            await _context.SaveChangesAsync();

            // Cache'i temizle
            _cacheService.ClearPredictionsCache();

            return Ok(new
            {
                message = dto.IsFeatured ? "Tahmin en başarılı olarak işaretlendi" : "Tahmin öne çıkarma kaldırıldı",
                isFeatured = prediction.IsFeatured,
                reason = dto.Reason
            });
        }

        // 📈 EN BAŞARILI TAHMİNLERİ LİSTELE
        [HttpGet("featured")]
        public async Task<IActionResult> GetFeaturedPredictions([FromQuery] int count = 5)
        {
            var featuredPredictions = await _context.Predictions
                .Include(p => p.User)
                .Where(p => p.IsActive && (p.IsFeatured || p.IsCorrect == true))
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.LikeCount)
                .ThenByDescending(p => p.ViewCount)
                .Take(count)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    Content = p.Content.Length > 150 ? p.Content.Substring(0, 150) + "..." : p.Content,
                    p.IsFeatured,
                    p.IsCorrect,
                    p.ViewCount,
                    p.LikeCount,
                    p.ShareCount,
                    p.CreatedAt,
                    FirstImageUrl = GetFirstImageUrl(p.ImageUrls),
                    User = new
                    {
                        p.User.Id,
                        p.User.FirstName,
                        p.User.LastName
                    }
                })
                .ToListAsync();

            return Ok(featuredPredictions);
        }

        // 👥 Tahmini beğenen kullanıcıları getir
        [HttpGet("{id}/likes")]
        public async Task<IActionResult> GetPredictionLikes(int id)
        {
            var prediction = await _context.Predictions
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (prediction == null)
            {
                return NotFound("Tahmin bulunamadı");
            }

            // Ücretli tahmin ise VIP kontrolü
            var currentUserId = User.GetCurrentUserId();
            if (prediction.IsPaid)
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
                .Where(l => l.PredictionId == id)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            var likers = likes.Select(l => new PredictionLikerDto
            {
                UserId = l.UserId,
                UserName = l.User.FirstName + " " + l.User.LastName,
                ProfileImageUrl = l.User.ProfileImageUrl,
                LikeType = (int)l.Type,
                LikeTypeName = l.Type.ToString(),
                LikedAt = l.CreatedAt
            }).ToList();

            var result = new PredictionLikersDto
            {
                PredictionId = prediction.Id,
                PredictionTitle = prediction.Title,
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

        // 📌 SABİTLENMİŞ TAHMİN
        [HttpGet("pinned")]
        public async Task<IActionResult> GetPinnedPrediction()
        {
            var currentUserId = User.GetCurrentUserId();
            var pinnedPrediction = await _context.Predictions
                .Include(p => p.User)
                .Include(p => p.PinnedByUser)
                .FirstOrDefaultAsync(p => p.IsPinned && p.IsActive);

            if (pinnedPrediction == null)
            {
                return Ok(new { message = "Sabitlenmiş tahmin bulunamadı" });
            }

            // Kullanıcının bu tahmini beğenip beğenmediğini kontrol et
            var isLikedByCurrentUser = currentUserId.HasValue &&
                await _context.Likes.AnyAsync(l => l.UserId == currentUserId.Value && l.PredictionId == pinnedPrediction.Id);

            return Ok(new
            {
                pinnedPrediction.Id,
                pinnedPrediction.Title,
                Content = pinnedPrediction.Content.Length > 200 ? pinnedPrediction.Content.Substring(0, 200) + "..." : pinnedPrediction.Content,
                pinnedPrediction.ViewCount,
                pinnedPrediction.LikeCount,
                pinnedPrediction.ShareCount,
                pinnedPrediction.CreatedAt,
                pinnedPrediction.PinnedAt,
                FirstImageUrl = GetFirstImageUrl(pinnedPrediction.ImageUrls),
                IsLikedByCurrentUser = isLikedByCurrentUser,
                User = new
                {
                    pinnedPrediction.User.Id,
                    pinnedPrediction.User.FirstName,
                    pinnedPrediction.User.LastName
                },
                PinnedBy = pinnedPrediction.PinnedByUser != null ? new
                {
                    pinnedPrediction.PinnedByUser.Id,
                    pinnedPrediction.PinnedByUser.FirstName,
                    pinnedPrediction.PinnedByUser.LastName
                } : null
            });
        }
    }
}