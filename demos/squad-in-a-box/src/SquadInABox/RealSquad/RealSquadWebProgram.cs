using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Channels;

namespace SquadInABox.RealSquad;

public static class RealSquadWebProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        var runnerArgs = args
            .Where(arg => !string.Equals(arg, "--real-squad", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("maf-workflow"));
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.AddOtlpExporter(ConfigureOtlpExporter);
        });

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("maf-workflow"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(RealSquadTelemetry.MeterName)
                .AddOtlpExporter(ConfigureOtlpExporter))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(RealSquadTelemetry.ActivitySourceName)
                .AddOtlpExporter(ConfigureOtlpExporter));

        builder.Services.AddSingleton<RealSquadTelemetry>();
        builder.Services.AddSingleton(new RealSquadRunnerState(runnerArgs));
        builder.Services.AddSingleton<WorkflowRunTraceStore>();
        builder.Services.AddSingleton<RealSquadWorkflowTrigger>();
        builder.Services.AddHostedService<RealSquadTriggeredRunner>();

        var app = builder.Build();

        app.MapGet("/", (RealSquadRunnerState state, WorkflowRunTraceStore traceStore) => Results.Json(state.GetSnapshot(traceStore)));
        app.MapGet("/health", () => Results.Ok("healthy"));
        app.MapGet("/status", (RealSquadRunnerState state, WorkflowRunTraceStore traceStore) => Results.Json(state.GetSnapshot(traceStore)));
        app.MapGet("/trace", (WorkflowRunTraceStore traceStore) => Results.Json(traceStore.GetAllTraces()));
        app.MapPost("/incidents/simulate", (HttpRequest request, RealSquadWorkflowTrigger trigger) =>
        {
            var incident = SimulatedIncident.From(request.Query);
            return trigger.TryTrigger(incident)
                ? Results.Accepted("/status", trigger.State.GetSnapshot(trigger.TraceStore))
                : Results.Conflict(trigger.State.GetSnapshot(trigger.TraceStore));
        });

        await app.RunAsync();
        return 0;
    }

    private static void ConfigureOtlpExporter(OtlpExporterOptions options)
    {
        options.Protocol = OtlpExportProtocol.Grpc;
    }
}

public sealed record SimulatedIncident(string Id, string Title, string Severity, DateTimeOffset TriggeredAt)
{
    public static SimulatedIncident From(IQueryCollection query)
    {
        var title = ReadQueryValue(query, "title") ?? "Simulated production incident";
        var severity = ReadQueryValue(query, "severity") ?? "Sev2";
        return new SimulatedIncident(Guid.NewGuid().ToString("n"), title, severity, DateTimeOffset.UtcNow);
    }

    private static string? ReadQueryValue(IQueryCollection query, string key)
    {
        var value = query[key].ToString().Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}

public sealed class RealSquadRunnerState
{
    private readonly object _gate = new();

    public RealSquadRunnerState(string[] args)
    {
        Args = args;
        Status = "WaitingForIncident";
        LastMessage = "POST /incidents/simulate to trigger the Real Squad workflow.";
    }

    public string[] Args { get; }

    public string Status { get; private set; }

    public int? ExitCode { get; private set; }

    public string LastMessage { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public SimulatedIncident? CurrentIncident { get; private set; }

    public RealSquadObservation? LastSquadObservation { get; private set; }

    public bool TryMarkQueued(SimulatedIncident incident)
    {
        lock (_gate)
        {
            if (Status is "Queued" or "Running")
            {
                return false;
            }

            Status = "Queued";
            StartedAt = null;
            CompletedAt = null;
            ExitCode = null;
            CurrentIncident = incident;
            LastSquadObservation = null;
            LastMessage = $"Incident {incident.Id} queued. Real Squad workflow will start shortly.";
            return true;
        }
    }

    public void MarkRunning(SimulatedIncident incident)
    {
        lock (_gate)
        {
            Status = "Running";
            StartedAt = DateTimeOffset.UtcNow;
            CompletedAt = null;
            ExitCode = null;
            CurrentIncident = incident;
            LastSquadObservation = null;
            LastMessage = $"Real Squad workflow is handling {incident.Severity} incident: {incident.Title}.";
        }
    }

    public void MarkSquadObserved(SquadRuntimeDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);

        var observation = RealSquadObservation.From(description);
        lock (_gate)
        {
            LastSquadObservation = observation;
            LastMessage = $"Real Squad workflow loaded {observation.AgentsLoaded} agents: {observation.AgentNames}.";
        }
    }

