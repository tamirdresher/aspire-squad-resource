namespace SquadInABox.RealSquad;

/// <summary>
/// Explicit prompt behavior for tool approvals. There is no unrestricted mode.
/// </summary>
public enum SquadYoloMode
{
    Disabled = 0,
    PromptlessWithinPolicy = 1
}
