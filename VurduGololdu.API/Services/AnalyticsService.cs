using Microsoft.EntityFrameworkCore;
using VurduGololdu.API.Data;
using VurduGololdu.API.Models;

namespace VurduGololdu.API.Services
{
    public interface IAnalyticsService
    {
        Task<DailyAnalytics> GetTodayAnalyticsAsync();
        Task<List<DailyAnalytics>> GetAnalyticsRangeAsync(DateTime startDate, DateTime endDate);
        Task<AnalyticsSummary> GetAnalyticsSummaryAsync();
        Task<List<UserSuccessStats>> GetTopUsersAsync(int count = 10);
        Task UpdateUserSuccessStatsAsync(int userId);
        Task GenerateDailyAnalyticsAsync(DateTime date);
        Task<RevenueAnalytics> GetRevenueAnalyticsAsync(DateTime startDate, DateTime endDate);
    }

    public class AnalyticsService : IAnalyticsService
    {
        private readonly ApplicationDbContext _context;

        public AnalyticsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DailyAnalytics> GetTodayAnalyticsAsync()
        {
            var today = DateTime.UtcNow.Date;
            var analytics = await _context.DailyAnalytics
                .FirstOrDefaultAsync(a => a.Date.Date == today);

            if (analytics == null)
            {
                await GenerateDailyAnalyticsAsync(today);
                analytics = await _context.DailyAnalytics
                    .FirstOrDefaultAsync(a => a.Date.Date == today);
            }

            return analytics ?? new DailyAnalytics { Date = today };
        }

        public async Task<List<DailyAnalytics>> GetAnalyticsRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.DailyAnalytics
                .Where(a => a.Date.Date >= startDate.Date && a.Date.Date <= endDate.Date)
                .OrderBy(a => a.Date)
                .ToListAsync();
        }

        public async Task<AnalyticsSummary> GetAnalyticsSummaryAsync()
        {
            var today = DateTime.UtcNow.Date;
            var lastWeek = today.AddDays(-7);
            var lastMonth = today.AddDays(-30);

            // Toplam kullanıcı sayısı
            var totalUsers = await _context.Users.CountAsync(u => u.IsActive);
            var totalVipUsers = await _context.Users.CountAsync(u => u.IsActive && u.VipExpiryDate > DateTime.UtcNow);
            
            // Toplam tahmin sayısı
            var totalPredictions = await _context.Predictions.CountAsync(p => p.IsActive);
            var completedPredictions = await _context.Predictions.CountAsync(p => p.Status == PredictionStatus.Completed);
            var correctPredictions = await _context.Predictions.CountAsync(p => p.IsCorrect == true);

            // Başarı oranı
            var overallSuccessRate = completedPredictions > 0 ? (decimal)correctPredictions / completedPredictions * 100 : 0;

            // Gelir hesaplamaları
            var totalRevenue = await _context.PaymentNotifications
                .Where(p => p.Status == PaymentStatus.Approved)
                .SumAsync(p => p.Amount);

            var weeklyRevenue = await _context.PaymentNotifications
                .Where(p => p.Status == PaymentStatus.Approved && p.CreatedAt >= lastWeek)
                .SumAsync(p => p.Amount);

            var monthlyRevenue = await _context.PaymentNotifications
                .Where(p => p.Status == PaymentStatus.Approved && p.CreatedAt >= lastMonth)
                .SumAsync(p => p.Amount);

            // Engagement istatistikleri
            var totalLikes = await _context.Likes.CountAsync();
            var totalComments = await _context.Comments.CountAsync();
            var totalViews = await _context.Predictions.SumAsync(p => p.ViewCount);

            return new AnalyticsSummary
            {
                TotalUsers = totalUsers,
                TotalVipUsers = totalVipUsers,
                TotalPredictions = totalPredictions,
                CompletedPredictions = completedPredictions,
                CorrectPredictions = correctPredictions,
                OverallSuccessRate = overallSuccessRate,
                TotalRevenue = totalRevenue,
                WeeklyRevenue = weeklyRevenue,
                MonthlyRevenue = monthlyRevenue,
                TotalLikes = totalLikes,
                TotalComments = totalComments,
                TotalViews = totalViews,
                GeneratedAt = DateTime.UtcNow
            };
        }

        public async Task<List<UserSuccessStats>> GetTopUsersAsync(int count = 10)
        {
            return await _context.UserSuccessStats
                .Include(u => u.User)
                .Where(u => u.TotalPredictions >= 5) // En az 5 tahmin yapmış olmalı
                .OrderByDescending(u => u.SuccessRate)
                .ThenByDescending(u => u.TotalPredictions)
                .Take(count)
                .ToListAsync();
        }

        public async Task UpdateUserSuccessStatsAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return;

            var userPredictions = await _context.Predictions
                .Where(p => p.UserId == userId && p.IsActive)
                .ToListAsync();

            var totalPredictions = userPredictions.Count;
            var correctPredictions = userPredictions.Count(p => p.IsCorrect == true);
            var incorrectPredictions = userPredictions.Count(p => p.IsCorrect == false);
            var pendingPredictions = userPredictions.Count(p => p.IsCorrect == null);

            var successRate = totalPredictions > 0 ? (decimal)correctPredictions / totalPredictions * 100 : 0;

            // Streak hesaplama (ardışık doğru tahminler)
            var currentStreak = CalculateCurrentStreak(userPredictions);
            var bestStreak = CalculateBestStreak(userPredictions);

            // Engagement istatistikleri
            var totalLikes = await _context.Likes.CountAsync(l => l.Prediction!.UserId == userId);
            var totalComments = await _context.Comments.CountAsync(c => c.Prediction!.UserId == userId);
            var totalViews = userPredictions.Sum(p => p.ViewCount);
            var totalShares = userPredictions.Sum(p => p.ShareCount);

            var existingStats = await _context.UserSuccessStats
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (existingStats == null)
            {
                existingStats = new UserSuccessStats
                {
                    UserId = userId,
                    User = user
                };
                _context.UserSuccessStats.Add(existingStats);
            }

            existingStats.TotalPredictions = totalPredictions;
            existingStats.CorrectPredictions = correctPredictions;
            existingStats.IncorrectPredictions = incorrectPredictions;
            existingStats.PendingPredictions = pendingPredictions;
            existingStats.SuccessRate = successRate;
            existingStats.CurrentStreak = currentStreak;
            existingStats.BestStreak = Math.Max(existingStats.BestStreak, bestStreak);
            existingStats.TotalLikes = totalLikes;
            existingStats.TotalComments = totalComments;
            existingStats.TotalViews = totalViews;
            existingStats.TotalShares = totalShares;
            existingStats.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task GenerateDailyAnalyticsAsync(DateTime date)
        {
            var dateOnly = date.Date;

            // Kullanıcı istatistikleri
            var newUserCount = await _context.Users
                .CountAsync(u => u.CreatedAt.Date == dateOnly && u.IsActive);
            
            var activeUserCount = await _context.Users
                .CountAsync(u => u.LastLoginDate.HasValue && u.LastLoginDate.Value.Date == dateOnly && u.IsActive);
            
            var totalUserCount = await _context.Users.CountAsync(u => u.IsActive);

            // Tahmin istatistikleri
            var newPredictionCount = await _context.Predictions
                .CountAsync(p => p.CreatedAt.Date == dateOnly && p.IsActive);
            
            var completedPredictionCount = await _context.Predictions
                .CountAsync(p => p.UpdatedAt.HasValue && p.UpdatedAt.Value.Date == dateOnly && p.Status == PredictionStatus.Completed);
            
            var correctPredictionCount = await _context.Predictions
                .CountAsync(p => p.UpdatedAt.HasValue && p.UpdatedAt.Value.Date == dateOnly && p.IsCorrect == true);
            
            var totalPredictionCount = await _context.Predictions.CountAsync(p => p.IsActive);

            // Başarı oranları
            var overallSuccessRate = await CalculateSuccessRate(null);
            var vipSuccessRate = await CalculateSuccessRate(UserRole.VipUser);
            var normalUserSuccessRate = await CalculateSuccessRate(UserRole.NormalUser);

            // Gelir istatistikleri
            var dailyRevenue = await _context.PaymentNotifications
                .Where(p => p.CreatedAt.Date == dateOnly && p.Status == PaymentStatus.Approved)
                .SumAsync(p => p.Amount);
            
            var totalRevenue = await _context.PaymentNotifications
                .Where(p => p.Status == PaymentStatus.Approved)
                .SumAsync(p => p.Amount);

            var newVipUserCount = await _context.PaymentNotifications
                .CountAsync(p => p.CreatedAt.Date == dateOnly && p.Status == PaymentStatus.Approved);

            var expiredVipUserCount = await _context.Users
                .CountAsync(u => u.VipExpiryDate.HasValue && u.VipExpiryDate.Value.Date == dateOnly);

            // Engagement istatistikleri
            var totalLikeCount = await _context.Likes.CountAsync();
            var totalCommentCount = await _context.Comments.CountAsync();
            var totalShareCount = await _context.Predictions.SumAsync(p => p.ShareCount);
            var totalViewCount = await _context.Predictions.SumAsync(p => p.ViewCount);

            var existingAnalytics = await _context.DailyAnalytics
                .FirstOrDefaultAsync(a => a.Date.Date == dateOnly);

            if (existingAnalytics == null)
            {
                var analytics = new DailyAnalytics
                {
                    Date = dateOnly,
                    NewUserCount = newUserCount,
                    ActiveUserCount = activeUserCount,
                    TotalUserCount = totalUserCount,
                    NewPredictionCount = newPredictionCount,
                    CompletedPredictionCount = completedPredictionCount,
                    CorrectPredictionCount = correctPredictionCount,
                    TotalPredictionCount = totalPredictionCount,
                    OverallSuccessRate = overallSuccessRate,
                    VipSuccessRate = vipSuccessRate,
                    NormalUserSuccessRate = normalUserSuccessRate,
                    DailyRevenue = dailyRevenue,
                    TotalRevenue = totalRevenue,
                    NewVipUserCount = newVipUserCount,
                    ExpiredVipUserCount = expiredVipUserCount,
                    TotalLikeCount = totalLikeCount,
                    TotalCommentCount = totalCommentCount,
                    TotalShareCount = totalShareCount,
                    TotalViewCount = totalViewCount
                };

                _context.DailyAnalytics.Add(analytics);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<RevenueAnalytics> GetRevenueAnalyticsAsync(DateTime startDate, DateTime endDate)
        {
            var payments = await _context.PaymentNotifications
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate && p.Status == PaymentStatus.Approved)
                .ToListAsync();

            var totalRevenue = payments.Sum(p => p.Amount);
            var averageOrderValue = payments.Any() ? payments.Average(p => p.Amount) : 0;
            var totalTransactions = payments.Count;

            var dailyRevenue = payments
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new DailyRevenueData
                {
                    Date = g.Key,
                    Revenue = g.Sum(p => p.Amount),
                    TransactionCount = g.Count()
                })
                .OrderBy(d => d.Date)
                .ToList();

            return new RevenueAnalytics
            {
                TotalRevenue = totalRevenue,
                AverageOrderValue = averageOrderValue,
                TotalTransactions = totalTransactions,
                DailyRevenue = dailyRevenue,
                StartDate = startDate,
                EndDate = endDate
            };
        }