    public void MarkCompleted(int exitCode)
    {
        lock (_gate)
        {
            Status = exitCode == 0 ? "Completed" : "Failed";
            ExitCode = exitCode;
            CompletedAt = DateTimeOffset.UtcNow;
            LastMessage = exitCode == 0
                ? LastSquadObservation is null
                    ? "Real Squad workflow completed. POST /incidents/simulate to trigger it again."
                    : $"Real Squad workflow completed after loading {LastSquadObservation.AgentsLoaded} agents: {LastSquadObservation.AgentNames}."
                : $"Real Squad workflow failed with exit code {exitCode}. POST /incidents/simulate to retry.";
        }
    }

    public void MarkFailed(Exception exception)
    {
        lock (_gate)
        {
            Status = "Failed";
            ExitCode = 2;
            CompletedAt = DateTimeOffset.UtcNow;
            LastMessage = exception.Message;
        }
    }

    public void MarkTriggerRejected(SimulatedIncident incident, string reason)
    {
        lock (_gate)
        {
            Status = "Failed";
            ExitCode = 2;
            CompletedAt = DateTimeOffset.UtcNow;
            CurrentIncident = incident;
            LastMessage = reason;
        }
    }

    public object GetSnapshot(WorkflowRunTraceStore? traceStore = null)
    {
        lock (_gate)
        {
            var snapshot = new
            {
                service = "maf-workflow",
                mode = "real-squad",
                status = Status,
                exitCode = ExitCode,
                startedAt = StartedAt,
                completedAt = CompletedAt,
                message = LastMessage,
                incident = CurrentIncident,
                squad = LastSquadObservation,
                triggerEndpoint = "POST /incidents/simulate?severity=Sev2&title=Database%20latency",
                recentTrace = traceStore?.GetRecentTrace()
            };
            return snapshot;
        }
    }
}

public sealed record RealSquadObservation(
    string TeamRoot,
    string SquadRoot,
    string ToolPolicyPath,
    string YoloMode,
    int AgentsLoaded,
    int NativeMafAgentsConstructed,
    string AgentNames,
    IReadOnlyList<RealSquadAgentSnapshot> Agents)
{
    public static RealSquadObservation From(SquadRuntimeDescription description)
    {
        var agents = description.Agents
            .OrderBy(agent => agent.Definition.Id, StringComparer.OrdinalIgnoreCase)
            .Select(agent => new RealSquadAgentSnapshot(
                agent.Definition.Id,
                agent.Definition.Name,
                agent.Definition.Role,
                agent.Model,
                agent.NativeStatus))
            .ToArray();

        return new RealSquadObservation(
            description.TeamRoot,
            description.SquadRoot,
            description.ToolPolicyPath,
            description.YoloMode.ToString(),
            agents.Length,
            description.ConstructedNativeAgentCount,
            string.Join(", ", agents.Select(agent => agent.Name)),
            agents);
    }
}

public sealed record RealSquadAgentSnapshot(
    string Id,
    string Name,
    string Role,
    string Model,
    string AdapterStatus);

public sealed class RealSquadWorkflowTrigger
{
    private readonly Channel<SimulatedIncident> _incidents = Channel.CreateUnbounded<SimulatedIncident>();
    private readonly ILogger<RealSquadWorkflowTrigger> _logger;
    private readonly RealSquadTelemetry _telemetry;

