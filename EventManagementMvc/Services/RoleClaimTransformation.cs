using EventManagementMvc.Areas.Identity.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace EventManagementMvc.Services
{
    public class RoleClaimTransformation : IClaimsTransformation
    {
        private readonly UserManager<EventManagementMvcUser> _userManager;

        public RoleClaimTransformation(UserManager<EventManagementMvcUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
           
            if (principal.Identity?.IsAuthenticated != true)
                return principal;

           
            if (principal.Claims.Any(c => c.Type == ClaimTypes.Role))
                return principal;

            var user = await _userManager.GetUserAsync(principal);
            if (user == null)
                return principal;

            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Count > 0 && principal.Identity is ClaimsIdentity identity)
            {
                foreach (var role in roles)
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }

            return principal;
        }
    }
}