        private async Task<decimal> CalculateSuccessRate(UserRole? role)
        {
            var query = _context.Predictions.Where(p => p.Status == PredictionStatus.Completed);
            
            if (role.HasValue)
            {
                query = query.Where(p => p.User.Role == role.Value);
            }

            var totalCompleted = await query.CountAsync();
            var totalCorrect = await query.CountAsync(p => p.IsCorrect == true);

            return totalCompleted > 0 ? (decimal)totalCorrect / totalCompleted * 100 : 0;
        }

        private int CalculateCurrentStreak(List<Prediction> predictions)
        {
            var completedPredictions = predictions
                .Where(p => p.IsCorrect.HasValue)
                .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                .ToList();

            int streak = 0;
            foreach (var prediction in completedPredictions)
            {
                if (prediction.IsCorrect == true)
                {
                    streak++;
                }
                else
                {
                    break;
                }
            }

            return streak;
        }

        private int CalculateBestStreak(List<Prediction> predictions)
        {
            var completedPredictions = predictions
                .Where(p => p.IsCorrect.HasValue)
                .OrderBy(p => p.UpdatedAt ?? p.CreatedAt)
                .ToList();

            int maxStreak = 0;
            int currentStreak = 0;

            foreach (var prediction in completedPredictions)
            {
                if (prediction.IsCorrect == true)
                {
                    currentStreak++;
                    maxStreak = Math.Max(maxStreak, currentStreak);
                }
                else
                {
                    currentStreak = 0;
                }
            }

            return maxStreak;
        }
    }

    public class AnalyticsSummary
    {
        public int TotalUsers { get; set; }
        public int TotalVipUsers { get; set; }
        public int TotalPredictions { get; set; }
        public int CompletedPredictions { get; set; }
        public int CorrectPredictions { get; set; }
        public decimal OverallSuccessRate { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal WeeklyRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public int TotalLikes { get; set; }
        public int TotalComments { get; set; }
        public int TotalViews { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class RevenueAnalytics
    {
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int TotalTransactions { get; set; }
        public List<DailyRevenueData> DailyRevenue { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class DailyRevenueData
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public int TransactionCount { get; set; }
    }
} 