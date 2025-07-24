using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace VurduGololdu.API.Extensions
{
    /// <summary>
    /// Claims transformation that adds lower-level role claims for users with higher-level roles (e.g., SuperAdmin ➜ Admin).
    /// Böylece SuperAdmin, Admin yetkisi gerektiren tüm endpoint'lere erişebilir.
    /// </summary>
    public class RoleHierarchyClaimsTransformation : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity is not ClaimsIdentity identity)
                return Task.FromResult(principal);

            // Eğer kullanıcı SuperAdmin ise ve Admin rolü claim'i yoksa ekle
            bool isSuperAdmin = identity.HasClaim(ClaimTypes.Role, "SuperAdmin");
            bool hasAdmin = identity.HasClaim(ClaimTypes.Role, "Admin");

            if (isSuperAdmin && !hasAdmin)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
            }

            return Task.FromResult(principal);
        }
    }
}