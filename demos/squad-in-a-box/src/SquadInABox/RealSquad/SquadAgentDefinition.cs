namespace SquadInABox.RealSquad;

/// <summary>
/// Repo-native Squad agent definition loaded from .squad/agents/{id}/charter.md.
/// </summary>
public sealed record SquadAgentDefinition(
    string Id,
    string Name,
    string Role,
    string Title,
    string CharterPath,
    string? HistoryPath,
    string SystemPrompt,
    IReadOnlyList<string> Capabilities)
{
    public string Description =>
        string.IsNullOrWhiteSpace(Title)
            ? $"{Name} — {Role}"
            : $"{Name} — {Title}";
}
