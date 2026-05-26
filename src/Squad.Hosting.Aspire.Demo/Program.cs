using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

// Squad.Hosting.Aspire.Demo - AppHost
// Demonstrates Squad as a first-class .NET Aspire resource.
//
// Resources visible in the Aspire dashboard after running this AppHost:
//   research-squad          [Active]     logical squad resource; squad://resource/research-squad?teamRoot=...&agents=...
//   incident-team           [Active]     logical squad resource; squad://resource/incident-team?teamRoot=...&agents=...
//   maf-squad               [Active]     logical squad resource; squad://resource/maf-squad?teamRoot=...&agents=...
//   maf-workflow            [Project]    demos/squad-in-a-box real-Squad MAF web-hosted runner with HTTP endpoint
//   upstream-squad          [Active]     logical squad resource; squad://resource/upstream-squad?teamRoot=...&agents=... (if cloned locally)
//
// Agent rosters are exposed as resource properties on the squad rows rather than as
// separate top-level dashboard resources.

var builder = DistributedApplication.CreateBuilder(args);
var repoRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", ".."));
var sampleSquadRoot = Path.Combine(repoRoot, "samples", "sample-squad");
var upstreamSquadRoot = Environment.GetEnvironmentVariable("UPSTREAM_SQUAD_ROOT");

// Squad resources are logical Aspire resources. They read a .squad/team.md roster
// from a workspace root and expose that roster as dashboard metadata.
builder.AddSquad("research-squad",
    teamRoot: sampleSquadRoot);

// Second squad: same sample workspace, different resource identity.
builder.AddSquad("incident-team",
    teamRoot: sampleSquadRoot);

var mafSquad = builder.AddSquad("maf-squad",
    teamRoot: sampleSquadRoot);

builder.AddProject<Projects.SquadInABox>("maf-workflow")
    .WithReference(mafSquad)
    .WithArgs("--team-root", sampleSquadRoot)
    .WithHttpEndpoint(name: "http", env: "HTTP_PORTS")
    .WithHttpCommand(
        path: "/",
        displayName: "Show Snapshot",
        endpointName: "http",
        commandName: "show-snapshot",
        commandOptions: new HttpCommandOptions
        {
            Method = HttpMethod.Get,
            Description = "Calls GET / and reports the current workflow snapshot.",
            IconName = "DocumentData",
            IsHighlighted = true,
        })
    .WithHttpCommand(
        path: "/status",
        displayName: "Check Status",
        endpointName: "http",
        commandName: "check-status",
        commandOptions: new HttpCommandOptions
        {
            Method = HttpMethod.Get,
            Description = "Calls GET /status and reports the current workflow status.",
            IconName = "Pulse",
            IsHighlighted = true,
        })
    .WithHttpCommand(
        path: "/health",
        displayName: "Check Health",
        endpointName: "http",
        commandName: "check-health",
        commandOptions: new HttpCommandOptions
        {
            Method = HttpMethod.Get,
            Description = "Calls GET /health on the workflow API.",
            IconName = "HeartPulse",
            IsHighlighted = true,
        })
    .WithHttpCommand(
        path: "/incidents/simulate",
        displayName: "Trigger Incident",
        endpointName: "http",
        commandName: "trigger-incident",
        commandOptions: new HttpCommandOptions
        {
            Method = HttpMethod.Post,
            Description = "Calls POST /incidents/simulate to start the sample Real Squad workflow.",
            IconName = "Flash",
            IsHighlighted = true,
        });

if (!string.IsNullOrWhiteSpace(upstreamSquadRoot) && Directory.Exists(upstreamSquadRoot))
{
    builder.AddSquad("upstream-squad",
        teamRoot: upstreamSquadRoot);
}
else
{
    Console.WriteLine("Skipping upstream-squad resource; set UPSTREAM_SQUAD_ROOT to a local Squad clone to include it.");
}

// Build and run.
builder.Build().Run();
