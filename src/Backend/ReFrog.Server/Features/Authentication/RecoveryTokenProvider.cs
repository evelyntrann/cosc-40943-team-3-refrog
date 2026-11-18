using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ReFrog.Server.Features.Authentication.Persistence;

namespace ReFrog.Server.Features.Authentication;

public sealed class RecoveryTokenProvider(
    IDataProtectionProvider protection,
    IOptions<DataProtectionTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<AuthAccount>> logger,
    TimeProvider clock) : DataProtectorTokenProvider<AuthAccount>(protection, options, logger)
{
    public const string ProviderName = "Recovery";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private static readonly Encoding TokenEncoding = new UTF8Encoding(false, true);

    // ASP.NET Core Identity's DataProtectorTokenProvider payload, with injected time
    // and the FR-AUTH-password-recovery expiry boundary.
    public override async Task<string> GenerateAsync(string purpose, UserManager<AuthAccount> manager, AuthAccount user)
    {
        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, TokenEncoding, leaveOpen: true))
        {
            writer.Write(clock.GetUtcNow().UtcTicks);
            writer.Write(await manager.GetUserIdAsync(user));
            writer.Write(purpose);
            writer.Write(await manager.GetSecurityStampAsync(user));
        }

        return Convert.ToBase64String(Protector.Protect(payload.ToArray()));
    }

    public override async Task<bool> ValidateAsync(
        string purpose, string token, UserManager<AuthAccount> manager, AuthAccount user)
    {
        try
        {
            using var payload = new MemoryStream(Protector.Unprotect(Convert.FromBase64String(token)));
            using var reader = new BinaryReader(payload, TokenEncoding);
            var issuedAt = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
            var accountId = reader.ReadString();
            var tokenPurpose = reader.ReadString();
            var stamp = reader.ReadString();
            var now = clock.GetUtcNow();

            return issuedAt <= now && now < issuedAt.Add(Lifetime) &&
                accountId == await manager.GetUserIdAsync(user) && tokenPurpose == purpose &&
                stamp == await manager.GetSecurityStampAsync(user) && payload.Position == payload.Length;
        }
        catch (Exception failure) when (failure is FormatException or CryptographicException or IOException or ArgumentException)
        {
            return false;
        }
    }
}
