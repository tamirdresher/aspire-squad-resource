namespace SquadInABox.RealSquad;

/// <summary>
/// Deterministic policy decision for one agent/tool request.
/// </summary>
public sealed record SquadToolDecision(
    string AgentId,
    int AgentTier,
    SquadYoloMode YoloMode,
    string Tool,
    string? TargetPath,
    bool AllowedByPolicy,
    bool Denied,
    bool RequiresApproval,
    bool PromptMayBeSkipped,
    string Reason,
    IReadOnlyDictionary<string, string> AuditParameters)
{
    public string Verdict => Denied
        ? "denied"
        : PromptMayBeSkipped
            ? "allowed:promptless"
            : RequiresApproval
                ? "allowed:approval-required"
                : "allowed";
}