    public RealSquadWorkflowTrigger(
        RealSquadRunnerState state,
        WorkflowRunTraceStore traceStore,
        RealSquadTelemetry telemetry,
        ILogger<RealSquadWorkflowTrigger> logger)
    {
        State = state;
        TraceStore = traceStore;
        _telemetry = telemetry;
        _logger = logger;
    }

    public RealSquadRunnerState State { get; }

    public WorkflowRunTraceStore TraceStore { get; }

    public ChannelReader<SimulatedIncident> Incidents => _incidents.Reader;

    public bool TryTrigger(SimulatedIncident incident)
    {
        if (!State.TryMarkQueued(incident))
        {
            _logger.LogWarning(
                "Rejected simulated incident trigger {IncidentId} because workflow status is {Status}.",
                incident.Id,
                State.Status);
            return false;
        }

        if (_incidents.Writer.TryWrite(incident))
        {
            var runId = incident.Id;
            TraceStore.AddEvent(runId, "IncidentQueued", $"Incident {incident.Id} queued with severity {incident.Severity}: {incident.Title}");
            _logger.LogInformation(
                "Queued simulated incident {IncidentId} with severity {Severity} and title {Title}.",
                incident.Id,
                incident.Severity,
                incident.Title);
            _telemetry.RecordIncidentTriggered(incident);
            return true;
        }

        State.MarkTriggerRejected(incident, "Incident trigger queue is closed.");
        _logger.LogError("Failed to queue simulated incident {IncidentId} because the trigger queue is closed.", incident.Id);
        return false;
    }
}

public sealed class RealSquadTriggeredRunner : BackgroundService
{
    private readonly RealSquadRunnerState _state;
    private readonly WorkflowRunTraceStore _traceStore;
    private readonly ILogger<RealSquadTriggeredRunner> _logger;
    private readonly RealSquadTelemetry _telemetry;
    private readonly RealSquadWorkflowTrigger _trigger;

