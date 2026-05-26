using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using System.Security.Cryptography;
using System.Text;

namespace SquadInABox.RealSquad;

public static class CopilotSessionTraceMapper
{
    public static bool ShouldEmitSessionEvent(SessionEvent sessionEvent)
    {
        ArgumentNullException.ThrowIfNull(sessionEvent);

        return sessionEvent switch
        {
            AssistantMessageDeltaEvent => false,
            AssistantReasoningDeltaEvent => false,
            ToolExecutionProgressEvent => false,
            ToolExecutionPartialResultEvent => false,
            _ => true
        };
    }

    public static CopilotSessionTraceEvent FromSessionEvent(
        string rootAgentId,
        SessionEvent sessionEvent,
        bool includeRawContent = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootAgentId);
        ArgumentNullException.ThrowIfNull(sessionEvent);

        return sessionEvent switch
        {
            SubagentStartedEvent started => new CopilotSessionTraceEvent(
                EventType: started.Type,
                RootAgentId: rootAgentId,
                SdkAgentId: started.AgentId,
                SubagentName: started.Data.AgentName,
                SubagentDisplayName: started.Data.AgentDisplayName,
                Model: started.Data.Model,
                ToolName: null,
                ToolCallId: started.Data.ToolCallId,
                DurationMs: null,
                TotalTokens: null,
                TotalToolCalls: null,
                ContentLength: null,
                ContentSha256: null,
                Status: "started",
                ErrorMessage: null,
                Tools: null,
                RawSubagentDescription: Raw(started.Data.AgentDescription, includeRawContent),
                RawToolArguments: null,
                RawToolResult: null,
                RawAssistantContent: null),
            SubagentCompletedEvent completed => new CopilotSessionTraceEvent(
                EventType: completed.Type,
                RootAgentId: rootAgentId,
                SdkAgentId: completed.AgentId,
                SubagentName: completed.Data.AgentName,
                SubagentDisplayName: completed.Data.AgentDisplayName,
                Model: completed.Data.Model,
                ToolName: null,
                ToolCallId: completed.Data.ToolCallId,
                DurationMs: completed.Data.DurationMs,
                TotalTokens: completed.Data.TotalTokens,
                TotalToolCalls: completed.Data.TotalToolCalls,
                ContentLength: null,
                ContentSha256: null,
                Status: "completed",
                ErrorMessage: null,
                Tools: null,
                RawSubagentDescription: null,
                RawToolArguments: null,
                RawToolResult: null,
                RawAssistantContent: null),
            SubagentFailedEvent failed => new CopilotSessionTraceEvent(
                EventType: failed.Type,
                RootAgentId: rootAgentId,
                SdkAgentId: failed.AgentId,
                SubagentName: failed.Data.AgentName,
                SubagentDisplayName: failed.Data.AgentDisplayName,
                Model: failed.Data.Model,
                ToolName: null,
                ToolCallId: failed.Data.ToolCallId,
                DurationMs: failed.Data.DurationMs,
                TotalTokens: failed.Data.TotalTokens,
                TotalToolCalls: failed.Data.TotalToolCalls,
                ContentLength: null,
                ContentSha256: null,
                Status: "failed",
                ErrorMessage: failed.Data.Error,
                Tools: null,
                RawSubagentDescription: null,
                RawToolArguments: null,
                RawToolResult: null,
                RawAssistantContent: null),
            SubagentSelectedEvent selected => new CopilotSessionTraceEvent(
                EventType: selected.Type,
                RootAgentId: rootAgentId,
                SdkAgentId: selected.AgentId,
                SubagentName: selected.Data.AgentName,
                SubagentDisplayName: selected.Data.AgentDisplayName,
                Model: null,
                ToolName: null,
                ToolCallId: null,
                DurationMs: null,
                TotalTokens: null,
                TotalToolCalls: null,
                ContentLength: null,
                ContentSha256: null,
                Status: "selected",
                ErrorMessage: null,
                Tools: selected.Data.Tools,
                RawSubagentDescription: null,
                RawToolArguments: null,
                RawToolResult: null,
                RawAssistantContent: null),
            SubagentDeselectedEvent deselected => new CopilotSessionTraceEvent(
                EventType: deselected.Type,
                RootAgentId: rootAgentId,
                SdkAgentId: deselected.AgentId,
                SubagentName: null,
                SubagentDisplayName: null,
                Model: null,
                ToolName: null,
                ToolCallId: null,
                DurationMs: null,
                TotalTokens: null,
                TotalToolCalls: null,
                ContentLength: null,
                ContentSha256: null,
                Status: "deselected",
                ErrorMessage: null,
                Tools: null,
                RawSubagentDescription: null,
                RawToolArguments: null,
                RawToolResult: null,
                RawAssistantContent: null),
            ToolExecutionStartEvent toolStart => new CopilotSessionTraceEvent(
                EventType: toolStart.Type,
                RootAgentId: rootAgentId,
                SdkAgentId: toolStart.AgentId,
                SubagentName: null,
                SubagentDisplayName: null,
                Model: null,
                ToolName: toolStart.Data.McpToolName ?? toolStart.Data.ToolName,
                ToolCallId: toolStart.Data.ToolCallId,
                DurationMs: null,
                TotalTokens: null,
                TotalToolCalls: null,
                ContentLength: null,
                ContentSha256: null,
                Status: "started",
                ErrorMessage: null,
                Tools: null,
                RawSubagentDescription: null,
                RawToolArguments: Raw(toolStart.Data.Arguments, includeRawContent),
                RawToolResult: null,
                RawAssistantContent: null),
            ToolExecutionCompleteEvent toolComplete => new CopilotSessionTraceEvent(
                EventType: toolComplete.Type,
                RootAgentId: rootAgentId,
                SdkAgentId: toolComplete.AgentId,
                SubagentName: null,
                SubagentDisplayName: null,
                Model: toolComplete.Data.Model,
                ToolName: null,
                ToolCallId: toolComplete.Data.ToolCallId,
                DurationMs: null,
                TotalTokens: null,
                TotalToolCalls: null,
                ContentLength: null,
                ContentSha256: null,
                Status: toolComplete.Data.Success ? "completed" : "failed",
                ErrorMessage: toolComplete.Data.Error?.ToString(),
                Tools: null,
                RawSubagentDescription: null,
                RawToolArguments: null,
                RawToolResult: Raw(toolComplete.Data.Result, includeRawContent),
                RawAssistantContent: null),
            AssistantMessageEvent assistant => CreateAssistantEvent(rootAgentId, assistant, includeRawContent),
            AssistantMessageDeltaEvent assistantDelta => CreateAssistantDeltaEvent(rootAgentId, assistantDelta, includeRawContent),
            _ => new CopilotSessionTraceEvent(
                EventType: sessionEvent.Type,
                RootAgentId: rootAgentId,
                SdkAgentId: sessionEvent.AgentId,
                SubagentName: null,
                SubagentDisplayName: null,
                Model: null,
                ToolName: null,
                ToolCallId: null,
                DurationMs: null,
                TotalTokens: null,
                TotalToolCalls: null,
                ContentLength: null,
                ContentSha256: null,
                Status: null,
                ErrorMessage: null,
                Tools: null,
                RawSubagentDescription: null,
                RawToolArguments: null,
                RawToolResult: null,
                RawAssistantContent: null)
        };
    }

    public static CopilotSessionTraceEvent FromAgentResponseUpdate(
        string rootAgentId,
        AgentResponseUpdate update,
        bool includeRawContent = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootAgentId);
        ArgumentNullException.ThrowIfNull(update);

        var content = update.Text;
        return new CopilotSessionTraceEvent(
            EventType: "agent.response.update",
            RootAgentId: rootAgentId,
            SdkAgentId: update.AgentId,
            SubagentName: update.AuthorName,
            SubagentDisplayName: update.AuthorName,
            Model: null,
            ToolName: null,
            ToolCallId: null,
            DurationMs: null,
            TotalTokens: null,
            TotalToolCalls: null,
            ContentLength: string.IsNullOrEmpty(content) ? null : content.Length,
            ContentSha256: Hash(content),
            Status: update.FinishReason?.ToString(),
            ErrorMessage: null,
            Tools: null,
            RawSubagentDescription: null,
            RawToolArguments: null,
            RawToolResult: null,
            RawAssistantContent: Raw(content, includeRawContent));
    }

    public static CopilotSessionTraceEvent FromAgentResponseSummary(
        string rootAgentId,
        string? sdkAgentId,
        string? authorName,
        string content,
        int updateCount,
        string? finishReason,
        bool includeRawContent = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootAgentId);
        ArgumentNullException.ThrowIfNull(content);

        return new CopilotSessionTraceEvent(
            EventType: "agent.response.summary",
            RootAgentId: rootAgentId,
            SdkAgentId: sdkAgentId,
            SubagentName: authorName,
            SubagentDisplayName: authorName,
            Model: null,
            ToolName: null,
            ToolCallId: null,
            DurationMs: null,
            TotalTokens: null,
            TotalToolCalls: null,
            ContentLength: content.Length,
            ContentSha256: Hash(content),
            Status: string.IsNullOrWhiteSpace(finishReason) ? "completed" : finishReason,
            ErrorMessage: null,
            Tools: null,
            RawSubagentDescription: null,
            RawToolArguments: null,
            RawToolResult: null,
            RawAssistantContent: Raw(content, includeRawContent),
            ResponseUpdateCount: updateCount);
    }

    public static CopilotSessionTraceEvent FromUserPrompt(
        string rootAgentId,
        string prompt,
        bool includeRawContent = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootAgentId);
        ArgumentNullException.ThrowIfNull(prompt);

        return new CopilotSessionTraceEvent(
            EventType: "session.user_prompt",
            RootAgentId: rootAgentId,
            SdkAgentId: rootAgentId,
            SubagentName: null,
            SubagentDisplayName: null,
            Model: null,
            ToolName: null,
            ToolCallId: null,
            DurationMs: null,
            TotalTokens: null,
            TotalToolCalls: null,
            ContentLength: prompt.Length,
            ContentSha256: Hash(prompt),
            Status: "submitted",
            ErrorMessage: null,
            Tools: null,
            RawSubagentDescription: null,
            RawToolArguments: null,
            RawToolResult: null,
            RawAssistantContent: null,
            RawUserPrompt: Raw(prompt, includeRawContent));
    }

    public static CopilotSessionTraceEvent FromSubagentPrompt(
        string rootAgentId,
        SquadAgentDefinition subagent,
        string prompt,
        bool includeRawContent = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootAgentId);
        ArgumentNullException.ThrowIfNull(subagent);
        ArgumentNullException.ThrowIfNull(prompt);

        return new CopilotSessionTraceEvent(
            EventType: "subagent.activation.prompt",
            RootAgentId: rootAgentId,
            SdkAgentId: subagent.Id,
            SubagentName: subagent.Id,
            SubagentDisplayName: subagent.Name,
            Model: null,
            ToolName: null,
            ToolCallId: null,
            DurationMs: null,
            TotalTokens: null,
            TotalToolCalls: null,
            ContentLength: prompt.Length,
            ContentSha256: Hash(prompt),
            Status: "submitted",
            ErrorMessage: null,
            Tools: null,
            RawSubagentDescription: Raw(subagent.Description, includeRawContent),
            RawToolArguments: null,
            RawToolResult: null,
            RawAssistantContent: null,
            RawUserPrompt: Raw(prompt, includeRawContent));
    }

    private static CopilotSessionTraceEvent CreateAssistantEvent(
        string rootAgentId,
        AssistantMessageEvent assistant,
        bool includeRawContent)
    {
        var content = assistant.Data.Content;
        return new CopilotSessionTraceEvent(
            EventType: assistant.Type,
            RootAgentId: rootAgentId,
            SdkAgentId: assistant.AgentId,
            SubagentName: null,
            SubagentDisplayName: null,
            Model: null,
            ToolName: null,
            ToolCallId: null,
            DurationMs: null,
            TotalTokens: assistant.Data.OutputTokens,
            TotalToolCalls: assistant.Data.ToolRequests?.Length,
            ContentLength: content?.Length,
            ContentSha256: Hash(content),
            Status: assistant.Data.Phase,
            ErrorMessage: null,
            Tools: null,
            RawSubagentDescription: null,
            RawToolArguments: null,
            RawToolResult: null,
            RawAssistantContent: Raw(content, includeRawContent));
    }

    private static CopilotSessionTraceEvent CreateAssistantDeltaEvent(
        string rootAgentId,
        AssistantMessageDeltaEvent assistantDelta,
        bool includeRawContent)
    {
        var content = assistantDelta.Data.DeltaContent;
        return new CopilotSessionTraceEvent(
            EventType: assistantDelta.Type,
            RootAgentId: rootAgentId,
            SdkAgentId: assistantDelta.AgentId,
            SubagentName: null,
            SubagentDisplayName: null,
            Model: null,
            ToolName: null,
            ToolCallId: null,
            DurationMs: null,
            TotalTokens: null,
            TotalToolCalls: null,
            ContentLength: content?.Length,
            ContentSha256: Hash(content),
            Status: null,
            ErrorMessage: null,
            Tools: null,
            RawSubagentDescription: null,
            RawToolArguments: null,
            RawToolResult: null,
            RawAssistantContent: Raw(content, includeRawContent));
    }

    private static string? Raw(object? value, bool includeRawContent)
    {
        if (!includeRawContent || value is null)
        {
            return null;
        }

        return value.ToString();
    }

    private static string? Hash(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
