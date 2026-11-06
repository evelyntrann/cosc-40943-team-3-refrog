using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ReFrog.Server.Features.Authentication;

public sealed class RegistrationCode(IReadOnlyDictionary<int, byte[]> keys, int keyVersion)
{
    public int KeyVersion { get; } = keyVersion;

    public bool HasKey(int version) => keys.ContainsKey(version);

    public RegistrationCode(byte[] key, int keyVersion) : this(
        new Dictionary<int, byte[]> { [keyVersion] = key }, keyVersion)
    {
    }

    public string Generate() => RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    public byte[] Hash(Guid subjectId, Guid challengeId, string code) => Hash(KeyVersion, subjectId, challengeId, code);

    public bool Matches(int keyVersion, Guid subjectId, Guid challengeId, string code, byte[] expected) =>
        keys.ContainsKey(keyVersion) &&
        CryptographicOperations.FixedTimeEquals(Hash(keyVersion, subjectId, challengeId, code), expected);

    public byte[] Hash(int keyVersion, Guid subjectId, Guid challengeId, string code) =>
        HMACSHA256.HashData(keys[keyVersion],
            Encoding.UTF8.GetBytes($"ACCOUNT_REGISTRATION:{subjectId:D}:{challengeId:D}:{code}"));
}