    public RealSquadTriggeredRunner(
        RealSquadRunnerState state,
        WorkflowRunTraceStore traceStore,
        RealSquadWorkflowTrigger trigger,
        RealSquadTelemetry telemetry,
        ILogger<RealSquadTriggeredRunner> logger)
    {
        _state = state;
        _traceStore = traceStore;
        _trigger = trigger;
        _telemetry = telemetry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var incident in _trigger.Incidents.ReadAllAsync(stoppingToken))
        {
            var runId = incident.Id;
            _state.MarkRunning(incident);
            using var activity = _telemetry.StartIncidentActivity(incident);
            var started = Stopwatch.GetTimestamp();
            
            _traceStore.AddEvent(runId, "WorkflowStarted", $"Real Squad workflow started for incident {incident.Id} with severity {incident.Severity}");
            _logger.LogInformation(
                "Real Squad workflow started for incident {IncidentId} with severity {Severity}.",
                incident.Id,
                incident.Severity);

            try
            {
                var exitCode = await RealSquadProgram.RunAsync(_state.Args, description =>
                {
                    var observation = RealSquadObservation.From(description);
                    _state.MarkSquadObserved(description);
                    activity?.SetTag("squad.agents", observation.AgentNames);
                    activity?.SetTag("squad.agents.count", observation.AgentsLoaded);
                    activity?.SetTag("squad.native_agents.constructed", observation.NativeMafAgentsConstructed);

                    _traceStore.AddEvent(runId, "SquadRuntimeDescribed", $"Squad runtime loaded: {observation.TeamRoot}");
                    _logger.LogInformation(
                        "Real Squad workflow loaded {AgentCount} agent(s): {Agents}. Native MAF agents constructed: {ConstructedNativeAgentCount}.",
                        observation.AgentsLoaded,
                        observation.AgentNames,
                        observation.NativeMafAgentsConstructed);

                    foreach (var agent in observation.Agents)
                    {
                        using var agentActivity = _telemetry.StartAgentActivity(incident, agent);
                        _traceStore.AddEvent(runId, "AgentAvailable", $"{agent.Id} ({agent.Name}), role: {agent.Role}, model: {agent.Model}, adapter status: {agent.AdapterStatus}");
                        _logger.LogInformation(
                            "Real Squad agent available: {AgentId} ({AgentName}), role {AgentRole}, model {Model}, adapter status {AdapterStatus}.",
                            agent.Id,
                            agent.Name,
                            agent.Role,
                            agent.Model,
                            agent.AdapterStatus);
                        agentActivity?.SetStatus(ActivityStatusCode.Ok);
                    }
                });
                _state.MarkCompleted(exitCode);
                var duration = Stopwatch.GetElapsedTime(started);
                _telemetry.RecordWorkflowCompleted(incident, exitCode, duration);
                _traceStore.AddEvent(runId, "WorkflowCompleted", $"Workflow completed with exit code {exitCode} in {duration.TotalMilliseconds:F2} ms");
                _logger.LogInformation(
                    "Real Squad workflow completed for incident {IncidentId} with exit code {ExitCode} in {DurationMs} ms.",
                    incident.Id,
                    exitCode,
                    duration.TotalMilliseconds);

                if (exitCode == 0)
                {
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }
                else
                {
                    activity?.SetStatus(ActivityStatusCode.Error, $"Real Squad workflow failed with exit code {exitCode}.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _state.MarkCompleted(0);
                var duration = Stopwatch.GetElapsedTime(started);
                _telemetry.RecordWorkflowCompleted(incident, 0, duration);
                _traceStore.AddEvent(runId, "WorkflowStopped", $"Workflow stopped after {duration.TotalMilliseconds:F2} ms");
                _logger.LogInformation(
                    "Real Squad workflow stopped for incident {IncidentId} after {DurationMs} ms.",
                    incident.Id,
                    duration.TotalMilliseconds);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception exception)
            {
                _state.MarkFailed(exception);
                var duration = Stopwatch.GetElapsedTime(started);
                _telemetry.RecordWorkflowFailed(incident, duration);
                _traceStore.AddEvent(runId, "WorkflowFailed", $"Workflow failed after {duration.TotalMilliseconds:F2} ms: {exception.Message}");
                _logger.LogError(
                    exception,
                    "Real Squad workflow failed for incident {IncidentId} after {DurationMs} ms.",
                    incident.Id,
                    duration.TotalMilliseconds);
                activity?.AddException(exception);
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            }
        }
    }
}

public sealed class RealSquadTelemetry : IDisposable
{
    public const string ActivitySourceName = "SquadInABox.RealSquad";
    public const string MeterName = "SquadInABox.RealSquad";

    private readonly ActivitySource _activitySource = new(ActivitySourceName);
    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _incidentsTriggered;
    private readonly Counter<long> _workflowsCompleted;
    private readonly Histogram<double> _workflowDurationSeconds;

    public RealSquadTelemetry()
    {
        _incidentsTriggered = _meter.CreateCounter<long>("real_squad.incidents.triggered");
        _workflowsCompleted = _meter.CreateCounter<long>("real_squad.workflows.completed");
        _workflowDurationSeconds = _meter.CreateHistogram<double>("real_squad.workflow.duration", unit: "s");
    }

    public Activity? StartIncidentActivity(SimulatedIncident incident)
    {
        var activity = _activitySource.StartActivity("real_squad.incident", ActivityKind.Internal);
        activity?.SetTag("incident.id", incident.Id);
        activity?.SetTag("incident.title", incident.Title);
        activity?.SetTag("incident.severity", incident.Severity);
        return activity;
    }

    public Activity? StartAgentActivity(SimulatedIncident incident, RealSquadAgentSnapshot agent)
    {
        var activity = _activitySource.StartActivity($"real_squad.agent.{agent.Id}", ActivityKind.Internal);
        activity?.SetTag("incident.id", incident.Id);
        activity?.SetTag("incident.severity", incident.Severity);
        activity?.SetTag("squad.agent.id", agent.Id);
        activity?.SetTag("squad.agent.name", agent.Name);
        activity?.SetTag("squad.agent.role", agent.Role);
        activity?.SetTag("squad.agent.model", agent.Model);
        activity?.SetTag("squad.agent.adapter_status", agent.AdapterStatus);
        return activity;
    }

