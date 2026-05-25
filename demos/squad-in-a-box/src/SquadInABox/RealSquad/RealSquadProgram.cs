using Spectre.Console;

namespace SquadInABox.RealSquad;

public static class RealSquadProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        RejectUnsafePolicyBypassArgs(args);

        var loader = new SquadContextLoader();
        var teamRoot = ReadOption(args, "--team-root")
            ?? loader.FindTeamRoot(Environment.CurrentDirectory);

        teamRoot = Path.GetFullPath(teamRoot);
        var model = ReadOption(args, "--model")
            ?? Environment.GetEnvironmentVariable("SQUAD_MODEL")
            ?? loader.LoadDefaultModel(teamRoot);

        var requestedAgentIds = ReadRepeatedOption(args, "--agent");
        var constructNativeAgents = args.Contains("--construct", StringComparer.OrdinalIgnoreCase);
        var yoloMode = ReadYoloMode(args);
        var toolPolicyPath = Path.Combine(teamRoot, ".squad", "policies", "tool-allowlists.json");
        var toolName = ReadOption(args, "--tool");
        var targetPath = ReadOption(args, "--target-path");
        var auditParameters = ReadKeyValueOptions(args, "--audit-param");
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        var runtime = new SquadRuntime(loader, new SquadAgentFactory(), new SquadPolicyEvaluator());
        var description = runtime.Describe(
            new SquadRuntimeOptions(
                TeamRoot: teamRoot,
                Model: model,
                GitHubToken: token,
                ConstructNativeAgents: constructNativeAgents,
                YoloMode: yoloMode,
                ToolPolicyPath: toolPolicyPath,
                ToolName: toolName,
                TargetPath: targetPath,
                AuditParameters: auditParameters),
            requestedAgentIds.Count == 0 ? null : requestedAgentIds);

        Render(description);

        foreach (var agent in description.Agents)
        {
            await agent.DisposeAsync();
        }

        return 0;
    }

    private static void Render(SquadRuntimeDescription description)
    {
        AnsiConsole.Write(new Rule("[bold cyan1]Real .squad → Copilot-backed Microsoft Agent Framework adapter[/]"));
        AnsiConsole.MarkupLine($"[bold]Team root:[/] {Markup.Escape(description.TeamRoot)}");
        AnsiConsole.MarkupLine($"[bold]Squad root:[/] {Markup.Escape(description.SquadRoot)}");
        AnsiConsole.MarkupLine($"[bold]Tool policy:[/] {Markup.Escape(description.ToolPolicyPath)}");
        AnsiConsole.MarkupLine($"[bold]YOLO mode:[/] {Markup.Escape(description.YoloMode.ToString())}");
        AnsiConsole.MarkupLine($"[bold]Agents loaded:[/] {description.Agents.Count}");
        AnsiConsole.MarkupLine($"[bold]Native MAF agents constructed:[/] {description.ConstructedNativeAgentCount}");
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Id")
            .AddColumn("Name")
            .AddColumn("Role")
            .AddColumn("Model")
            .AddColumn("Adapter status");

        foreach (var agent in description.Agents.OrderBy(agent => agent.Definition.Id, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(
                Markup.Escape(agent.Definition.Id),
                Markup.Escape(agent.Definition.Name),
                Markup.Escape(agent.Definition.Role),
                Markup.Escape(agent.Model),
                Markup.Escape(agent.NativeStatus));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (description.ToolDecisions.Count > 0)
        {
            var decisions = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Agent")
                .AddColumn("Tier")
                .AddColumn("Tool")
                .AddColumn("Target")
                .AddColumn("Verdict")
                .AddColumn("Reason");

            foreach (var decision in description.ToolDecisions.OrderBy(decision => decision.AgentId, StringComparer.OrdinalIgnoreCase))
            {
                decisions.AddRow(
                    Markup.Escape(decision.AgentId),
                    decision.AgentTier.ToString(),
                    Markup.Escape(decision.Tool),
                    Markup.Escape(decision.TargetPath ?? "(none)"),
                    Markup.Escape(decision.Verdict),
                    Markup.Escape(decision.Reason));
            }

            AnsiConsole.Write(new Rule("[bold cyan1]Deterministic tool policy smoke decision[/]"));
            AnsiConsole.Write(decisions);

            var auditParameters = description.ToolDecisions
                .SelectMany(decision => decision.AuditParameters)
                .DistinctBy(parameter => parameter.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (auditParameters.Length > 0)
            {
                AnsiConsole.MarkupLine("[bold]Redacted audit parameters:[/]");
                foreach (var (key, value) in auditParameters)
                {
                    AnsiConsole.MarkupLine($"  - {Markup.Escape(key)}={Markup.Escape(value)}");
                }
            }

            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine("[bold]Runtime prerequisites:[/]");
        foreach (var prerequisite in description.RuntimePrerequisites)
        {
            AnsiConsole.MarkupLine($"  - {Markup.Escape(prerequisite)}");
        }
    }

    private static string? ReadOption(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static IReadOnlySet<string> ReadRepeatedOption(IReadOnlyList<string> args, string name)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                values.Add(args[i + 1]);
            }
        }

        return values;
    }

    private static SquadYoloMode ReadYoloMode(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i].StartsWith("--yolo=", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("--yolo is a safe flag only and does not accept a value.");
            }

            if (!string.Equals(args[i], "--yolo", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("--yolo is a safe flag only and does not accept a value.");
            }

            return SquadYoloMode.PromptlessWithinPolicy;
        }

        return SquadYoloMode.Disabled;
    }

    private static IReadOnlyDictionary<string, string> ReadKeyValueOptions(IReadOnlyList<string> args, string name)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in ReadRepeatedOption(args, name))
        {
            var split = value.Split('=', 2, StringSplitOptions.TrimEntries);
            if (split.Length != 2 || string.IsNullOrWhiteSpace(split[0]))
            {
                throw new InvalidOperationException($"{name} expects key=value.");
            }

            values[split[0]] = split[1];
        }

        return values;
    }

    private static void RejectUnsafePolicyBypassArgs(IReadOnlyList<string> args)
    {
        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bypass",
            "skipreview",
            "skipapproval",
            "overridepolicy",
            "disableaudit",
            "reduceapproval",
            "removegate",
            "unrestricted",
            "alltoolswithoutpolicy",
            "ignoreallowlist"
        };

        foreach (var arg in args)
        {
            var token = arg.TrimStart('-');
            foreach (var part in token.Split(['=', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var normalized = new string(part.Where(char.IsLetterOrDigit).ToArray());
                if (forbidden.Contains(normalized))
                {
                    throw new InvalidOperationException($"Unsafe policy bypass configuration is rejected: {Markup.Escape(arg)}");
                }
            }
        }
    }
}
