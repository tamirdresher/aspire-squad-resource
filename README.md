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

After **Trigger Incident** completes, `GET /status` includes a `squad` object with the loaded agent names, roles, models, and adapter status. That is the in-band proof that the HTTP trigger ran the real Squad adapter workflow, not just a placeholder endpoint. In the default sample, look for:

```json
{
  "status": "Completed",
  "exitCode": 0,
  "squad": {
    "agentsLoaded": 6,
    "agentNames": "Data, Picard, Ralph, Scribe, Seven, Worf",
    "nativeMafAgentsConstructed": 0
  }
}
```

The `maf-workflow` console logs also emit one `Real Squad agent available` line per agent, including the role, model, and adapter status. `nativeMafAgentsConstructed` is `0` by default because the safe demo path describes the Copilot-backed MAF agents without requiring Brady to authenticate live Copilot agent construction; pass `--construct` only when live Copilot auth is configured.

The `maf-workflow` resource references `maf-squad` with `.WithReference(mafSquad)`, so the API runs against the same sample `.squad` workspace represented by the `maf-squad` dashboard row. Running `dotnet run` directly in `demos\squad-in-a-box\src\SquadInABox` still starts the original terminal demo; the AppHost path starts the API through Aspire endpoint configuration.

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
        { "timestamp": "2024-01-15T10:30:03Z", "eventType": "AgentAvailable", "message": "data (Data), role: Code Expert, model: gpt-5, adapter status: described" },
        { "timestamp": "2024-01-15T10:30:10Z", "eventType": "WorkflowCompleted", "message": "Exit code: 0, Duration: 9.2s" }
      ]
    }
  ],
  "totalRuns": 1
}
```

This trace data provides visibility into the Squad workflow lifecycle, agent loading sequence, and execution timing. It is observable runtime proof, not a raw transcript of private subagent reasoning or conversation.

## What is included

- `src\CommunityToolkit.Aspire.Hosting.Squad` — Aspire hosting integration and `builder.AddSquad(...)`.
- `src\Squad.Hosting.Aspire.Demo` — AppHost wiring multiple Squad resources and the MAF workflow.
- `demos\squad-in-a-box\src\SquadInABox` — sample workflow process with `/status` and `POST /incidents/simulate`.
- `docs\blog-aspire-squad-resource.md` and screenshots — the write-up and dashboard proof.

`squad://resource/...` is a descriptor string for Aspire metadata and dependency injection. It is not a public network protocol and does not imply that each squad row starts a server.
