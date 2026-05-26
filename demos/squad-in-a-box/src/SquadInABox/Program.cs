// ═══════════════════════════════════════════════════════════════════════════
//  Squad in a Box  ·  Program.cs
//
//  Entry point for the real .squad → Copilot-backed Microsoft Agent Framework
//  workflow. The only runtime path is the live "Real Squad" web host, which
//  exposes /status, /trace, and /incidents/simulate endpoints and triggers
//  the constructed SquadAgent (Microsoft.Agents.AI.AIAgent wrapper around
//  GitHub.Copilot.SDK) for each simulated incident.
//
//  Quick start:
//    $env:HTTP_PORTS = "8080"
//    dotnet run -- --real-squad --team-root <path-to-squad-workspace>
//
//  Under Aspire (the supported flow), AppHost wires HTTP_PORTS and the
//  --real-squad / --construct / --trace-raw-copilot-content arguments.
// ═══════════════════════════════════════════════════════════════════════════

using SquadInABox.RealSquad;

return await RealSquadWebProgram.RunAsync(args);
