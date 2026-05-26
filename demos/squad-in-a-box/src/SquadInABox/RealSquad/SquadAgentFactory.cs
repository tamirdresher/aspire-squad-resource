using GitHub.Copilot.SDK;
using Microsoft.Agents.AI.GitHub.Copilot;

namespace SquadInABox.RealSquad;

/// <summary>
/// Adapter boundary between repo-native Squad charters and Copilot-backed Microsoft Agent Framework agents.
/// </summary>
public sealed class SquadAgentFactory
{
    public SquadAgentRegistration Create(
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

        return new SquadAgentRegistration(
            Definition: definition,
            Model: options.Model,
            CopilotSdkPackage: "GitHub.Copilot.SDK/1.0.0-beta.3",
            MafPackage: "Microsoft.Agents.AI.GitHub.Copilot/1.5.0-preview.260507.1",
            PermissionPolicy: options.PermissionPolicy,
            NativeAgent: nativeAgent);
    }

    private static SquadAgent CreateNativeAgent(
        SquadAgentDefinition definition,
        SquadRuntimeOptions options,
        IReadOnlyCollection<SquadAgentDefinition> customAgentDefinitions)
    {
        return new SquadAgent(definition, options, customAgentDefinitions);
    }

    public static CopilotClientOptions CreateClientOptions(SquadRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var clientOptions = new CopilotClientOptions
        {
            AutoStart = false,
            Cwd = options.TeamRoot,
            GitHubToken = string.IsNullOrWhiteSpace(options.GitHubToken) ? null : options.GitHubToken,
            UseLoggedInUser = string.IsNullOrWhiteSpace(options.GitHubToken)
        };

        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            clientOptions.Telemetry = new TelemetryConfig { OtlpEndpoint = otlpEndpoint };
        }

        return clientOptions;
    }

    public static SessionConfig CreateSessionConfig(
        SquadAgentDefinition definition,
        SquadRuntimeOptions options,
        IReadOnlyCollection<SquadAgentDefinition> customAgentDefinitions,
        SessionEventHandler? onEvent)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(customAgentDefinitions);

        return new SessionConfig
        {
            ClientName = $"SquadInABox.RealSquad.{definition.Id}",
            ConfigDir = options.SquadRoot,
            EnableConfigDiscovery = true,
            GitHubToken = string.IsNullOrWhiteSpace(options.GitHubToken) ? null : options.GitHubToken,
            Model = options.Model,
            OnPermissionRequest = PermissionHandler.ApproveAll,
            Streaming = true,
            IncludeSubAgentStreamingEvents = true,
            Agent = definition.Id,
            CustomAgents = customAgentDefinitions
                .Select(agent => new CustomAgentConfig
                {
                    Name = agent.Id,
                    DisplayName = agent.Name,
                    Description = agent.Description,
                    Prompt = agent.SystemPrompt,
                    Infer = true
                })
                .ToArray(),
            SystemMessage = new SystemMessageConfig
            {
                Content = definition.SystemPrompt
            },
            WorkingDirectory = options.TeamRoot,
            OnEvent = onEvent
        };
    }
}
