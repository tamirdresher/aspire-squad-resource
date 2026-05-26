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
3. Use its endpoint link in the **Urls** column to open the workflow status JSON (`/status` is also available).
4. Trigger the sample incident with:

```powershell
$baseUrl = "<maf-workflow url from the Aspire dashboard>"
Invoke-RestMethod -Method Post "$baseUrl/incidents/simulate?severity=Sev2&title=Database%20latency"
Invoke-RestMethod "$baseUrl/status"
```

The `maf-workflow` resource references `maf-squad` with `.WithReference(mafSquad)`, so the API runs against the same sample `.squad` workspace represented by the `maf-squad` dashboard row. Running `dotnet run` directly in `demos\squad-in-a-box\src\SquadInABox` still starts the original terminal demo; the AppHost path starts the API through Aspire endpoint configuration.

## What is included

- `src\CommunityToolkit.Aspire.Hosting.Squad` — Aspire hosting integration and `builder.AddSquad(...)`.
- `src\Squad.Hosting.Aspire.Demo` — AppHost wiring multiple Squad resources and the MAF workflow.
- `demos\squad-in-a-box\src\SquadInABox` — sample workflow process with `/status` and `POST /incidents/simulate`.
- `docs\blog-aspire-squad-resource.md` and screenshots — the write-up and dashboard proof.

`squad://resource/...` is a descriptor string for Aspire metadata and dependency injection. It is not a public network protocol and does not imply that each squad row starts a server.
