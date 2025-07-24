using System.ComponentModel.DataAnnotations;

namespace VurduGololdu.API.DTOs
{
    public class LoginDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterDto
    {
        [Required(ErrorMessage = "Ad alanı zorunludur")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Ad 2-100 karakter arasında olmalıdır")]
        [NoHtml]
        [SafeString]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Soyad alanı zorunludur")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Soyad 2-100 karakter arasında olmalıdır")]
        [NoHtml]
        [SafeString]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email alanı zorunludur")]
        [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz")]
        [StringLength(255, ErrorMessage = "Email adresi çok uzun")]
        public string Email { get; set; } = string.Empty;

        // Telefon opsiyonel, format serbest bırakıldı (UI'de mask yapılabilir)
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Şifre alanı zorunludur")]
        [StrongPassword]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre tekrarı zorunludur")]
        [Compare("Password", ErrorMessage = "Şifreler eşleşmiyor")]
        public string ConfirmPassword { get; set; } = string.Empty;

        // Captcha doğrulaması için
        [Required(ErrorMessage = "Captcha session ID zorunludur")]
        public string CaptchaSessionId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Captcha kodu zorunludur")]
        [StringLength(10, MinimumLength = 4, ErrorMessage = "Captcha kodu 4-10 karakter arasında olmalıdır")]
        public string CaptchaCode { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime TokenExpiry { get; set; }
        public UserDto User { get; set; } = null!;
    }

    public class RefreshTokenDto
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class ForgotPasswordDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordDto
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare("NewPassword")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class VerifyEmailDto
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsEmailVerified { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? VipExpiryDate { get; set; }
        public bool IsVipActive { get; set; }
        public bool IsBlocked { get; set; }
        public string? ProfileImageUrl { get; set; }
    }

    public class ProfileUpdateDto
    {
        [Required(ErrorMessage = "Ad alanı zorunludur")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Ad 2-100 karakter arasında olmalıdır")]
        [NoHtml]
        [SafeString]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Soyad alanı zorunludur")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Soyad 2-100 karakter arasında olmalıdır")]
        [NoHtml]
        [SafeString]
        public string LastName { get; set; } = string.Empty;

        // Telefon opsiyonel, format serbest bırakıldı (UI'de mask yapılabilir)
        public string? Phone { get; set; }
    }

    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "Mevcut şifre zorunludur")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Yeni şifre zorunludur")]
        [StrongPassword]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Yeni şifre tekrarı zorunludur")]
        [Compare("NewPassword", ErrorMessage = "Şifreler eşleşmiyor")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }

    public class CheckEmailDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class UserListDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsVipActive { get; set; }
        public bool IsBlocked { get; set; }
    }
}