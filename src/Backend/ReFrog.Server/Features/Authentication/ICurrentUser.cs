using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReFrog.Server.Features.Authentication.Persistence;

namespace ReFrog.Server.Features.Authentication;

/// <summary>Current volunteer access (AUTH.md <c>IVolunteerAccessStateProvider</c>; <c>BR-volunteer-role-lifecycle</c>).</summary>
public enum VolunteerAccessState
{
    None,
    RoleActive,
    Restricted,
}

/// <summary>
/// Reads an account's current volunteer access from the database on every call, never from cookie claims
/// (<c>FR-AUTH-shopping-eligibility</c>). A failed read throws <see cref="AccessStateUnavailableException"/>,
/// never <see cref="VolunteerAccessState.None"/>.
/// </summary>
public interface IVolunteerAccessStateProvider
{
    Task<VolunteerAccessState> CurrentStateAsync(Guid accountId, CancellationToken cancellationToken);
}

/// <summary>
/// The signed-in account, taken only from the validated server principal, and its current trusted access
/// (AUTH.md <c>ICurrentUser</c>). No caller accepts an account id or role from the browser.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The account id, or null when the request is not signed in.</summary>
    Guid? AccountId { get; }

    Task<VolunteerAccessState> CurrentVolunteerAccessAsync(CancellationToken cancellationToken);
}

public sealed class CurrentUser(IHttpContextAccessor http, IVolunteerAccessStateProvider access) : ICurrentUser
{
    public Guid? AccountId
    {
        get
        {
            // The cookie handler has already validated the security stamp for this request (AuthCookieValidation).
            var principal = http.HttpContext?.User;
            return principal?.Identity?.IsAuthenticated == true &&
                Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id
                : null;
        }
    }

    public Task<VolunteerAccessState> CurrentVolunteerAccessAsync(CancellationToken cancellationToken) =>
        AccountId is { } id
            ? access.CurrentStateAsync(id, cancellationToken)
            : throw new InvalidOperationException("No signed-in account.");
}

/// <summary>
/// Maps the Identity roles VOL already enforces: <c>VolunteerRestricted</c> gives <c>RESTRICTED</c> even alongside
/// <c>Volunteer</c>; otherwise <c>Volunteer</c> gives <c>ROLE_ACTIVE</c>; otherwise <c>NONE</c>. The role names stay
/// defined in <see cref="Volunteer.VolunteerAuthorization"/> (developer decision, issue #103).
/// </summary>
public sealed class EfVolunteerAccessStateProvider(AuthDbContext db, ILookupNormalizer normalizer)
    : IVolunteerAccessStateProvider
{
    public async Task<VolunteerAccessState> CurrentStateAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var restricted = normalizer.NormalizeName(Volunteer.VolunteerAuthorization.VolunteerRestrictedRole);
        var volunteer = normalizer.NormalizeName(Volunteer.VolunteerAuthorization.VolunteerRole);
        List<string?> roles;
        try
        {
            roles = await db.UserRoles.AsNoTracking()
                .Where(link => link.UserId == accountId)
                .Join(db.Roles, link => link.RoleId, role => role.Id, (_, role) => role.NormalizedName)
                .ToListAsync(cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            throw new AccessStateUnavailableException(failure);
        }

        return roles.Contains(restricted) ? VolunteerAccessState.Restricted
            : roles.Contains(volunteer) ? VolunteerAccessState.RoleActive
            : VolunteerAccessState.None;
    }
}
