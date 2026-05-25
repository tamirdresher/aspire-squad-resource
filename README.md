# Aspire Squad Resource

This sample shows how to model a Squad AI-agent team as a first-class .NET Aspire resource.

The AppHost adds logical `SquadResource` rows to the Aspire dashboard, exposes squad metadata as resource properties, wires a workflow project with `.WithReference(mafSquad)`, and adds dashboard commands including **Open Copilot CLI**.

## Run

```powershell
dotnet build .\AspireSquadResource.sln
dotnet run --project .\src\Squad.Hosting.Aspire.Demo\Squad.Hosting.Aspire.Demo.csproj
```

Open the Aspire dashboard from the AppHost output. The demo uses `samples\sample-squad` as a synthetic, public-safe `.squad` workspace.

Set `UPSTREAM_SQUAD_ROOT` to a local clone of an upstream Squad workspace if you also want the optional `upstream-squad` row to appear in the dashboard.

## What is included

- `src\CommunityToolkit.Aspire.Hosting.Squad` — Aspire hosting integration and `builder.AddSquad(...)`.
- `src\Squad.Hosting.Aspire.Demo` — AppHost wiring multiple Squad resources and the MAF workflow.
- `demos\squad-in-a-box\src\SquadInABox` — sample workflow process with `/status` and `POST /incidents/simulate`.
- `docs\blog-aspire-squad-resource.md` and screenshots — the write-up and dashboard proof.

`squad://resource/...` is a descriptor string for Aspire metadata and dependency injection. It is not a public network protocol and does not imply that each squad row starts a server.
