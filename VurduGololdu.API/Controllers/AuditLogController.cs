using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VurduGololdu.API.Data;
using VurduGololdu.API.DTOs;
using VurduGololdu.API.Models;
using VurduGololdu.API.Services;

namespace VurduGololdu.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AuditLogController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditLogService;

        public AuditLogController(ApplicationDbContext context, IAuditLogService auditLogService)
        {
            _context = context;
            _auditLogService = auditLogService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResponse<AuditLogDto>>> GetAuditLogs([FromQuery] AuditLogFilterDto filter)
        {
            await _auditLogService.LogAsync("ViewAuditLogs", "AuditLog", requestData: filter);

            var query = _context.AuditLogs.AsQueryable();

            // Filtreleri uygula
            if (!string.IsNullOrEmpty(filter.Action))
                query = query.Where(x => x.Action.Contains(filter.Action));

            if (!string.IsNullOrEmpty(filter.Entity))
                query = query.Where(x => x.Entity.Contains(filter.Entity));

            if (filter.UserId.HasValue)
                query = query.Where(x => x.UserId == filter.UserId);

            if (!string.IsNullOrEmpty(filter.UserEmail))
                query = query.Where(x => x.UserEmail != null && x.UserEmail.Contains(filter.UserEmail));

            if (!string.IsNullOrEmpty(filter.IpAddress))
                query = query.Where(x => x.IpAddress.Contains(filter.IpAddress));

            if (filter.Level.HasValue)
                query = query.Where(x => x.Level == filter.Level);

            if (filter.StartDate.HasValue)
                query = query.Where(x => x.CreatedAt >= filter.StartDate.Value);

            if (filter.EndDate.HasValue)
                query = query.Where(x => x.CreatedAt <= filter.EndDate.Value);

            // Sıralama
            switch (filter.SortBy?.ToLower())
            {
                case "action":
                    query = filter.SortDescending ? query.OrderByDescending(x => x.Action) : query.OrderBy(x => x.Action);
                    break;
                case "entity":
                    query = filter.SortDescending ? query.OrderByDescending(x => x.Entity) : query.OrderBy(x => x.Entity);
                    break;
                case "username":
                    query = filter.SortDescending ? query.OrderByDescending(x => x.UserName) : query.OrderBy(x => x.UserName);
                    break;
                case "ipaddress":
                    query = filter.SortDescending ? query.OrderByDescending(x => x.IpAddress) : query.OrderBy(x => x.IpAddress);
                    break;
                case "level":
                    query = filter.SortDescending ? query.OrderByDescending(x => x.Level) : query.OrderBy(x => x.Level);
                    break;
                default:
                    query = filter.SortDescending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt);
                    break;
            }

            var totalCount = await query.CountAsync();
            var logs = await query
                .Skip((filter.Page - 1) * filter.Size)
                .Take(filter.Size)
                .Select(x => new AuditLogDto
                {
                    Id = x.Id,
                    Action = x.Action,
                    Entity = x.Entity,
                    EntityId = x.EntityId,
                    UserId = x.UserId,
                    UserEmail = x.UserEmail,
                    UserName = x.UserName,
                    IpAddress = x.IpAddress,
                    UserAgent = x.UserAgent,
                    Endpoint = x.Endpoint,
                    HttpMethod = x.HttpMethod,
                    RequestData = x.RequestData,
                    ResponseData = x.ResponseData,
                    StatusCode = x.StatusCode,
                    Duration = x.Duration,
                    ErrorMessage = x.ErrorMessage,
                    CreatedAt = x.CreatedAt,
                    Level = x.Level
                })
                .ToListAsync();

            return Ok(new PagedResponse<AuditLogDto>
            {
                Data = logs,
                TotalCount = totalCount,
                Page = filter.Page,
                Size = filter.Size,
                TotalPages = (int)Math.Ceiling((double)totalCount / filter.Size)
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AuditLogDto>> GetAuditLog(int id)
        {
            var auditLog = await _context.AuditLogs
                .FirstOrDefaultAsync(x => x.Id == id);

            if (auditLog == null)
                return NotFound(new { message = "Audit log bulunamadı." });

            await _auditLogService.LogAsync("ViewAuditLog", "AuditLog", id);

            var dto = new AuditLogDto
            {
                Id = auditLog.Id,
                Action = auditLog.Action,
                Entity = auditLog.Entity,
                EntityId = auditLog.EntityId,
                UserId = auditLog.UserId,
                UserEmail = auditLog.UserEmail,
                UserName = auditLog.UserName,
                IpAddress = auditLog.IpAddress,
                UserAgent = auditLog.UserAgent,
                Endpoint = auditLog.Endpoint,
                HttpMethod = auditLog.HttpMethod,
                RequestData = auditLog.RequestData,
                ResponseData = auditLog.ResponseData,
                StatusCode = auditLog.StatusCode,
                Duration = auditLog.Duration,
                ErrorMessage = auditLog.ErrorMessage,
                CreatedAt = auditLog.CreatedAt,
                Level = auditLog.Level
            };

            return Ok(dto);
        }

        [HttpGet("summary")]
        public async Task<ActionResult<AuditLogSummaryDto>> GetAuditLogSummary([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            await _auditLogService.LogAsync("ViewAuditLogSummary", "AuditLog", requestData: new { startDate, endDate });

            var query = _context.AuditLogs.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(x => x.CreatedAt >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(x => x.CreatedAt <= endDate.Value);

            var totalLogs = await query.CountAsync();
            var totalUsers = await query.Where(x => x.UserId.HasValue).Select(x => x.UserId).Distinct().CountAsync();
            var totalActions = await query.Select(x => x.Action).Distinct().CountAsync();
            var errorCount = await query.Where(x => x.Level == AuditLogLevel.Error || x.Level == AuditLogLevel.Critical).CountAsync();
            var warningCount = await query.Where(x => x.Level == AuditLogLevel.Warning).CountAsync();

            var topActions = await query
                .GroupBy(x => x.Action)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new ActionSummaryDto
                {
                    Action = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var topUsers = await query
                .Where(x => x.UserId.HasValue && x.UserName != null)
                .GroupBy(x => new { x.UserId, x.UserName, x.UserEmail })
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new UserSummaryDto
                {
                    UserId = g.Key.UserId!.Value,
                    UserName = g.Key.UserName ?? "",
                    UserEmail = g.Key.UserEmail ?? "",
                    Count = g.Count()
                })
                .ToListAsync();

            var topIps = await query
                .GroupBy(x => x.IpAddress)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new IpSummaryDto
                {
                    IpAddress = g.Key,
                    Count = g.Count(),
                    UniqueUsers = g.Where(x => x.UserId.HasValue).Select(x => x.UserId).Distinct().Count()
                })
                .ToListAsync();

            var summary = new AuditLogSummaryDto
            {
                TotalLogs = totalLogs,
                TotalUsers = totalUsers,
                TotalActions = totalActions,
                ErrorCount = errorCount,
                WarningCount = warningCount,
                TopActions = topActions,
                TopUsers = topUsers,
                TopIps = topIps
            };

            return Ok(summary);
        }

        [HttpDelete("cleanup")]
        public async Task<IActionResult> CleanupOldLogs([FromQuery] int daysToKeep = 90)
        {
            await _auditLogService.LogAsync("CleanupAuditLogs", "AuditLog", requestData: new { daysToKeep });

            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
            var logsToDelete = await _context.AuditLogs
                .Where(x => x.CreatedAt < cutoffDate)
                .CountAsync();

            if (logsToDelete == 0)
                return Ok(new { message = "Silinecek eski log kaydı bulunamadı.", deletedCount = 0 });

            await _context.AuditLogs
                .Where(x => x.CreatedAt < cutoffDate)
                .ExecuteDeleteAsync();

            await _auditLogService.LogAsync("CleanupAuditLogsCompleted", "AuditLog", 
                requestData: new { daysToKeep, deletedCount = logsToDelete });

            return Ok(new { message = $"{logsToDelete} adet eski log kaydı başarıyla silindi.", deletedCount = logsToDelete });
        }

        [HttpGet("actions")]
        public async Task<ActionResult<List<string>>> GetAvailableActions()
        {
            var actions = await _context.AuditLogs
                .Select(x => x.Action)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            return Ok(actions);
        }

        [HttpGet("entities")]
        public async Task<ActionResult<List<string>>> GetAvailableEntities()
        {
            var entities = await _context.AuditLogs
                .Select(x => x.Entity)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            return Ok(entities);
        }
    }
} 