    public void RecordIncidentTriggered(SimulatedIncident incident)
    {
        _incidentsTriggered.Add(1, CreateIncidentTags(incident));
    }

    public void RecordWorkflowCompleted(SimulatedIncident incident, int exitCode, TimeSpan duration)
    {
        var tags = CreateWorkflowTags(incident, exitCode == 0 ? "completed" : "failed", exitCode);
        _workflowsCompleted.Add(1, tags);
        _workflowDurationSeconds.Record(duration.TotalSeconds, tags);
    }

    public void RecordWorkflowFailed(SimulatedIncident incident, TimeSpan duration)
    {
        var tags = CreateWorkflowTags(incident, "failed", 2);
        _workflowsCompleted.Add(1, tags);
        _workflowDurationSeconds.Record(duration.TotalSeconds, tags);
    }

    public void Dispose()
    {
        _activitySource.Dispose();
        _meter.Dispose();
    }

    private static TagList CreateIncidentTags(SimulatedIncident incident)
    {
        var tags = new TagList
        {
            { "incident.severity", incident.Severity },
            { "incident.title", incident.Title }
        };
        return tags;
    }

    private static TagList CreateWorkflowTags(SimulatedIncident incident, string status, int exitCode)
    {
        var tags = CreateIncidentTags(incident);
        tags.Add("workflow.status", status);
        tags.Add("workflow.exit_code", exitCode);
        return tags;
    }
}

public sealed class WorkflowRunTraceStore
{
    private readonly ConcurrentDictionary<string, WorkflowRunTrace> _traces = new();
    private const int MaxTracesPerRun = 100;

    public void AddEvent(string runId, string eventType, string message)
    {
        var trace = _traces.GetOrAdd(runId, _ => new WorkflowRunTrace(runId));
        trace.AddEvent(eventType, message);
    }

    public object GetAllTraces()
    {
        var traces = _traces.Values
            .OrderByDescending(t => t.StartedAt)
            .Select(t => new
            {
                runId = t.RunId,
                startedAt = t.StartedAt,
                eventCount = t.Events.Count,
                events = t.Events.Select(e => new
                {
                    timestamp = e.Timestamp,
                    eventType = e.EventType,
                    message = e.Message
                })
            })
            .ToList();

        return new { traces, totalRuns = traces.Count };
    }

    public object? GetRecentTrace()
    {
        var recentTrace = _traces.Values
            .OrderByDescending(t => t.StartedAt)
            .FirstOrDefault();

        if (recentTrace == null)
        {
            return null;
        }

        return new
        {
            runId = recentTrace.RunId,
            startedAt = recentTrace.StartedAt,
            eventCount = recentTrace.Events.Count,
            recentEvents = recentTrace.Events
                .OrderByDescending(e => e.Timestamp)
                .Take(10)
                .Select(e => new
                {
                    timestamp = e.Timestamp,
                    eventType = e.EventType,
                    message = e.Message
                })
        };
    }
}

public sealed class WorkflowRunTrace
{
    private readonly ConcurrentQueue<WorkflowTraceEvent> _events = new();

    public WorkflowRunTrace(string runId)
    {
        RunId = runId;
        StartedAt = DateTime.UtcNow;
    }

    public string RunId { get; }
    public DateTime StartedAt { get; }
    public IReadOnlyCollection<WorkflowTraceEvent> Events => _events.ToArray();

    public void AddEvent(string eventType, string message)
    {
        _events.Enqueue(new WorkflowTraceEvent(eventType, message));
    }
}

public sealed class WorkflowTraceEvent
{
    public WorkflowTraceEvent(string eventType, string message)
    {
        Timestamp = DateTime.UtcNow;
        EventType = eventType;
        Message = message;
    }

    public DateTime Timestamp { get; }
    public string EventType { get; }
    public string Message { get; }
}
