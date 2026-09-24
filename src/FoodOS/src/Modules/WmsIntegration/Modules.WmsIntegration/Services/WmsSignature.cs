using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FSH.Modules.WmsIntegration.Services;

public static class WmsSignature
{
    public static string Sign(string timestamp, ReadOnlySpan<byte> body, string secret)
    {
        byte[] prefix = Encoding.UTF8.GetBytes(timestamp + ".");
        byte[] signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0);
        body.CopyTo(signed.AsSpan(prefix.Length));
        byte[] hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed);
        return "sha256=" + Convert.ToHexStringLower(hash);
    }

    public static bool Verify(string timestamp, ReadOnlySpan<byte> body, string secret, string candidate)
    {
        string expected = Sign(timestamp, body, secret);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(candidate));
    }

    public static bool IsWithinReplayWindow(string timestamp, TimeProvider clock, int replayWindowSeconds)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out long unixSeconds)) return false;
        var sentAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        return (clock.GetUtcNow() - sentAt).Duration() <= TimeSpan.FromSeconds(replayWindowSeconds);
    }
}
