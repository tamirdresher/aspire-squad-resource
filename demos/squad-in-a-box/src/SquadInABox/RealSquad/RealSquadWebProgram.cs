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
        builder.Services.AddSingleton<RealSquadWorkflowTrigger>();
        builder.Services.AddHostedService<RealSquadTriggeredRunner>();

        var app = builder.Build();

        app.MapGet("/", (RealSquadRunnerState state) => Results.Json(state.GetSnapshot()));
        app.MapGet("/health", () => Results.Ok("healthy"));
        app.MapGet("/status", (RealSquadRunnerState state) => Results.Json(state.GetSnapshot()));
        app.MapPost("/incidents/simulate", (HttpRequest request, RealSquadWorkflowTrigger trigger) =>
        {
            var incident = SimulatedIncident.From(request.Query);
            return trigger.TryTrigger(incident)
                ? Results.Accepted("/status", trigger.State.GetSnapshot())
                : Results.Conflict(trigger.State.GetSnapshot());
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
            LastMessage = $"Real Squad workflow is handling {incident.Severity} incident: {incident.Title}.";
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
                ? "Real Squad workflow completed. POST /incidents/simulate to trigger it again."
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

    public object GetSnapshot()
    {
        lock (_gate)
        {
            return new
            {
                service = "maf-workflow",
                mode = "real-squad",
                status = Status,
                exitCode = ExitCode,
                startedAt = StartedAt,
                completedAt = CompletedAt,
                message = LastMessage,
                incident = CurrentIncident,
                triggerEndpoint = "POST /incidents/simulate?severity=Sev2&title=Database%20latency"
            };
        }
    }
}

public sealed class RealSquadWorkflowTrigger
{
    private readonly Channel<SimulatedIncident> _incidents = Channel.CreateUnbounded<SimulatedIncident>();
    private readonly ILogger<RealSquadWorkflowTrigger> _logger;
    private readonly RealSquadTelemetry _telemetry;

    public RealSquadWorkflowTrigger(
        RealSquadRunnerState state,
        RealSquadTelemetry telemetry,
        ILogger<RealSquadWorkflowTrigger> logger)
    {
        State = state;
        _telemetry = telemetry;
        _logger = logger;
    }

    public RealSquadRunnerState State { get; }

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
    private readonly ILogger<RealSquadTriggeredRunner> _logger;
    private readonly RealSquadTelemetry _telemetry;
    private readonly RealSquadWorkflowTrigger _trigger;

    public RealSquadTriggeredRunner(
        RealSquadRunnerState state,
        RealSquadWorkflowTrigger trigger,
        RealSquadTelemetry telemetry,
        ILogger<RealSquadTriggeredRunner> logger)
    {
        _state = state;
        _trigger = trigger;
        _telemetry = telemetry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var incident in _trigger.Incidents.ReadAllAsync(stoppingToken))
        {
            _state.MarkRunning(incident);
            using var activity = _telemetry.StartIncidentActivity(incident);
            var started = Stopwatch.GetTimestamp();
            _logger.LogInformation(
                "Real Squad workflow started for incident {IncidentId} with severity {Severity}.",
                incident.Id,
                incident.Severity);

            try
            {
                var exitCode = await RealSquadProgram.RunAsync(_state.Args);
                _state.MarkCompleted(exitCode);
                var duration = Stopwatch.GetElapsedTime(started);
                _telemetry.RecordWorkflowCompleted(incident, exitCode, duration);
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
