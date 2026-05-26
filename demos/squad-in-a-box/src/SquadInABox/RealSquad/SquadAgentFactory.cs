using GitHub.Copilot.SDK;
using Microsoft.Agents.AI.GitHub.Copilot;

namespace SquadInABox.RealSquad;

/// <summary>
/// Adapter boundary between repo-native Squad charters and Copilot-backed Microsoft Agent Framework agents.
/// </summary>
public sealed class SquadAgentFactory
{
    public CopilotBackedMafAgent Create(
        SquadAgentDefinition definition,
        SquadRuntimeOptions options,
        IReadOnlyCollection<SquadAgentDefinition> customAgentDefinitions)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(customAgentDefinitions);

        object? nativeAgent = options.ConstructNativeAgents
            ? CreateNativeAgent(definition, options, customAgentDefinitions)
            : null;

        return new CopilotBackedMafAgent(
            Definition: definition,
            Model: options.Model,
            CopilotSdkPackage: "GitHub.Copilot.SDK/1.0.0-beta.3",
            MafPackage: "Microsoft.Agents.AI.GitHub.Copilot/1.5.0-preview.260507.1",
            PermissionPolicy: options.PermissionPolicy,
            NativeAgent: nativeAgent);
    }

    private static GitHubCopilotAgent CreateNativeAgent(
        SquadAgentDefinition definition,
        SquadRuntimeOptions options,
        IReadOnlyCollection<SquadAgentDefinition> customAgentDefinitions)
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
            IncludeSubAgentStreamingEvents = true,
            Agent = definition.Id,
            CustomAgents = customAgentDefinitions
                .Select(agent => new CustomAgentConfig
                {
                    Name = agent.Id,
                    DisplayName = agent.Name,
                    Description = agent.Description,
                    Prompt = agent.SystemPrompt
                })
                .ToArray(),
            SystemMessage = new SystemMessageConfig
            {
                Content = definition.SystemPrompt
            },
            WorkingDirectory = options.TeamRoot,
            OnEvent = sessionEvent => options.OnCopilotSessionEvent?.Invoke(
                CopilotSessionTraceMapper.FromSessionEvent(definition.Id, sessionEvent))
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
