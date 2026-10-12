using System.Security.Claims;

namespace Tern.Api.Security;

public static class UserExtensions
{
    // The id is stored in the "sub" claim of the token.
    public static string? UserId(this ClaimsPrincipal user) => user.FindFirstValue("sub");
}
