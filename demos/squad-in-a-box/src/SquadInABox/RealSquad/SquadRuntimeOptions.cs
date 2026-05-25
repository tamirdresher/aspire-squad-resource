namespace SquadInABox.RealSquad;

/// <summary>
/// Options for loading a repo-native Squad and adapting it to Copilot-backed MAF agents.
/// </summary>
public sealed record SquadRuntimeOptions(
    string TeamRoot,
    string Model,
    string? GitHubToken,
    bool ConstructNativeAgents,
    SquadYoloMode YoloMode,
    string ToolPolicyPath,
    string? ToolName,
    string? TargetPath,
    IReadOnlyDictionary<string, string> AuditParameters)
{
    public string SquadRoot => Path.Combine(TeamRoot, ".squad");

    public string PermissionPolicy =>
        YoloMode == SquadYoloMode.PromptlessWithinPolicy
            ? "YOLO is PromptlessWithinPolicy only: low-risk operations may skip prompts only after allowlist/blocklist/path policy permits them."
            : "YOLO disabled: live Copilot sessions must use explicit OnPermissionRequest approval; approval timeouts abort.";
}
