using Microsoft.EntityFrameworkCore;
using VurduGololdu.API.Models;

namespace VurduGololdu.API.Data
{
      public class ApplicationDbContext : DbContext
      {
            public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
            {
            }

            public DbSet<User> Users { get; set; }
            public DbSet<Prediction> Predictions { get; set; }
            public DbSet<Comment> Comments { get; set; }
            public DbSet<Like> Likes { get; set; }
            public DbSet<PaymentNotification> PaymentNotifications { get; set; }
            public DbSet<ContactMessage> ContactMessages { get; set; }
            public DbSet<AuditLog> AuditLogs { get; set; }
            // EmailTemplate kaldırıldı
            public DbSet<NotificationLog> NotificationLogs { get; set; }
            public DbSet<CaptchaVerification> CaptchaVerifications { get; set; }
            public DbSet<DailyAnalytics> DailyAnalytics { get; set; }
            public DbSet<UserSuccessStats> UserSuccessStats { get; set; }
            public DbSet<DailyPost> DailyPosts { get; set; }
            public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                  base.OnModelCreating(modelBuilder);

                  // User entity configuration
                  modelBuilder.Entity<User>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasIndex(e => e.Email).IsUnique();
                        entity.Property(e => e.Email).HasMaxLength(255);
                        entity.Property(e => e.FirstName).HasMaxLength(100);
                        entity.Property(e => e.LastName).HasMaxLength(100);
                        entity.Property(e => e.Phone).HasMaxLength(20);

