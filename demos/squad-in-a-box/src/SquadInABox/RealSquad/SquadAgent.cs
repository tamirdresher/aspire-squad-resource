using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.GitHub.Copilot;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace SquadInABox.RealSquad;

/// <summary>
/// MAF agent wrapper for a repo-native Squad charter.
/// </summary>
public sealed class SquadAgent : AIAgent, IAsyncDisposable
{
    private static readonly ActivitySource s_activitySource = new("SquadInABox.RealSquad");

    private static readonly ChatMessage BoundaryMessage = new(
        ChatRole.System,
        "You are running as a repo-native Squad custom agent. Follow the loaded .squad charter, use configured subagents when delegation is needed, and keep the run safe: do not modify files or external systems unless the caller explicitly asks for that operation.");

    private readonly SquadAgentDefinition _definition;
    private readonly SquadRuntimeOptions _options;
    private readonly IReadOnlyCollection<SquadAgentDefinition> _customAgentDefinitions;
    private GitHubCopilotAgent? _inner;
    private CopilotClient? _client;

    public SquadAgent(
        SquadAgentDefinition definition,
        SquadRuntimeOptions options,
        IReadOnlyCollection<SquadAgentDefinition> customAgentDefinitions)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _customAgentDefinitions = customAgentDefinitions ?? throw new ArgumentNullException(nameof(customAgentDefinitions));
    }

    protected override string? IdCore => _definition.Id;

    public override string? Name => _definition.Name;

    public override string? Description => _definition.Description;

    protected override async ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
    {
        var agent = await EnsureInnerAsync(cancellationToken).ConfigureAwait(false);
        return await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var agent = await EnsureInnerAsync(cancellationToken).ConfigureAwait(false);
        return await agent.SerializeSessionAsync(session, jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    protected override async ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var agent = await EnsureInnerAsync(cancellationToken).ConfigureAwait(false);
        return await agent.DeserializeSessionAsync(serializedState, jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity($"real_squad.agent.{_definition.Id}.run", ActivityKind.Internal);
        SetAgentTags(activity);
        try
        {
            var agent = await EnsureInnerAsync(cancellationToken).ConfigureAwait(false);
            var response = await agent.RunAsync(AddSquadBoundary(messages), session, options, cancellationToken).ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity($"real_squad.agent.{_definition.Id}.run", ActivityKind.Internal);
        SetAgentTags(activity);

        var agent = await EnsureInnerAsync(cancellationToken).ConfigureAwait(false);
        await foreach (var update in agent.RunStreamingAsync(AddSquadBoundary(messages), session, options, cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    private void SetAgentTags(Activity? activity)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("squad.agent.id", _definition.Id);
        activity.SetTag("squad.agent.name", _definition.Name);
        activity.SetTag("squad.agent.role", _definition.Role);
        activity.SetTag("squad.agent.title", _definition.Title);
        activity.SetTag("squad.agent.description", _definition.Description);
        activity.SetTag("squad.agent.charter_path", _definition.CharterPath);
        activity.SetTag("squad.agent.system_prompt.length", _definition.SystemPrompt?.Length);
    }

    private async ValueTask<GitHubCopilotAgent> EnsureInnerAsync(CancellationToken cancellationToken)
    {
        if (_inner is not null)
        {
            return _inner;
        }

        _client = new CopilotClient(SquadAgentFactory.CreateClientOptions(_options));
        await _client.StartAsync(cancellationToken).ConfigureAwait(false);
        _inner = new GitHubCopilotAgent(
            _client,
            SquadAgentFactory.CreateSessionConfig(
                _definition,
                _options,
                _customAgentDefinitions,
                sessionEvent =>
                {
                    if (!CopilotSessionTraceMapper.ShouldEmitSessionEvent(sessionEvent))
                    {
                        return;
                    }

                    _options.OnCopilotSessionEvent?.Invoke(
                        CopilotSessionTraceMapper.FromSessionEvent(
                            _definition.Id,
                            sessionEvent,
                            _options.IncludeRawCopilotSessionContent));
                }),
            false,
            _definition.Id,
            _definition.Name,
            _definition.Description);

        return _inner;
    }

    private static IEnumerable<ChatMessage> AddSquadBoundary(IEnumerable<ChatMessage> messages)
    {
        yield return BoundaryMessage;

        foreach (var message in messages)
        {
            yield return message;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_inner is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }

        _client?.Dispose();
    }
}
