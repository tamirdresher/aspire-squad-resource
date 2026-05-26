namespace SquadInABox.RealSquad;

/// <summary>
/// Registration record for one loaded .squad charter.
///
/// This is NOT an agent. The actual Microsoft.Agents.AI.AIAgent wrapper around
/// the GitHub Copilot SDK lives in <see cref="SquadAgent"/>. This record holds
/// the metadata Aspire and the dashboard surface to the user (model, package
/// versions, permission policy) plus an optional reference to the constructed
/// native MAF agent, so the runtime can describe what was loaded without
/// instantiating Copilot sessions when only a dry-run description is needed.
/// </summary>
public sealed record SquadAgentRegistration(
    SquadAgentDefinition Definition,
    string Model,
    string CopilotSdkPackage,
    string MafPackage,
    string PermissionPolicy,
    object? NativeAgent) : IAsyncDisposable
{
    public string NativeStatus => NativeAgent is null
        ? "described"
        : $"constructed:{NativeAgent.GetType().FullName}";

    public async ValueTask DisposeAsync()
    {
        if (NativeAgent is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
            return;
        }

        if (NativeAgent is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
