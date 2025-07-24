using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VurduGololdu.API.Services;
using VurduGololdu.API.Helpers;

namespace VurduGololdu.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public AnalyticsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        // 📊 GÜNLÜK ANALİTİK ÖZET
        [HttpGet("summary")]
        public async Task<IActionResult> GetAnalyticsSummary()
        {
            try
            {
                var summary = await _analyticsService.GetAnalyticsSummaryAsync();
                return Ok(summary);
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Analytics summary error: {ex.Message}");
                return StatusCode(500, new { message = "Analytics verileri alınırken hata oluştu" });
            }
        }

        // 📈 BUGÜNKÜ ANALİTİKLER
        [HttpGet("today")]
        public async Task<IActionResult> GetTodayAnalytics()
        {
            try
            {
                var analytics = await _analyticsService.GetTodayAnalyticsAsync();
                return Ok(analytics);
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Today analytics error: {ex.Message}");
                return StatusCode(500, new { message = "Günlük analytics verileri alınırken hata oluştu" });
            }
        }

        // 📅 TARİH ARALIĞI ANALİTİKLERİ
        [HttpGet("range")]
        public async Task<IActionResult> GetAnalyticsRange(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            try
            {
                if (startDate > endDate)
                {
                    return BadRequest("Başlangıç tarihi bitiş tarihinden büyük olamaz");
                }

                if ((endDate - startDate).TotalDays > 365)
                {
                    return BadRequest("Maksimum 1 yıllık veri sorgulanabilir");
                }

                var analytics = await _analyticsService.GetAnalyticsRangeAsync(startDate, endDate);
                return Ok(analytics);
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Analytics range error: {ex.Message}");
                return StatusCode(500, new { message = "Tarih aralığı analytics verileri alınırken hata oluştu" });
            }
        }

        // 💰 GELİR ANALİTİKLERİ
        [HttpGet("revenue")]
        public async Task<IActionResult> GetRevenueAnalytics(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            try
            {
                if (startDate > endDate)
                {
                    return BadRequest("Başlangıç tarihi bitiş tarihinden büyük olamaz");
                }

                var revenueAnalytics = await _analyticsService.GetRevenueAnalyticsAsync(startDate, endDate);
                return Ok(revenueAnalytics);
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Revenue analytics error: {ex.Message}");
                return StatusCode(500, new { message = "Gelir analytics verileri alınırken hata oluştu" });
            }
        }

        // 🏆 EN BAŞARILI KULLANICILAR
        [HttpGet("top-users")]
        public async Task<IActionResult> GetTopUsers([FromQuery] int count = 10)
        {
            try
            {
                if (count < 1 || count > 100)
                {
                    return BadRequest("Count değeri 1-100 arasında olmalı");
                }

                var topUsers = await _analyticsService.GetTopUsersAsync(count);
                return Ok(topUsers);
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Top users error: {ex.Message}");
                return StatusCode(500, new { message = "En başarılı kullanıcılar alınırken hata oluştu" });
            }
        }

        // 🔄 GÜNLÜK ANALİTİK OLUŞTUR
        [HttpPost("generate-daily")]
        public async Task<IActionResult> GenerateDailyAnalytics([FromQuery] DateTime? date = null)
        {
            try
            {
                var targetDate = date ?? DateTime.UtcNow.Date;
                await _analyticsService.GenerateDailyAnalyticsAsync(targetDate);

                return Ok(new
                {
                    message = "Günlük analytics başarıyla oluşturuldu",
                    date = targetDate.ToString("yyyy-MM-dd")
                });
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Generate daily analytics error: {ex.Message}");
                return StatusCode(500, new { message = "Günlük analytics oluşturulurken hata oluştu" });
            }
        }

        // 📊 BAŞARI YÜZDELERI
        [HttpGet("success-rates")]
        public async Task<IActionResult> GetSuccessRates()
        {
            try
            {
                var summary = await _analyticsService.GetAnalyticsSummaryAsync();

                var successRates = new
                {
                    overall = new
                    {
                        rate = summary.OverallSuccessRate,
                        totalPredictions = summary.TotalPredictions,
                        correctPredictions = summary.CorrectPredictions,
                        completedPredictions = summary.CompletedPredictions
                    },
                    engagement = new
                    {
                        totalUsers = summary.TotalUsers,
                        totalVipUsers = summary.TotalVipUsers,
                        totalLikes = summary.TotalLikes,
                        totalComments = summary.TotalComments,
                        totalViews = summary.TotalViews
                    },
                    revenue = new
                    {
                        total = summary.TotalRevenue,
                        weekly = summary.WeeklyRevenue,
                        monthly = summary.MonthlyRevenue,
                        vipUserPercentage = summary.TotalUsers > 0 ?
                            Math.Round((decimal)summary.TotalVipUsers / summary.TotalUsers * 100, 2) : 0
                    }
                };

                return Ok(successRates);
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Success rates error: {ex.Message}");
                return StatusCode(500, new { message = "Başarı oranları alınırken hata oluştu" });
            }
        }

        // 📈 DASHBOARD ÖZET VERİLERİ
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardData()
        {
            try
            {
                var summary = await _analyticsService.GetAnalyticsSummaryAsync();
                var todayAnalytics = await _analyticsService.GetTodayAnalyticsAsync();
                var topUsers = await _analyticsService.GetTopUsersAsync(5);

                var dashboard = new
                {
                    summary,
                    todayAnalytics,
                    topUsers,
                    lastUpdated = DateTime.UtcNow
                };

                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Dashboard error: {ex.Message}");
                return StatusCode(500, new { message = "Dashboard verileri alınırken hata oluştu" });
            }
        }
    }
}