                        entity.HasOne(e => e.BlockedByUser)
                        .WithMany()
                        .HasForeignKey(e => e.BlockedByUserId)
                        .OnDelete(DeleteBehavior.NoAction);
                  });

                  // Prediction entity configuration
                  modelBuilder.Entity<Prediction>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasOne(e => e.User)
                        .WithMany(e => e.Predictions)
                        .HasForeignKey(e => e.UserId)
                        .OnDelete(DeleteBehavior.Restrict);
                  });

                  // Comment entity configuration
                  modelBuilder.Entity<Comment>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasOne(e => e.User)
                        .WithMany(e => e.Comments)
                        .HasForeignKey(e => e.UserId)
                        .OnDelete(DeleteBehavior.Restrict);

                        entity.HasOne(e => e.Prediction)
                        .WithMany(e => e.Comments)
                        .HasForeignKey(e => e.PredictionId)
                        .OnDelete(DeleteBehavior.NoAction);

                        entity.HasOne(e => e.DailyPost)
                        .WithMany(e => e.Comments)
                        .HasForeignKey(e => e.DailyPostId)
                        .OnDelete(DeleteBehavior.NoAction);

                        entity.HasOne(e => e.ApprovedByUser)
                        .WithMany()
                        .HasForeignKey(e => e.ApprovedByUserId)
                        .OnDelete(DeleteBehavior.NoAction);
                  });

                  // Like entity configuration
                  modelBuilder.Entity<Like>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasOne(e => e.User)
                        .WithMany(e => e.Likes)
                        .HasForeignKey(e => e.UserId)
                        .OnDelete(DeleteBehavior.NoAction);

                        entity.HasOne(e => e.Prediction)
                        .WithMany(e => e.Likes)
                        .HasForeignKey(e => e.PredictionId)
                        .OnDelete(DeleteBehavior.NoAction);

                        entity.HasOne(e => e.Comment)
                        .WithMany(e => e.Likes)
                        .HasForeignKey(e => e.CommentId)
                        .OnDelete(DeleteBehavior.NoAction);

                        entity.HasOne(e => e.DailyPost)
                        .WithMany(e => e.Likes)
                        .HasForeignKey(e => e.DailyPostId)
                        .OnDelete(DeleteBehavior.NoAction);

                        // Unique constraint: User can like each prediction/comment/dailypost only once
                        entity.HasIndex(e => new { e.UserId, e.PredictionId })
                        .IsUnique()
                        .HasFilter("[PredictionId] IS NOT NULL");

                        entity.HasIndex(e => new { e.UserId, e.CommentId })
                        .IsUnique()
                        .HasFilter("[CommentId] IS NOT NULL");

                        entity.HasIndex(e => new { e.UserId, e.DailyPostId })
                        .IsUnique()
                        .HasFilter("[DailyPostId] IS NOT NULL");
                  });

                  // PaymentNotification entity configuration
                  modelBuilder.Entity<PaymentNotification>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasOne(e => e.User)
                        .WithMany(e => e.PaymentNotifications)
                        .HasForeignKey(e => e.UserId)
                        .OnDelete(DeleteBehavior.Restrict);

                        entity.HasOne(e => e.ProcessedByUser)
                        .WithMany()
                        .HasForeignKey(e => e.ProcessedByUserId)
                        .OnDelete(DeleteBehavior.NoAction);

                        entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                        entity.Property(e => e.SenderName).HasMaxLength(100);
                        entity.Property(e => e.BankName).HasMaxLength(50);
                        entity.Property(e => e.TransactionReference).HasMaxLength(100);
                        entity.Property(e => e.Note).HasMaxLength(500);
                  });

                  // ContactMessage entity configuration
                  modelBuilder.Entity<ContactMessage>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasOne(e => e.User)
                        .WithMany()
                        .HasForeignKey(e => e.UserId)
                        .OnDelete(DeleteBehavior.NoAction);

                        entity.HasOne(e => e.RepliedByUser)
                        .WithMany()
                        .HasForeignKey(e => e.RepliedByUserId)
                        .OnDelete(DeleteBehavior.NoAction);

                        entity.Property(e => e.Name).HasMaxLength(100);
                        entity.Property(e => e.Email).HasMaxLength(255);
                        entity.Property(e => e.Phone).HasMaxLength(20);
                        entity.Property(e => e.Subject).HasMaxLength(200);
                  });

                  // AuditLog entity configuration
                  modelBuilder.Entity<AuditLog>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasOne(e => e.User)
                        .WithMany()
                        .HasForeignKey(e => e.UserId)
                        .OnDelete(DeleteBehavior.NoAction);

                        entity.Property(e => e.Action).HasMaxLength(100);
                        entity.Property(e => e.Entity).HasMaxLength(50);
                        entity.Property(e => e.UserEmail).HasMaxLength(100);
                        entity.Property(e => e.UserName).HasMaxLength(200);
                        entity.Property(e => e.IpAddress).HasMaxLength(45);
                        entity.Property(e => e.UserAgent).HasMaxLength(500);
                        entity.Property(e => e.Endpoint).HasMaxLength(200);
                        entity.Property(e => e.HttpMethod).HasMaxLength(10);
                        entity.Property(e => e.ErrorMessage).HasMaxLength(1000);

                        entity.HasIndex(e => e.CreatedAt);
                        entity.HasIndex(e => e.UserId);
                        entity.HasIndex(e => e.Action);
                        entity.HasIndex(e => e.IpAddress);
                  });

                  // EmailTemplate configuration kaldırıldı

                  // NotificationLog entity configuration
                  modelBuilder.Entity<NotificationLog>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasOne(e => e.User)
                        .WithMany(e => e.NotificationLogs)
                        .HasForeignKey(e => e.UserId)
                        .OnDelete(DeleteBehavior.NoAction);

                        entity.Property(e => e.Type).HasMaxLength(50);
                        entity.Property(e => e.Category).HasMaxLength(100);
                        entity.Property(e => e.Subject).HasMaxLength(200);
                        entity.Property(e => e.Status).HasMaxLength(100);
                        entity.Property(e => e.RelatedLink).HasMaxLength(500);
                        entity.Property(e => e.ActorFirstName).HasMaxLength(100);
                        entity.Property(e => e.ActorLastName).HasMaxLength(100);
                        entity.Property(e => e.ActorProfileImageUrl).HasMaxLength(500);

                        entity.HasIndex(e => e.UserId);
                        entity.HasIndex(e => e.Type);
                        entity.HasIndex(e => e.Category);
                        entity.HasIndex(e => e.Status);
                        entity.HasIndex(e => e.CreatedAt);
                        entity.HasIndex(e => e.ActorUserId);
                  });

                  // Prediction entity configuration - Update for new fields
                  modelBuilder.Entity<Prediction>(entity =>
                  {
                        entity.HasOne(e => e.PinnedByUser)
                        .WithMany()
                        .HasForeignKey(e => e.PinnedByUserId)
                        .OnDelete(DeleteBehavior.NoAction);

                        entity.HasIndex(e => e.Status);
                        entity.HasIndex(e => e.IsFeatured);
                        entity.HasIndex(e => e.IsPinned);
                        entity.HasIndex(e => e.IsShared);
                        entity.HasIndex(e => e.ResultDate);
                  });

                  // CaptchaVerification entity configuration
                  modelBuilder.Entity<CaptchaVerification>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.Property(e => e.SessionId).HasMaxLength(100);
                        entity.Property(e => e.CaptchaCode).HasMaxLength(10);
                        entity.Property(e => e.CaptchaImageBase64).HasMaxLength(500);
                        entity.Property(e => e.IpAddress).HasMaxLength(45);
                        entity.Property(e => e.UserAgent).HasMaxLength(500);

                        entity.HasIndex(e => e.SessionId);
                        entity.HasIndex(e => e.CreatedAt);
                        entity.HasIndex(e => e.ExpiresAt);
                        entity.HasIndex(e => e.IpAddress);
                  });

                  // DailyAnalytics entity configuration
                  modelBuilder.Entity<DailyAnalytics>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.Property(e => e.OverallSuccessRate).HasColumnType("decimal(5,2)");
                        entity.Property(e => e.VipSuccessRate).HasColumnType("decimal(5,2)");
                        entity.Property(e => e.NormalUserSuccessRate).HasColumnType("decimal(5,2)");
                        entity.Property(e => e.DailyRevenue).HasColumnType("decimal(18,2)");
                        entity.Property(e => e.TotalRevenue).HasColumnType("decimal(18,2)");

                        entity.HasIndex(e => e.Date).IsUnique();
                        entity.HasIndex(e => e.CreatedAt);
                  });

                  // UserSuccessStats entity configuration
                  modelBuilder.Entity<UserSuccessStats>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasOne(e => e.User)
                        .WithMany()
                        .HasForeignKey(e => e.UserId)
                        .OnDelete(DeleteBehavior.Cascade);

                        entity.Property(e => e.SuccessRate).HasColumnType("decimal(5,2)");

                        entity.HasIndex(e => e.UserId).IsUnique();
                        entity.HasIndex(e => e.SuccessRate);
                        entity.HasIndex(e => e.TotalPredictions);
                        entity.HasIndex(e => e.CurrentStreak);
                        entity.HasIndex(e => e.BestStreak);
                  });

                  // DailyPost entity configuration
                  modelBuilder.Entity<DailyPost>(entity =>
                  {
                        entity.HasKey(e => e.Id);
                        entity.HasOne(e => e.Admin)
                        .WithMany()
                        .HasForeignKey(e => e.AdminId)
                        .OnDelete(DeleteBehavior.Restrict);

                        entity.Property(e => e.Title).HasMaxLength(200);
                        entity.Property(e => e.Content).HasMaxLength(2000);
                        entity.Property(e => e.ImageUrl).HasMaxLength(500);
                        entity.Property(e => e.Category).HasMaxLength(100);
                        entity.Property(e => e.Tags).HasMaxLength(500);

                        entity.HasIndex(e => e.CreatedAt);
                        entity.HasIndex(e => e.Category);
                        entity.HasIndex(e => e.IsPublished);
                        entity.HasIndex(e => e.IsFeatured);
                        entity.HasIndex(e => e.AdminId);
                  });
            }
      }
}