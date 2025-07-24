using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace VurduGololdu.API.DTOs
{
    public class NoHtmlAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null) return true;
            
            var input = value.ToString();
            if (string.IsNullOrEmpty(input)) return true;
            
            // HTML tag'leri kontrol et
            var htmlPattern = @"<[^>]*>";
            return !Regex.IsMatch(input, htmlPattern);
        }

        public override string FormatErrorMessage(string name)
        {
            return $"{name} alanında HTML tag'leri kullanılamaz.";
        }
    }

    public class NoScriptAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null) return true;
            
            var input = value.ToString()?.ToLower();
            if (string.IsNullOrEmpty(input)) return true;
            
            // Tehlikeli script pattern'leri
            var dangerousPatterns = new[]
            {
                "javascript:", "<script", "onload=", "onerror=", "onclick=",
                "onmouseover=", "alert(", "document.cookie", "eval(",
                "expression(", "vbscript:", "data:text/html"
            };
            
            return !dangerousPatterns.Any(pattern => input.Contains(pattern));
        }

        public override string FormatErrorMessage(string name)
        {
            return $"{name} alanında güvenlik riski oluşturan içerik tespit edildi.";
        }
    }

    public class SafeStringAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null) return true;
            
            var input = value.ToString();
            if (string.IsNullOrEmpty(input)) return true;
            
            // SQL injection pattern'leri
            var sqlPatterns = new[]
            {
                "union select", "drop table", "insert into", "delete from",
                "update set", "exec(", "execute(", "sp_", "xp_",
                "'; --", "' or '1'='1", "' or 1=1", "admin'--",
                "/*", "*/", "@@", "char(", "nchar(", "varchar(",
                "nvarchar(", "alter(", "begin(", "cast(", "create(",
                "cursor(", "declare(", "end(", "fetch(", "kill("
            };
            
            return !sqlPatterns.Any(pattern => 
                input.ToLower().Contains(pattern.ToLower()));
        }

        public override string FormatErrorMessage(string name)
        {
            return $"{name} alanında güvenlik riski oluşturan karakter dizisi tespit edildi.";
        }
    }

    public class InternationalPhoneAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null) return true;
            
            var phone = value.ToString();
            if (string.IsNullOrEmpty(phone)) return true;
            
            // Uluslararası telefon numarası formatı (E.164 gibi)
            // Örnekler: +905321234567, 905321234567, 5321234567
            // Basit bir regex: başında isteğe bağlı + ve ardından 7-15 arası rakam
            var phonePattern = @"^\+?[1-9]\d{6,14}$";
            return Regex.IsMatch(phone, phonePattern);
        }

        public override string FormatErrorMessage(string name)
        {
            return $"{name} geçerli bir uluslararası telefon numarası olmalıdır.";
        }
    }

    public class StrongPasswordAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null) return false;
            
            var password = value.ToString();
            if (string.IsNullOrEmpty(password)) return false;
            
            // Güçlü şifre kriterleri
            var hasMinLength = password.Length >= 8;
            var hasUpper = password.Any(char.IsUpper);
            var hasLower = password.Any(char.IsLower);
            var hasDigit = password.Any(char.IsDigit);
            var hasSpecial = password.Any(ch => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(ch));
            
            return hasMinLength && hasUpper && hasLower && hasDigit && hasSpecial;
        }

        public override string FormatErrorMessage(string name)
        {
            return $"{name} en az 8 karakter olmalı ve büyük harf, küçük harf, rakam ve özel karakter içermelidir.";
        }
    }
} 