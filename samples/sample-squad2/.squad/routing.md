# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Incident command | Sisko | Severity declaration, priorities, command cadence, decision log |
| SRE mitigation | Kira | Containment, rollback/roll-forward, traffic shaping, customer impact reduction |
| .NET distributed systems | Dax | ASP.NET Core, Aspire, resilience policies, async/concurrency defects |
| Observability & diagnostics | Bashir | Logs, traces, metrics, health checks, root-cause evidence |
| Security response | Worf | Secret exposure, auth failures, threat containment, security hotfixes |
| Compliance & audit | Odo | Regulatory impact, evidence preservation, incident records, policy adherence |
| Azure DevOps / platform | O'Brien | Azure resources, IaC, CI/CD, deployment rings, runtime infrastructure |
| Database reliability | Rom | SQL/Cosmos/Postgres health, locks, migrations, backups, data integrity |
| Data science | Nog | Anomaly detection, incident correlation, forecasting, noisy-signal reduction |
| Communications | Quark | Status updates, stakeholder messaging, post-incident summaries |
| Code review | Sisko | Review PRs, check quality, coordinate domain review gates |
| Testing | Dax | Write tests, find edge cases, verify fixes for .NET distributed behavior |
| Scope & priorities | Sisko | What to build next, trade-offs, decisions |
| Session logging | Scribe | Automatic — never needs routing |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Sisko |
| `squad:{name}` | Pick up issue and complete the work | Named member |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, **Sisko** triages it — analyzing content, assigning the right `squad:{member}` label, and commenting with triage notes.
2. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
3. Members can reassign by removing their label and adding another member's label.
4. The `squad` label is the "inbox" — untriaged issues waiting for Lead review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. The Lead handles all `squad` (base label) triage.
