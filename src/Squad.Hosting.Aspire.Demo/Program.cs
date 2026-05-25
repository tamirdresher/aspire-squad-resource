using Aspire.Hosting;

// Squad.Hosting.Aspire.Demo - AppHost
// Demonstrates Squad as a first-class .NET Aspire resource.
//
// Resources visible in the Aspire dashboard after running this AppHost:
//   research-squad          [Active]     logical squad resource; squad://resource/research-squad?teamRoot=...&agents=...
//   incident-team           [Active]     logical squad resource; squad://resource/incident-team?teamRoot=...&agents=...
//   maf-squad               [Active]     logical squad resource; squad://resource/maf-squad?teamRoot=...&agents=...
//   maf-workflow            [Project]    demos/squad-in-a-box real-Squad MAF web-hosted runner
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
    .WithArgs("--real-squad", "--team-root", sampleSquadRoot);

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
