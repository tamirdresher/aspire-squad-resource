using GitHub.Copilot.SDK;
using Microsoft.Agents.AI.GitHub.Copilot;

namespace SquadInABox.RealSquad;

/// <summary>
/// Adapter boundary between repo-native Squad charters and Copilot-backed Microsoft Agent Framework agents.
/// </summary>
public sealed class SquadAgentFactory
{
    public CopilotBackedMafAgent Create(SquadAgentDefinition definition, SquadRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);

        object? nativeAgent = options.ConstructNativeAgents
            ? CreateNativeAgent(definition, options)
            : null;

        return new CopilotBackedMafAgent(
            Definition: definition,
            Model: options.Model,
            CopilotSdkPackage: "GitHub.Copilot.SDK/1.0.0-beta.3",
            MafPackage: "Microsoft.Agents.AI.GitHub.Copilot/1.5.0-preview.260507.1",
            PermissionPolicy: options.PermissionPolicy,
            NativeAgent: nativeAgent);
    }

    private static GitHubCopilotAgent CreateNativeAgent(SquadAgentDefinition definition, SquadRuntimeOptions options)
    {
        var clientOptions = new CopilotClientOptions
        {
            AutoStart = false,
            Cwd = options.TeamRoot,
            GitHubToken = string.IsNullOrWhiteSpace(options.GitHubToken) ? null : options.GitHubToken,
            UseLoggedInUser = string.IsNullOrWhiteSpace(options.GitHubToken)
        };

        var sessionConfig = new SessionConfig
        {
            ClientName = $"SquadInABox.RealSquad.{definition.Id}",
            ConfigDir = options.SquadRoot,
            EnableConfigDiscovery = true,
            GitHubToken = string.IsNullOrWhiteSpace(options.GitHubToken) ? null : options.GitHubToken,
            Model = options.Model,
            Streaming = true,
            SystemMessage = new SystemMessageConfig
            {
                Content = definition.SystemPrompt
            },
            WorkingDirectory = options.TeamRoot
        };

        var client = new CopilotClient(clientOptions);
        return new GitHubCopilotAgent(
            client,
            sessionConfig,
            true,
            definition.Id,
            definition.Name,
            definition.Description);
    }
}
