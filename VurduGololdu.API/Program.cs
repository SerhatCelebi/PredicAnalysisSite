using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using VurduGololdu.API.Data;
using VurduGololdu.API.Models;
using VurduGololdu.API.Services;
using DotNetEnv;
using VurduGololdu.API.Helpers;

// Load environment variables from .env file
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Environment variables'ları appsettings.json'a bind etme
var configuration = builder.Configuration;

// Database connection string'i environment variables'tan oluştur
var dbServer = Environment.GetEnvironmentVariable("DB_SERVER");
var dbName = Environment.GetEnvironmentVariable("DB_NAME");
var dbTrustedConnection = Environment.GetEnvironmentVariable("DB_TRUSTED_CONNECTION");
var dbTrustServerCertificate = Environment.GetEnvironmentVariable("DB_TRUST_SERVER_CERTIFICATE");
var dbMultipleActiveResultSets = Environment.GetEnvironmentVariable("DB_MULTIPLE_ACTIVE_RESULT_SETS");
var dbEncrypt = Environment.GetEnvironmentVariable("DB_ENCRYPT");

var connectionString = $"Server={dbServer};Database={dbName};Trusted_Connection={dbTrustedConnection};TrustServerCertificate={dbTrustServerCertificate};MultipleActiveResultSets={dbMultipleActiveResultSets};Encrypt={dbEncrypt}";

// Configuration'a connection string'i ekle
configuration["ConnectionStrings:DefaultConnection"] = connectionString;

// JWT ayarlarını environment variables'tan al
configuration["Jwt:Key"] = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
configuration["Jwt:Issuer"] = Environment.GetEnvironmentVariable("JWT_ISSUER");
configuration["Jwt:Audience"] = Environment.GetEnvironmentVariable("JWT_AUDIENCE");
configuration["Jwt:ExpireMinutes"] = Environment.GetEnvironmentVariable("JWT_EXPIRE_MINUTES");
configuration["Jwt:RefreshTokenExpireDays"] = Environment.GetEnvironmentVariable("JWT_REFRESH_TOKEN_EXPIRE_DAYS");


configuration["AwsS3:BucketName"] = Environment.GetEnvironmentVariable("AWS_S3_BUCKET_NAME");
configuration["AwsS3:Region"] = Environment.GetEnvironmentVariable("AWS_S3_REGION") ;
configuration["AwsS3:AccessKey"] = Environment.GetEnvironmentVariable("AWS_S3_ACCESS_KEY");
configuration["AwsS3:SecretKey"] = Environment.GetEnvironmentVariable("AWS_S3_SECRET_KEY");
configuration["AwsS3:BaseUrl"] = Environment.GetEnvironmentVariable("AWS_S3_BASE_URL");

// Email ayarları kaldırıldı

// App ayarlarını environment variables'tan al
configuration["AppSettings:FrontendUrl"] = Environment.GetEnvironmentVariable("FRONTEND_URL");

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication - Debug ve güvenlik kontrolü
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSettings["Key"];

// JWT Key kontrolü ve debug
if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException("JWT Key cannot be null or empty. Check your configuration.");
}



var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(5), // 5 dakika tolerans
        // Token formatını kontrol et
        RequireExpirationTime = true,
        RequireSignedTokens = true
    };

    // JWT Bearer event'leri - Debug için
    x.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                context.Response.Headers.Append("Token-Expired", "true");
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            return Task.CompletedTask;
        },
        OnMessageReceived = context =>
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
            {
            }
            else if (authHeader.StartsWith("Bearer "))
            {
            }
            else
            {
                // Bearer prefix yoksa ekle
                context.Token = authHeader; // Token'ı direkt set et
            }
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Rol hiyerarşisi: SuperAdmin -> Admin erişimi
builder.Services.AddTransient<Microsoft.AspNetCore.Authentication.IClaimsTransformation, VurduGololdu.API.Extensions.RoleHierarchyClaimsTransformation>();

// Memory Cache for predictions
builder.Services.AddMemoryCache();

// Services
builder.Services.AddScoped<IEnvironmentService, EnvironmentService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IS3Service, S3Service>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ISecurityService, SecurityService>();
// Email service disabled - builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ICacheService, CacheService>();
// Push notification service removed - website only
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICaptchaService, CaptchaService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddHttpContextAccessor();

// Background Services
builder.Services.AddHostedService<CleanupBackgroundService>();

// CORS - Production için güvenli konfigürasyon
builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionPolicy", policy =>
    {
        policy.WithOrigins("https://vurdugololdu.com", "https://www.vurdugololdu.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });

    // Development için ayrı policy
    options.AddPolicy("DevelopmentPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Controllers
builder.Services.AddControllers();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VurduGololdu API",
        Version = "v1",
        Description = "VurduGololdu tahmin sitesi API'si"
    });

    // JWT Authorization için Swagger yapılandırması
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "VurduGololdu API V1");
        c.RoutePrefix = "swagger"; // Swagger UI'yi /swagger path'inde göster
    });
}

// HTTPS Enforcement - Production için zorunlu
if (!app.Environment.IsDevelopment())
{
    app.UseHsts(); // HTTP Strict Transport Security
    app.UseHttpsRedirection();
}
// Development'ta HTTPS redirect yapmıyoruz

// Environment'a göre CORS policy kullan
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevelopmentPolicy");
}
else
{
    app.UseCors("ProductionPolicy");
}

// Security headers middleware - en önce
app.UseMiddleware<VurduGololdu.API.Middleware.SecurityHeadersMiddleware>();

// Hata yakalama middleware'i (en başta)
app.UseMiddleware<VurduGololdu.API.Middleware.ErrorHandlingMiddleware>();

// Security middleware - en önce çalışmalı
app.UseMiddleware<VurduGololdu.API.Middleware.SecurityMiddleware>();

// Performance monitoring middleware
app.UseMiddleware<VurduGololdu.API.Middleware.PerformanceMiddleware>();

// Audit log middleware'ı authentication'dan önce ekle
app.UseMiddleware<VurduGololdu.API.Middleware.AuditLogMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Database Migration and Initialization
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        context.Database.EnsureCreated();

        // Admin kullanıcısı yoksa oluştur
        if (!context.Users.Any(u => u.Role == UserRole.Admin))
        {
            var adminUser = new User
            {
                FirstName = "Admin",
                LastName = "User",
                Email = "admin@vurdugololdu.com",
                Phone = "05551234567",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                Role = UserRole.Admin,
                IsEmailVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(adminUser);
            context.SaveChanges();
        }

        // Email templates kaldırıldı
        logger.LogInformation("Email system removed - notifications disabled");

        // Firebase removed - website only version
        logger.LogInformation("Email-only notification system initialized");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration ve initialization sırasında hata oluştu");
    }
}

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
