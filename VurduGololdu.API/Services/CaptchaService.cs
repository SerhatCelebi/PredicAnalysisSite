using System.Text;
using VurduGololdu.API.Data;
using VurduGololdu.API.Models;
using VurduGololdu.API.Helpers;

namespace VurduGololdu.API.Services
{
    public interface ICaptchaService
    {
        Task<CaptchaVerification> GenerateCaptchaAsync(string sessionId, string? ipAddress = null, string? userAgent = null);
        Task<bool> VerifyCaptchaAsync(string sessionId, string code);
        Task CleanupExpiredCaptchasAsync();
    }

    public class CaptchaService : ICaptchaService
    {
        private readonly ApplicationDbContext _context;
        private readonly Random _random;

        public CaptchaService(ApplicationDbContext context)
        {
            _context = context;
            _random = new Random();
        }

        public async Task<CaptchaVerification> GenerateCaptchaAsync(string sessionId, string? ipAddress = null, string? userAgent = null)
        {
            try
            {
                DebugConsole.Log($"🔄 Generating captcha for session: {sessionId}");

                // Eski captcha'ları temizle
                var existingCaptcha = _context.CaptchaVerifications.FirstOrDefault(c => c.SessionId == sessionId);
                if (existingCaptcha != null)
                {
                    _context.CaptchaVerifications.Remove(existingCaptcha);
                }

                // Yeni kod oluştur
                var code = GenerateCaptchaCode();
                DebugConsole.Log($"📝 Generated code: {code}");

                // SVG captcha oluştur
                var imageBase64 = GenerateSvgCaptcha(code);
                DebugConsole.Log($"🎨 SVG captcha generated");

                var captcha = new CaptchaVerification
                {
                    SessionId = sessionId,
                    CaptchaCode = code,
                    CaptchaImageBase64 = imageBase64,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                    AttemptCount = 0,
                    MaxAttempts = 3,
                    IpAddress = ipAddress,
                    UserAgent = userAgent
                };

                _context.CaptchaVerifications.Add(captcha);
                await _context.SaveChangesAsync();

                DebugConsole.Log($"✅ Captcha saved to database for session: {sessionId}");
                return captcha;
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Error in GenerateCaptchaAsync: {ex.Message}");
                DebugConsole.Log($"🚨 Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<bool> VerifyCaptchaAsync(string sessionId, string code)
        {
            try
            {
                var captcha = _context.CaptchaVerifications.FirstOrDefault(c => c.SessionId == sessionId);

                if (captcha == null)
                {
                    DebugConsole.Log($"❌ Captcha not found for session: {sessionId}");
                    return false;
                }

                if (captcha.ExpiresAt < DateTime.UtcNow)
                {
                    DebugConsole.Log($"⏰ Captcha expired for session: {sessionId}");
                    _context.CaptchaVerifications.Remove(captcha);
                    await _context.SaveChangesAsync();
                    return false;
                }

                captcha.AttemptCount++;

                if (captcha.AttemptCount > captcha.MaxAttempts)
                {
                    DebugConsole.Log($"🚫 Max attempts exceeded for session: {sessionId}");
                    _context.CaptchaVerifications.Remove(captcha);
                    await _context.SaveChangesAsync();
                    return false;
                }

                var isValid = string.Equals(captcha.CaptchaCode, code, StringComparison.OrdinalIgnoreCase);

                if (isValid)
                {
                    DebugConsole.Log($"✅ Captcha verified successfully for session: {sessionId}");
                    _context.CaptchaVerifications.Remove(captcha);
                }
                else
                {
                    DebugConsole.Log($"❌ Invalid captcha code for session: {sessionId}");
                }

                await _context.SaveChangesAsync();
                return isValid;
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Error in VerifyCaptchaAsync: {ex.Message}");
                return false;
            }
        }

        public async Task CleanupExpiredCaptchasAsync()
        {
            try
            {
                var expiredCaptchas = _context.CaptchaVerifications
                    .Where(c => c.ExpiresAt < DateTime.UtcNow)
                    .ToList();

                if (expiredCaptchas.Any())
                {
                    _context.CaptchaVerifications.RemoveRange(expiredCaptchas);
                    await _context.SaveChangesAsync();
                    DebugConsole.Log($"🧹 Cleaned up {expiredCaptchas.Count} expired captchas");
                }
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Error in CleanupExpiredCaptchasAsync: {ex.Message}");
            }
        }

        private string GenerateCaptchaCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, 4)
                .Select(s => s[_random.Next(s.Length)]).ToArray());
        }

        private string GenerateSvgCaptcha(string code)
        {
            try
            {
                DebugConsole.Log($"🎨 Generating simple SVG captcha for code: {code}");

                // Basit SVG oluştur - minimal komplekslik
                var svg = $@"<svg width='200' height='80' xmlns='http://www.w3.org/2000/svg'>
  <rect width='200' height='80' fill='#f8f9fa' stroke='#333' stroke-width='2'/>
  <text x='50' y='50' font-family='Arial' font-size='24' font-weight='bold' fill='#333'>{code}</text>
</svg>";

                // SVG'yi Base64'e çevir
                var svgBytes = Encoding.UTF8.GetBytes(svg);
                var base64Svg = Convert.ToBase64String(svgBytes);

                DebugConsole.Log($"✅ Simple SVG captcha generated successfully");
                return $"data:image/svg+xml;base64,{base64Svg}";
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 SVG captcha generation error: {ex.Message}");

                // Son çare: Sadece kod
                DebugConsole.Log($"🔄 Fallback to plain text captcha");
                return $"CAPTCHA_CODE_{code}";
            }
        }
    }
}