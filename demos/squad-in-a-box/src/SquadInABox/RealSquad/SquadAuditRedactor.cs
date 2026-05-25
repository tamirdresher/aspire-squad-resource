using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SquadInABox.RealSquad;

/// <summary>
/// Redacts secret-looking audit parameters while preserving deterministic evidence.
/// </summary>
public static partial class SquadAuditRedactor
{
    public static IReadOnlyDictionary<string, string> Redact(IReadOnlyDictionary<string, string> parameters)
    {
        var redacted = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in parameters)
        {
            redacted[key] = LooksSecret(key, value)
                ? $"<redacted:sha256:{Hash(value)}>"
                : value;
        }

        return redacted;
    }

    private static bool LooksSecret(string key, string value) =>
        SecretKeyRegex().IsMatch(key)
        || SecretValueRegex().IsMatch(value);

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..12];
    }

    [GeneratedRegex("token|secret|password|credential|api[-_]?key|pat", RegexOptions.IgnoreCase)]
    private static partial Regex SecretKeyRegex();

    [GeneratedRegex("(github_pat_|ghp_|gho_|ghu_|ghs_|ghr_)[A-Za-z0-9_]+|[A-Za-z0-9+/=]{32,}", RegexOptions.IgnoreCase)]
    private static partial Regex SecretValueRegex();
}
