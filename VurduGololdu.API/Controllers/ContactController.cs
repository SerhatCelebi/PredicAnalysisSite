using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VurduGololdu.API.Data;
using VurduGololdu.API.DTOs;
using VurduGololdu.API.Models;

namespace VurduGololdu.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ContactController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(ContactMessageDto dto)
        {
            var currentUserId = GetCurrentUserId();

            var message = new ContactMessage
            {
                Name = dto.Name,
                Email = dto.Email,
                Phone = dto.Phone,
                Subject = dto.Subject,
                Message = dto.Message,
                UserId = currentUserId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                IsReplied = false
            };

            _context.ContactMessages.Add(message);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Mesajınız başarıyla gönderildi. En kısa sürede size dönüş yapılacak." });
        }

        [HttpGet("my-messages")]
        [Authorize]
        public async Task<IActionResult> GetMyMessages([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userId = GetCurrentUserId()!.Value;

            var query = _context.ContactMessages
                .Where(m => m.UserId == userId);

            var totalCount = await query.CountAsync();
            var messages = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    Id = m.Id,
                    Subject = m.Subject,
                    Message = m.Message,
                    AdminReply = m.AdminReply,
                    IsRead = m.IsRead,
                    IsReplied = m.IsReplied,
                    CreatedAt = m.CreatedAt,
                    RepliedAt = m.RepliedAt
                })
                .ToListAsync();

            return Ok(new
            {
                messages,
                totalCount,
                currentPage = page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllMessages(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10,
            [FromQuery] bool onlyUnread = false)
        {
            var query = _context.ContactMessages
                .Include(m => m.User)
                .Include(m => m.RepliedByUser)
                .AsQueryable();

            if (onlyUnread)
            {
                query = query.Where(m => !m.IsRead);
            }

            var totalCount = await query.CountAsync();
            var messages = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    Id = m.Id,
                    Name = m.Name,
                    Email = m.Email,
                    Phone = m.Phone,
                    Subject = m.Subject,
                    Message = m.Message,
                    AdminReply = m.AdminReply,
                    IsRead = m.IsRead,
                    IsReplied = m.IsReplied,
                    CreatedAt = m.CreatedAt,
                    ReadAt = m.ReadAt,
                    RepliedAt = m.RepliedAt,
                    User = m.User != null ? new
                    {
                        Id = m.User.Id,
                        FirstName = m.User.FirstName,
                        LastName = m.User.LastName,
                        Email = m.User.Email
                    } : null,
                    RepliedByUser = m.RepliedByUser != null ? new
                    {
                        Id = m.RepliedByUser.Id,
                        FirstName = m.RepliedByUser.FirstName,
                        LastName = m.RepliedByUser.LastName
                    } : null
                })
                .ToListAsync();

            return Ok(new
            {
                messages,
                totalCount,
                currentPage = page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetMessage(int id)
        {
            var message = await _context.ContactMessages
                .Include(m => m.User)
                .Include(m => m.RepliedByUser)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (message == null)
            {
                return NotFound("Mesaj bulunamadı");
            }

            // Mesajı okundu olarak işaretle
            if (!message.IsRead)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            var messageDto = new
            {
                Id = message.Id,
                Name = message.Name,
                Email = message.Email,
                Phone = message.Phone,
                Subject = message.Subject,
                Message = message.Message,
                AdminReply = message.AdminReply,
                IsRead = message.IsRead,
                IsReplied = message.IsReplied,
                CreatedAt = message.CreatedAt,
                ReadAt = message.ReadAt,
                RepliedAt = message.RepliedAt,
                User = message.User != null ? new
                {
                    Id = message.User.Id,
                    FirstName = message.User.FirstName,
                    LastName = message.User.LastName,
                    Email = message.User.Email,
                    Phone = message.User.Phone
                } : null,
                RepliedByUser = message.RepliedByUser != null ? new
                {
                    Id = message.RepliedByUser.Id,
                    FirstName = message.RepliedByUser.FirstName,
                    LastName = message.RepliedByUser.LastName
                } : null
            };

            return Ok(messageDto);
        }

        [HttpPost("{id}/reply")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReplyToMessage(int id, [FromBody] string reply)
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message == null)
            {
                return NotFound("Mesaj bulunamadı");
            }

            var adminUserId = GetCurrentUserId()!.Value;

            message.AdminReply = reply;
            message.IsReplied = true;
            message.RepliedAt = DateTime.UtcNow;
            message.RepliedByUserId = adminUserId;

            if (!message.IsRead)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Burada email ile kullanıcıya cevap gönderme servisi çağrılabilir
            // await _emailService.SendReplyEmailAsync(message.Email, message.Subject, reply);

            return Ok(new { message = "Cevap başarıyla gönderildi" });
        }

        [HttpPost("{id}/mark-read")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message == null)
            {
                return NotFound("Mesaj bulunamadı");
            }

            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Mesaj okundu olarak işaretlendi" });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message == null)
            {
                return NotFound("Mesaj bulunamadı");
            }

            _context.ContactMessages.Remove(message);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Mesaj başarıyla silindi" });
        }

        [HttpGet("stats")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetContactStats()
        {
            var totalMessages = await _context.ContactMessages.CountAsync();
            var unreadMessages = await _context.ContactMessages.CountAsync(m => !m.IsRead);
            var repliedMessages = await _context.ContactMessages.CountAsync(m => m.IsReplied);
            var todayMessages = await _context.ContactMessages
                .CountAsync(m => m.CreatedAt.Date == DateTime.Today);

            return Ok(new
            {
                totalMessages,
                unreadMessages,
                repliedMessages,
                todayMessages,
                replyRate = totalMessages > 0 ? Math.Round((double)repliedMessages / totalMessages * 100, 2) : 0
            });
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : null;
        }
    }
} 