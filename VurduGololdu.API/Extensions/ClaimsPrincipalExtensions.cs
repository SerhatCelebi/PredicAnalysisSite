using System.Security.Claims;

namespace VurduGololdu.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal user)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdStr, out var id) ? id : (int?)null;
    }

    public static string? GetUserEmail(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Email);

    public static int? GetCurrentUserId(this ClaimsPrincipal user) => user.GetUserId();
}