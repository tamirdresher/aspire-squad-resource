using System.Text.Json;
using System.Text.RegularExpressions;

namespace SquadInABox.RealSquad;

/// <summary>
/// Loads the real repository .squad folder without relying on the legacy .mjs bridge.
/// </summary>
public sealed partial class SquadContextLoader
{
    public string FindTeamRoot(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".squad")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException($"Could not find a .squad folder above '{startDirectory}'. Pass --team-root <path>.");
    }

    public string LoadDefaultModel(string teamRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamRoot);

        var configPath = Path.Combine(teamRoot, ".squad", "config.json");
        if (!File.Exists(configPath))
        {
            return "gpt-5";
        }

        using var config = JsonDocument.Parse(File.ReadAllText(configPath));
        return config.RootElement.TryGetProperty("defaultModel", out var defaultModel)
            && defaultModel.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(defaultModel.GetString())
                ? defaultModel.GetString()!
                : "gpt-5";
    }

    public IReadOnlyList<SquadAgentDefinition> LoadAgentDefinitions(string teamRoot, IReadOnlySet<string>? requestedAgentIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamRoot);

        var squadRoot = Path.Combine(teamRoot, ".squad");
        if (!Directory.Exists(squadRoot))
        {
            throw new DirectoryNotFoundException($"Squad root does not exist: {squadRoot}");
        }

        var agentsRoot = Path.Combine(squadRoot, "agents");
        if (!Directory.Exists(agentsRoot))
        {
            throw new DirectoryNotFoundException($"Squad agents directory does not exist: {agentsRoot}");
        }

        var definitions = new List<SquadAgentDefinition>();
        foreach (var agentDirectory in Directory.EnumerateDirectories(agentsRoot).Order(StringComparer.OrdinalIgnoreCase))
        {
            var id = Path.GetFileName(agentDirectory);
            if (id.StartsWith("_", StringComparison.Ordinal))
            {
                continue;
            }

            if (requestedAgentIds is not null && !requestedAgentIds.Contains(id))
            {
                continue;
            }

            var charterPath = Path.Combine(agentDirectory, "charter.md");
            if (!File.Exists(charterPath))
            {
                continue;
            }

            var charter = File.ReadAllText(charterPath);
            var historyPath = Path.Combine(agentDirectory, "history.md");
            definitions.Add(new SquadAgentDefinition(
                Id: id,
                Name: ExtractName(charter, id),
                Role: ExtractRole(charter),
                Title: ExtractTitle(charter),
                CharterPath: charterPath,
                HistoryPath: File.Exists(historyPath) ? historyPath : null,
                SystemPrompt: charter.Trim(),
                Capabilities: ExtractCapabilities(charter)));
        }

        if (requestedAgentIds is not null)
        {
            var loadedIds = definitions.Select(definition => definition.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingIds = requestedAgentIds.Where(id => !loadedIds.Contains(id)).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            if (missingIds.Length > 0)
            {
                throw new InvalidOperationException($"Requested agent charter(s) not found: {string.Join(", ", missingIds)}");
            }
        }

        if (definitions.Count == 0)
        {
            throw new InvalidOperationException($"No active Squad agent charters were found under {agentsRoot}.");
        }

        return definitions;
    }

    private static string ExtractName(string charter, string fallbackId)
    {
        var heading = HeadingRegex().Match(charter);
        if (heading.Success)
        {
            var value = heading.Groups["text"].Value.Trim('\uFEFF', ' ', '\t');
            var split = value.Split('—', 2, StringSplitOptions.TrimEntries);
            return string.IsNullOrWhiteSpace(split[0]) ? fallbackId : split[0];
        }

        return fallbackId;
    }

    private static string ExtractTitle(string charter)
    {
        var heading = HeadingRegex().Match(charter);
        if (!heading.Success)
        {
            return string.Empty;
        }

        var split = heading.Groups["text"].Value.Split('—', 2, StringSplitOptions.TrimEntries);
        return split.Length == 2 ? split[1] : string.Empty;
    }

    private static string ExtractRole(string charter)
    {
        var role = RoleRegex().Match(charter);
        return role.Success ? role.Groups["role"].Value.Trim() : "Squad agent";
    }

    private static IReadOnlyList<string> ExtractCapabilities(string charter)
    {
        var capabilities = CapabilityRegex().Match(charter);
        if (!capabilities.Success)
        {
            return Array.Empty<string>();
        }

        return capabilities.Groups["capabilities"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(capability => !string.IsNullOrWhiteSpace(capability))
            .ToArray();
    }

    [GeneratedRegex(@"^\s*#\s+(?<text>.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"\*\*Role:\*\*\s*(?<role>.+?)(?:\||\r?\n)|^\s*Role:\s*(?<role>.+?)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex RoleRegex();

    [GeneratedRegex(@"^\s*-\s*\*\*capabilities:\*\*\s*(?<capabilities>.+?)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex CapabilityRegex();
}
