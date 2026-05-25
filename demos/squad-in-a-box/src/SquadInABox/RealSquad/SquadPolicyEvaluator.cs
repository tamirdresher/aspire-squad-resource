namespace SquadInABox.RealSquad;

/// <summary>
/// Small deterministic evaluator for this proof slice. Blocklist wins; unknown agents use defaults.
/// </summary>
public sealed class SquadPolicyEvaluator
{
    private static readonly string[] LowRiskPromptlessTools =
    [
        "view",
        "grep",
        "glob",
        "web_fetch",
        "session_store_sql",
        "sql"
    ];

    private static readonly string[] FilesystemScopedTools =
    [
        "view",
        "edit",
        "create",
        "grep",
        "glob"
    ];

    private static readonly string[] ProtectedGovernancePathPrefixes =
    [
        ".squad/policies",
        ".squad/decisions",
        ".squad/decisions.md",
        ".squad/agents",
        ".squad/identity",
        ".squad/approval-gate",
        ".squad/audit-logs",
        ".squad/config.json",
        ".squad/routing.md"
    ];

    public SquadToolDecision Evaluate(
        SquadToolPolicy policy,
        string agentId,
        SquadYoloMode yoloMode,
        string tool,
        string? targetPath,
        string teamRoot,
        IReadOnlyDictionary<string, string>? auditParameters = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        ArgumentException.ThrowIfNullOrWhiteSpace(teamRoot);

        var agentPolicy = policy.ForAgent(agentId);
        var blocked = MatchesAny(agentPolicy.BlockedTools, tool);
        var allowed = !blocked && MatchesAny(agentPolicy.AllowedTools, tool);
        var pathScope = ResolvePathScope(teamRoot, tool, targetPath);

        if (blocked)
        {
            return Decision(false, true, false, false, "Explicit blocklist wins over allowlist.");
        }

        if (!allowed)
        {
            return Decision(false, true, false, false, "Tool is not allowed for this agent tier.");
        }

        if (pathScope is { IsInsideTeamRoot: false })
        {
            return Decision(true, true, false, false, "Filesystem target resolves outside team root and is denied in this deterministic proof.");
        }

        if (yoloMode == SquadYoloMode.PromptlessWithinPolicy && pathScope is { IsProtectedGovernancePath: true })
        {
            return Decision(true, true, false, false, "Protected governance paths are denied in YOLO mode.");
        }

        var lowRisk = MatchesAny(LowRiskPromptlessTools, tool);
        var promptMayBeSkipped = yoloMode == SquadYoloMode.PromptlessWithinPolicy && lowRisk;
        var requiresApproval = !promptMayBeSkipped;
        var reason = promptMayBeSkipped
            ? "Low-risk tool is allowed by policy; prompt may be skipped."
            : "Allowed by policy, but approval is required; destructive/high-impact operations abort on approval timeout.";

        return Decision(true, false, requiresApproval, promptMayBeSkipped, reason);

        SquadToolDecision Decision(bool allowedByPolicy, bool denied, bool requiresApproval, bool promptMayBeSkipped, string reason) =>
            new(
                AgentId: agentId,
                AgentTier: agentPolicy.Tier,
                YoloMode: yoloMode,
                Tool: tool,
                TargetPath: pathScope?.CanonicalTargetPath ?? targetPath,
                AllowedByPolicy: allowedByPolicy,
                Denied: denied,
                RequiresApproval: requiresApproval,
                PromptMayBeSkipped: promptMayBeSkipped,
                Reason: reason,
                AuditParameters: SquadAuditRedactor.Redact(auditParameters ?? new Dictionary<string, string>()));
    }

    private static bool MatchesAny(IEnumerable<string> patterns, string tool) =>
        patterns.Any(pattern => Matches(pattern, tool));

    private static bool Matches(string pattern, string tool)
    {
        if (string.Equals(pattern, tool, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (pattern.EndsWith('*'))
        {
            return tool.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static PathScope? ResolvePathScope(string teamRoot, string tool, string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || !MatchesAny(FilesystemScopedTools, tool))
        {
            return null;
        }

        var canonicalTeamRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(teamRoot));
        var canonicalTargetPath = Path.GetFullPath(Path.IsPathRooted(targetPath)
            ? targetPath
            : Path.Combine(canonicalTeamRoot, targetPath));
        var relative = Path.GetRelativePath(canonicalTeamRoot, canonicalTargetPath).Replace('\\', '/');
        var isInsideTeamRoot = IsInsideRoot(relative);
        var protectedGovernancePath = isInsideTeamRoot && ProtectedGovernancePathPrefixes.Any(prefix =>
            relative.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || relative.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));

        return new PathScope(canonicalTargetPath, isInsideTeamRoot, protectedGovernancePath);
    }

    private static bool IsInsideRoot(string relativePath) =>
        relativePath == "."
        || (!Path.IsPathRooted(relativePath)
            && !relativePath.Equals("..", StringComparison.Ordinal)
            && !relativePath.StartsWith("../", StringComparison.Ordinal));

    private sealed record PathScope(
        string CanonicalTargetPath,
        bool IsInsideTeamRoot,
        bool IsProtectedGovernancePath);
}
