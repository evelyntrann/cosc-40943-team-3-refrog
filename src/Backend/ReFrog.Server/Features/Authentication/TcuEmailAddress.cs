namespace ReFrog.Server.Features.Authentication;

/// <summary>
/// The one AUTH-owned TCU email check (AUTH.md "One email-domain check"; <c>BR-tcu-email-domain</c>).
/// Registration uses <see cref="HasExactTcuDomain"/> to decide whether completion writes TCU verification;
/// the affiliation request also applies the lenient shape check from IDV.md Key decisions.
/// </summary>
public static class TcuEmailAddress
{
    public const string Domain = "tcu.edu";
    public const int MaxLength = 254;

    /// <summary>Exactly one <c>@</c>, a non-empty local part, and a domain equal to <c>tcu.edu</c>, culture-independent.</summary>
    public static bool HasExactTcuDomain(string address)
    {
        var at = address.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && at == address.LastIndexOf('@') &&
            string.Equals(address[(at + 1)..], Domain, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The address as an affiliation request accepts it: trimmed, at most 254 characters, a local part with no
    /// whitespace or control characters, and the exact domain. Other local-part characters are not restricted.
    /// </summary>
    public static bool IsAcceptable(string trimmed) =>
        trimmed.Length <= MaxLength &&
        HasExactTcuDomain(trimmed) &&
        !trimmed[..trimmed.IndexOf('@', StringComparison.Ordinal)].Any(ch => char.IsWhiteSpace(ch) || char.IsControl(ch));
}
