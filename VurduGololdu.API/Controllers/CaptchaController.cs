using Microsoft.AspNetCore.Mvc;
using VurduGololdu.API.DTOs;
using VurduGololdu.API.Services;
using VurduGololdu.API.Helpers;

namespace VurduGololdu.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CaptchaController : ControllerBase
    {
        private readonly ICaptchaService _captchaService;

        public CaptchaController(ICaptchaService captchaService)
        {
            _captchaService = captchaService;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateCaptcha([FromBody] CaptchaRequestDto dto)
        {
            try
            {
                DebugConsole.Log($"🔄 Captcha generation started for session: {dto.SessionId}");

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

                DebugConsole.Log($"📍 IP: {ipAddress}, User-Agent: {userAgent?.Substring(0, Math.Min(50, userAgent?.Length ?? 0))}...");

                var captcha = await _captchaService.GenerateCaptchaAsync(dto.SessionId, ipAddress, userAgent);

                DebugConsole.Log($"✅ Captcha generated successfully for session: {dto.SessionId}");

                return Ok(new CaptchaResponseDto
                {
                    SessionId = captcha.SessionId,
                    ImageBase64 = captcha.ImageBase64.StartsWith("data:") ? captcha.ImageBase64 : $"data:image/png;base64,{captcha.ImageBase64}",
                    ExpiresAt = captcha.ExpiresAt
                });
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Captcha generation error: {ex.Message}");
                DebugConsole.Log($"🚨 Exception Type: {ex.GetType().Name}");
                DebugConsole.Log($"🚨 Inner Exception: {ex.InnerException?.Message}");
                DebugConsole.Log($"🚨 Stack Trace: {ex.StackTrace}");

                return StatusCode(500, new
                {
                    message = "Captcha oluşturulurken hata oluştu",
                    error = ex.Message,
                    type = ex.GetType().Name,
                    sessionId = dto.SessionId
                });
            }
        }

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyCaptcha([FromBody] CaptchaVerifyDto dto)
        {
            try
            {
                var isValid = await _captchaService.VerifyCaptchaAsync(dto.SessionId, dto.CaptchaCode);

                if (isValid)
                {
                    return Ok(new { message = "Captcha doğrulandı", isValid = true });
                }
                else
                {
                    return BadRequest(new { message = "Captcha kodu yanlış veya süresi dolmuş", isValid = false });
                }
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Captcha verification error: {ex.Message}");
                return StatusCode(500, new { message = "Captcha doğrulanırken hata oluştu" });
            }
        }

        [HttpPost("cleanup")]
        public async Task<IActionResult> CleanupExpiredCaptchas()
        {
            try
            {
                await _captchaService.CleanupExpiredCaptchasAsync();
                return Ok(new { message = "Süresi dolmuş captcha'lar temizlendi" });
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"🚨 Captcha cleanup error: {ex.Message}");
                return StatusCode(500, new { message = "Captcha temizleme sırasında hata oluştu" });
            }
        }
    }
}