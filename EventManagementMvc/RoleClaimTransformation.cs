using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace EventManagementMvc.Services
{
    public class RoleClaimTransformation : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
                return Task.FromResult(principal);

            // Collect existing roles (ClaimTypes.Role)
            var existingRoles = identity.FindAll(ClaimTypes.Role)
                                        .Select(c => c.Value)
                                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Copy "role" and "roles" claim types into ClaimTypes.Role
            foreach (var claimType in new[] { "role", "roles" })
            {
                foreach (var c in identity.FindAll(claimType))
                {
                    if (!string.IsNullOrWhiteSpace(c.Value) && !existingRoles.Contains(c.Value))
                        identity.AddClaim(new Claim(ClaimTypes.Role, c.Value));
                }
            }

            return Task.FromResult(principal);
        }
    }
}
