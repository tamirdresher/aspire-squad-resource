using GitHub.Copilot.SDK;
using System.Security.Cryptography;
using System.Text;

namespace SquadInABox.RealSquad;

public static class CopilotSessionTraceMapper
{
    public static CopilotSessionTraceEvent FromSessionEvent(string rootAgentId, SessionEvent sessionEvent)
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
                Tools: null),
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
                Tools: null),
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
                Tools: null),
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
                Tools: selected.Data.Tools),
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
                Tools: null),
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
                Tools: null),
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
                Tools: null),
            AssistantMessageEvent assistant => CreateAssistantEvent(rootAgentId, assistant),
            AssistantMessageDeltaEvent assistantDelta => CreateAssistantDeltaEvent(rootAgentId, assistantDelta),
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
                Tools: null)
        };
    }

    private static CopilotSessionTraceEvent CreateAssistantEvent(string rootAgentId, AssistantMessageEvent assistant)
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
            Tools: null);
    }

    private static CopilotSessionTraceEvent CreateAssistantDeltaEvent(string rootAgentId, AssistantMessageDeltaEvent assistantDelta)
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
            Tools: null);
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
