namespace VurduGololdu.API.Services
{
    public interface IEnvironmentService
    {
        string GetConnectionString();
        string GetJwtKey();
        string GetAwsAccessKey();
        string GetAwsSecretKey();
        string GetEmailUsername();
        string GetEmailPassword();
        string GetFrontendUrl();
        bool IsProduction();
        bool IsDevelopment();
    }

    public class EnvironmentService : IEnvironmentService
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public EnvironmentService(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public string GetConnectionString()
        {
            var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
            if (!string.IsNullOrEmpty(connectionString))
                return connectionString;

            // Fallback to appsettings.json
            return _configuration.GetConnectionString("DefaultConnection") ?? 
                   throw new InvalidOperationException("Database connection string not found");
        }

        public string GetJwtKey()
        {
            var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
            if (!string.IsNullOrEmpty(jwtKey))
                return jwtKey;

            // Fallback to appsettings.json
            return _configuration["Jwt:Key"] ?? 
                   throw new InvalidOperationException("JWT key not found");
        }

        public string GetAwsAccessKey()
        {
            var accessKey = Environment.GetEnvironmentVariable("AWS_S3_ACCESS_KEY");
            if (!string.IsNullOrEmpty(accessKey))
                return accessKey;

            return _configuration["AwsS3:AccessKey"] ?? 
                   throw new InvalidOperationException("AWS access key not found");
        }

        public string GetAwsSecretKey()
        {
            var secretKey = Environment.GetEnvironmentVariable("AWS_S3_SECRET_KEY");
            if (!string.IsNullOrEmpty(secretKey))
                return secretKey;

            return _configuration["AwsS3:SecretKey"] ?? 
                   throw new InvalidOperationException("AWS secret key not found");
        }

        public string GetEmailUsername()
        {
            var username = Environment.GetEnvironmentVariable("EMAIL_USERNAME");
            if (!string.IsNullOrEmpty(username))
                return username;

            return _configuration["EmailSettings:Username"] ?? string.Empty;
        }

        public string GetEmailPassword()
        {
            var password = Environment.GetEnvironmentVariable("EMAIL_PASSWORD");
            if (!string.IsNullOrEmpty(password))
                return password;

            return _configuration["EmailSettings:Password"] ?? string.Empty;
        }

        public string GetFrontendUrl()
        {
            var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL");
            if (!string.IsNullOrEmpty(frontendUrl))
                return frontendUrl;

            return _configuration["AppSettings:FrontendUrl"] ?? "https://localhost:3000";
        }

        public bool IsProduction()
        {
            return _environment.IsProduction();
        }

        public bool IsDevelopment()
        {
            return _environment.IsDevelopment();
        }
    }
} 