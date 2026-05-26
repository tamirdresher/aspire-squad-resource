# Aspire Squad Resource

This sample shows how to model a Squad AI-agent team as a first-class .NET Aspire resource.

The AppHost adds logical `SquadResource` rows to the Aspire dashboard, exposes squad metadata as resource properties, wires a workflow project with `.WithReference(mafSquad)`, and adds dashboard commands including **Open Copilot CLI**.

## Run

```powershell
dotnet build .\AspireSquadResource.sln
dotnet run --project .\src\Squad.Hosting.Aspire.Demo\Squad.Hosting.Aspire.Demo.csproj
```

Open the Aspire dashboard URL printed by the AppHost output. The demo uses `samples\sample-squad` as a synthetic, public-safe `.squad` workspace.

Set `UPSTREAM_SQUAD_ROOT` to a local clone of an upstream Squad workspace if you also want the optional `upstream-squad` row to appear in the dashboard.

## Trigger the workflow from the Aspire dashboard

The squad rows (`research-squad`, `incident-team`, and `maf-squad`) are logical Aspire resources. They show roster metadata and commands, but they do not listen on HTTP.

The runnable ASP.NET Core API is the `maf-workflow` project resource. It has an HTTP endpoint in the Aspire dashboard. In the dashboard:

1. Open the **Resources** page.
2. Find the `maf-workflow` row.
3. Use its endpoint link in the **Urls** column to open the workflow snapshot JSON (`/status` is also available).
4. Use the `maf-workflow` **Actions** menu to run **Show Snapshot**, **Check Status**, **Check Health**, or **Trigger Incident** directly from the dashboard.
5. Or trigger the sample incident from PowerShell with:

```powershell
$baseUrl = "<maf-workflow url from the Aspire dashboard>"
Invoke-RestMethod -Method Post "$baseUrl/incidents/simulate?severity=Sev2&title=Database%20latency"
Invoke-RestMethod "$baseUrl/status"
```

You can also open `maf-workflow.http`, replace `@mafWorkflowUrl` with the `maf-workflow` URL from the Aspire dashboard, and run the included `GET /`, `GET /status`, `GET /health`, and `POST /incidents/simulate` requests from an HTTP file client.

After **Trigger Incident** completes, `GET /status` includes a `squad` object with the loaded agent names, roles, model, and adapter status. That is the in-band proof that the HTTP trigger ran the real Squad adapter workflow, not just a placeholder endpoint. In the default AppHost configuration, look for:

```json
{
  "status": "Completed",
  "exitCode": 0,
  "squad": {
    "agentsLoaded": 12,
    "agentNames": "Bashir - Observability & Diagnostics, Dax - .NET Distributed Systems Engineer, Kira - SRE Operations & Mitigation, Nog - Data Science & Anomaly Detection, O'Brien - Azure DevOps / Platform Engineer, Odo - Compliance & Audit Lead, Quark - Incident Communications Lead, Ralph, Rom - Database Reliability Engineer, Scribe, Sisko - Incident Commander / SRE Lead, Worf - Security Response Engineer",
    "nativeMafAgentsConstructed": 12
  }
}
```

The `maf-workflow` console logs also emit one `Real Squad agent available` line per agent, including the role, model, and adapter status. Every agent reports `adapter status: constructed:SquadInABox.RealSquad.SquadAgent` — a single `Microsoft.Agents.AI.AIAgent`-derived wrapper around `GitHub.Copilot.SDK`. The default AppHost path passes `--construct` so live MAF agents are built; pass `--no-construct` (or remove `--construct` from AppHost args) if you only want the describe-only path.

The `maf-workflow` resource references `maf-squad` with `.WithReference(mafSquad)`, so the API runs against the same sample `.squad` workspace represented by the `maf-squad` dashboard row. `dotnet run` directly in `demos\squad-in-a-box\src\SquadInABox` also starts the same Real Squad web host (the legacy terminal-UI demo was removed in the cleanup); under Aspire, the AppHost wires `HTTP_PORTS` and the workflow arguments automatically.

## SquadAgent — the single MAF wrapper

The workflow uses exactly one `Microsoft.Agents.AI.AIAgent` implementation:

```
demos\squad-in-a-box\src\SquadInABox\RealSquad\SquadAgent.cs           — public sealed class SquadAgent : AIAgent, IAsyncDisposable
demos\squad-in-a-box\src\SquadInABox\RealSquad\SquadAgentFactory.cs    — constructs the live agent from a charter
demos\squad-in-a-box\src\SquadInABox\RealSquad\SquadAgentRegistration.cs — metadata record (model, package versions, permission policy, NativeAgent reference)
```

`SquadAgent` wraps a `GitHubCopilotAgent` and forwards `RunAsync` / `RunStreamingAsync` / session APIs to it, prepending a Squad boundary system message. `SquadAgentRegistration` is **not** an agent — it is a `record` that holds the metadata Aspire surfaces in the dashboard (model, Copilot SDK package version, MAF package version, permission policy) plus an optional reference to the constructed native agent for describe-only runs.

## Workflow trace

The `maf-workflow` API includes a structured trace system to observe the internal workflow execution. After triggering an incident with `POST /incidents/simulate`, you can retrieve detailed event traces showing each stage of the workflow:

### Trace endpoints

- **GET /trace** — Returns all workflow run traces with complete event history
- **GET /status** — Includes `recentTrace` with the most recent workflow events

