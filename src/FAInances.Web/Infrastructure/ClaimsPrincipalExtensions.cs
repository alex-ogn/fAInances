using System.Security.Claims;

namespace FAInances.Web.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id of the signed-in user. Throws if the principal is not authenticated.</summary>
    public static string GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("The current user is not signed in.");
}
