using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ReFrog.Server.Features.Authentication;

/// <summary>
/// Six-digit code generation and versioned HMAC checks for every verification purpose
/// (AUTH.md "Shared verification challenges use purpose and a constrained owner").
/// The HMAC input is <c>purpose:ownerId:challengeId:code</c>, so a code cannot cross purposes or owners.
/// </summary>
public sealed class VerificationCode(IReadOnlyDictionary<int, byte[]> keys, int keyVersion)
{
    public int KeyVersion { get; } = keyVersion;

    public bool HasKey(int version) => keys.ContainsKey(version);

    public VerificationCode(byte[] key, int keyVersion) : this(
        new Dictionary<int, byte[]> { [keyVersion] = key }, keyVersion)
    {
    }

    public string Generate() => RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    public byte[] Hash(ChallengeOwner owner, Guid challengeId, string code) => Hash(KeyVersion, owner, challengeId, code);

    public bool Matches(int keyVersion, ChallengeOwner owner, Guid challengeId, string code, byte[] expected) =>
        keys.ContainsKey(keyVersion) &&
        CryptographicOperations.FixedTimeEquals(Hash(keyVersion, owner, challengeId, code), expected);

    public byte[] Hash(int keyVersion, ChallengeOwner owner, Guid challengeId, string code) =>
        HMACSHA256.HashData(keys[keyVersion],
            Encoding.UTF8.GetBytes($"{owner.Purpose}:{owner.Id:D}:{challengeId:D}:{code}"));
}
