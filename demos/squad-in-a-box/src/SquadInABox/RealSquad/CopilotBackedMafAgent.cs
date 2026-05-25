namespace SquadInABox.RealSquad;

/// <summary>
/// Describes the C# adapter result for one real Squad charter.
/// </summary>
public sealed record CopilotBackedMafAgent(
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
