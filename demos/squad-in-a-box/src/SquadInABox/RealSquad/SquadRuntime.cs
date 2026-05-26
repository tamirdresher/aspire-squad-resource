namespace SquadInABox.RealSquad;

/// <summary>
/// Loads a real .squad folder and maps it to Copilot-backed MAF adapter objects.
/// </summary>
public sealed class SquadRuntime
{
    private readonly SquadContextLoader _loader;
    private readonly SquadAgentFactory _factory;
    private readonly SquadPolicyEvaluator _policyEvaluator;

    public SquadRuntime(SquadContextLoader loader, SquadAgentFactory factory, SquadPolicyEvaluator policyEvaluator)
    {
        _loader = loader;
        _factory = factory;
        _policyEvaluator = policyEvaluator;
    }

    public SquadRuntimeDescription Describe(SquadRuntimeOptions options, IReadOnlySet<string>? requestedAgentIds = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var definitions = _loader.LoadAgentDefinitions(options.TeamRoot, requestedAgentIds);
        var agents = definitions
            .Select(definition => _factory.Create(definition, options, definitions))
            .ToArray();
        var toolDecisions = Array.Empty<SquadToolDecision>();
        if (!string.IsNullOrWhiteSpace(options.ToolName))
        {
            var policy = SquadToolPolicy.Load(options.ToolPolicyPath);
            toolDecisions = definitions
                .Select(definition => _policyEvaluator.Evaluate(
                    policy,
                    definition.Id,
                    options.YoloMode,
                    options.ToolName,
                    options.TargetPath,
                    options.TeamRoot,
                    options.AuditParameters))
                .ToArray();
        }

        return new SquadRuntimeDescription(
            TeamRoot: options.TeamRoot,
            SquadRoot: options.SquadRoot,
            ToolPolicyPath: options.ToolPolicyPath,
            YoloMode: options.YoloMode,
            Agents: agents,
            ToolDecisions: toolDecisions,
            RuntimePrerequisites:
            [
                "For live execution, authenticate GitHub Copilot CLI/SDK non-interactively or set GITHUB_TOKEN.",
                "YOLO means PromptlessWithinPolicy only: it never bypasses allowlists, blocklists, protected paths, approval gates, or audit.",
                "Only low-risk operations already allowed by .squad/policies/tool-allowlists.json may skip prompts in YOLO mode.",
                "Destructive/high-impact operations still require explicit approval; approval timeouts abort.",
                "This adapter constructs Microsoft.Agents.AI.GitHub.Copilot.GitHubCopilotAgent objects only when --construct is supplied."
            ]);
    }
}
