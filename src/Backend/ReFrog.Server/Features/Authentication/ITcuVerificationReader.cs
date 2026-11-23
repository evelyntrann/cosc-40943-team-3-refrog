namespace ReFrog.Server.Features.Authentication;

/// <summary>
/// An account's persisted TCU email verification and the expiry of its live <c>TCU_AFFILIATION</c> code,
/// if any. It never carries the verified email address.
/// </summary>
public sealed record TcuVerificationState(bool Verified, DateTimeOffset? VerifiedAt, DateTimeOffset? PendingCodeExpiresAt);

/// <summary>
/// The only way another area reads persisted TCU email verification (AUTH.md <c>ITcuVerificationReader</c>).
/// A failed read throws <see cref="AccessStateUnavailableException"/>; it never answers "not verified".
/// </summary>
public interface ITcuVerificationReader
{
    Task<TcuVerificationState> ForAccountAsync(Guid accountId, CancellationToken cancellationToken);
}

/// <summary>
/// Current access or verification state could not be read inside an area (AUTH.md "Unavailable by stage").
/// The shared handler answers <c>503 ACCESS_UNAVAILABLE</c>, never the generic 500 (architecture §8.2.1).
/// </summary>
public sealed class AccessStateUnavailableException(Exception? inner = null)
    : Exception("Current access state could not be established.", inner);
