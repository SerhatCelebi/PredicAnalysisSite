using VurduGololdu.API.Models;

namespace VurduGololdu.API.DTOs
{
    public class AuditLogDto
    {
        public int Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public int? UserId { get; set; }
        public string? UserEmail { get; set; }
        public string? UserName { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string? UserAgent { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = string.Empty;
        public string? RequestData { get; set; }
        public string? ResponseData { get; set; }
        public int StatusCode { get; set; }
        public long Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public AuditLogLevel Level { get; set; }
    }

    public class AuditLogFilterDto
    {
        public string? Action { get; set; }
        public string? Entity { get; set; }
        public int? UserId { get; set; }
        public string? UserEmail { get; set; }
        public string? IpAddress { get; set; }
        public AuditLogLevel? Level { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Page { get; set; } = 1;
        public int Size { get; set; } = 20;
        public string? SortBy { get; set; } = "CreatedAt";
        public bool SortDescending { get; set; } = true;
    }

    public class AuditLogSummaryDto
    {
        public int TotalLogs { get; set; }
        public int TotalUsers { get; set; }
        public int TotalActions { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public List<ActionSummaryDto> TopActions { get; set; } = new();
        public List<UserSummaryDto> TopUsers { get; set; } = new();
        public List<IpSummaryDto> TopIps { get; set; } = new();
    }

    public class ActionSummaryDto
    {
        public string Action { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class UserSummaryDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class IpSummaryDto
    {
        public string IpAddress { get; set; } = string.Empty;
        public int Count { get; set; }
        public int UniqueUsers { get; set; }
    }
} 