using System.Text.Json;
using System.Text.Json.Serialization;

namespace SquadInABox.RealSquad;

/// <summary>
/// Mirrors .squad/policies/tool-allowlists.json for deterministic policy evaluation.
/// </summary>
public sealed record SquadToolPolicy(
    int Version,
    string Description,
    IReadOnlyDictionary<string, SquadAgentToolPolicy> Agents,
    SquadAgentToolPolicy Defaults)
{
    public static SquadToolPolicy Load(string policyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyPath);

        if (!File.Exists(policyPath))
        {
            throw new FileNotFoundException("Squad tool policy file was not found.", policyPath);
        }

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());

        var policy = JsonSerializer.Deserialize<SquadToolPolicy>(File.ReadAllText(policyPath), options);
        if (policy is null)
        {
            throw new InvalidOperationException($"Could not parse Squad tool policy: {policyPath}");
        }

        return policy with
        {
            Agents = new Dictionary<string, SquadAgentToolPolicy>(policy.Agents, StringComparer.OrdinalIgnoreCase)
        };
    }

    public SquadAgentToolPolicy ForAgent(string agentId) =>
        Agents.TryGetValue(agentId, out var policy) ? policy : Defaults;
}

public sealed record SquadAgentToolPolicy(
    int Tier,
    [property: JsonPropertyName("allowed_tools")] IReadOnlyList<string> AllowedTools,
    [property: JsonPropertyName("blocked_tools")] IReadOnlyList<string> BlockedTools,
    string Rationale);
