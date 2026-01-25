using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventManagementMvc.Tests.Infrastructure;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string HeaderUserId = "X-Test-UserId";
    public const string HeaderUserEmail = "X-Test-UserEmail";
    public const string HeaderRole = "X-Test-Role";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        
        if (!Request.Headers.ContainsKey(HeaderUserId))
        {
            var anonymousIdentity = new ClaimsIdentity(SchemeName);
            var anonymousPrincipal = new ClaimsPrincipal(anonymousIdentity);
            var anonymousTicket = new AuthenticationTicket(anonymousPrincipal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(anonymousTicket));
        }

        var userId = Request.Headers[HeaderUserId].ToString();
        var email = Request.Headers.ContainsKey(HeaderUserEmail)
            ? Request.Headers[HeaderUserEmail].ToString()
            : "test@example.com";

        var role = Request.Headers.ContainsKey(HeaderRole)
            ? Request.Headers[HeaderRole].ToString()
            : "User";

        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, userId),
        new Claim(ClaimTypes.Name, email),
        new Claim(ClaimTypes.Email, email),
    };

        if (!string.IsNullOrWhiteSpace(role))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

}