### Trace event types

Each workflow execution (identified by `runId`) emits the following structured events:

1. **IncidentQueued** — Incident accepted into processing channel with severity and title
2. **WorkflowStarted** — Squad workflow execution begins in background service
3. **SquadRuntimeDescribed** — Squad runtime loads and describes the team composition
4. **AgentAvailable** — Emitted for each agent loaded (includes role, model, and adapter status)
5. **WorkflowCompleted** — Successful completion with exit code and duration
6. **WorkflowFailed** — Exception occurred with error message
7. **WorkflowStopped** — Workflow cancelled gracefully

### Example trace usage

```powershell
# Trigger workflow
$baseUrl = "<maf-workflow url from the Aspire dashboard>"
Invoke-RestMethod -Method Post "$baseUrl/incidents/simulate?severity=Sev2&title=Database%20latency"

# Get full trace
Invoke-RestMethod "$baseUrl/trace"

# Get status with recent trace events
Invoke-RestMethod "$baseUrl/status"
```

The trace response includes:

```json
{
  "traces": [
    {
      "runId": "inc-abc123",
      "startedAt": "2024-01-15T10:30:00Z",
      "eventCount": 8,
      "events": [
        { "timestamp": "2024-01-15T10:30:00Z", "eventType": "IncidentQueued", "message": "Severity: Sev2, Title: Database latency" },
        { "timestamp": "2024-01-15T10:30:01Z", "eventType": "WorkflowStarted", "message": "Starting workflow for inc-abc123" },
        { "timestamp": "2024-01-15T10:30:02Z", "eventType": "SquadRuntimeDescribed", "message": "Squad loaded with 6 agents" },
        { "timestamp": "2024-01-15T10:30:03Z", "eventType": "AgentAvailable", "message": "sisko (Sisko - Incident Commander / SRE Lead), role: Incident Commander / SRE Lead, model: claude-sonnet-4.5, adapter status: constructed:SquadInABox.RealSquad.SquadAgent" },
        { "timestamp": "2024-01-15T10:30:10Z", "eventType": "WorkflowCompleted", "message": "Exit code: 0, Duration: 9.2s" }
      ]
    }
  ],
  "totalRuns": 1
}
```

This trace data provides visibility into the Squad workflow lifecycle, agent loading sequence, and execution timing. It is observable runtime proof, not a raw transcript of private subagent reasoning or conversation.

The same workflow also emits OpenTelemetry spans to the Aspire dashboard. In **Traces**, open the `maf-workflow` `real_squad.incident` trace and expand it to see one child span per loaded Squad agent, named `real_squad.agent.<agent-id>` with tags for the agent name, role, model, adapter status, and incident metadata.

When live Copilot-backed agents are constructed and executed, the demo also wires `GitHub.Copilot.SDK` session hooks into OpenTelemetry. Copilot SDK events are emitted as spans named `real_squad.copilot.<event-type>` and as `CopilotSessionEvent` entries in `/trace`. These spans record safe metadata such as event type, root agent id, SDK agent id, subagent name, tool name, tool-call id, model, duration, token/tool counts, status, content length, and content SHA-256. By default, they intentionally do **not** store raw prompts, assistant text, tool arguments, tool results, or private reasoning in Aspire traces.

For a local demo where you explicitly want to inspect the coordinator prompt/tool payloads and assistant/subagent responses, enable raw Copilot session tracing:

```powershell
$env:SQUAD_TRACE_RAW_COPILOT_CONTENT = "true"
dotnet run --project .\src\Squad.Hosting.Aspire.Demo\Squad.Hosting.Aspire.Demo.csproj
```

Or pass `--trace-raw-copilot-content` to the `SquadInABox` workflow runner. In that mode, `/trace` includes raw SDK event details when the SDK exposes them (`rawToolArguments`, `rawToolResult`, `rawAssistantContent`, and `rawSubagentDescription`), and Aspire trace spans add corresponding `copilot.*.raw` tags. This is intentionally opt-in because those values can contain prompts, repo data, tool results, credentials, or other sensitive content. Private model reasoning is still not exposed unless the SDK itself surfaces it as event content.

The default AppHost path now constructs live MAF agents (`--construct` is passed by AppHost) and enables raw Copilot session tracing (`--trace-raw-copilot-content`), so `nativeMafAgentsConstructed` is `12` against `samples\sample-squad2` and the deeper `real_squad.copilot.*` spans appear. The model is set in `samples\sample-squad2\.squad\config.json` (`defaultModel`); pick whatever the Copilot CLI on the current machine accepts.

## What is included

- `src\CommunityToolkit.Aspire.Hosting.Squad` — Aspire hosting integration and `builder.AddSquad(...)`.
- `src\Squad.Hosting.Aspire.Demo` — AppHost wiring multiple Squad resources and the MAF workflow.
- `demos\squad-in-a-box\src\SquadInABox` — Real-Squad web host (`/status`, `/trace`, `POST /incidents/simulate`) that constructs the single `SquadAgent : AIAgent` per charter and runs live coordinator → subagent handoffs through `GitHub.Copilot.SDK`.
- `docs\blog-aspire-squad-resource.md` and screenshots — the write-up and dashboard proof.

`squad://resource/...` is a descriptor string for Aspire metadata and dependency injection. It is not a public network protocol and does not imply that each squad row starts a server.
