namespace SquadInABox.RealSquad;

/// <summary>
/// A deterministic proof artifact: real .squad charters mapped to Copilot-backed MAF adapter records.
/// </summary>
public sealed record SquadRuntimeDescription(
    string TeamRoot,
    string SquadRoot,
    string ToolPolicyPath,
    SquadYoloMode YoloMode,
    IReadOnlyList<CopilotBackedMafAgent> Agents,
    IReadOnlyList<SquadToolDecision> ToolDecisions,
    IReadOnlyList<string> RuntimePrerequisites)
{
    public int ConstructedNativeAgentCount => Agents.Count(agent => agent.NativeAgent is not null);
}
