using System.Net;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace KyrolusSous.RateLimiting.UnitTests;

public sealed class AiPartitionKeysTests
{
    private static readonly IPAddress BaseIp = IPAddress.Parse("198.51.100.42");

    [Fact(DisplayName = "AI Model key resolves from headers and query parameters accurately")]
    public void AiModelKey_ResolvesFromHeadersAndQueryAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        KyrolusRateLimitPartitionKeys.GetAiModelKey(context).ShouldBe("aimodel_unspecified");

        context.Request.Headers["X-AI-Model"] = "gpt-4o";
        KyrolusRateLimitPartitionKeys.GetAiModelKey(context).ShouldBe("aimodel_gpt-4o");
        KyrolusRateLimitPartitionKeys.GetAiModelAndClientKey(context).ShouldBe("aimodel_gpt-4o:198.51.100.42");

        var queryContext = new DefaultHttpContext();
        queryContext.Request.QueryString = new QueryString("?model=claude/3.5-sonnet");
        KyrolusRateLimitPartitionKeys.GetAiModelKey(queryContext).ShouldBe("aimodel_claude_3.5-sonnet");
    }

    [Fact(DisplayName = "AI Stream Mode resolves streaming vs unary modes accurately")]
    public void AiStreamMode_ResolvesStreamingVsUnaryAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        KyrolusRateLimitPartitionKeys.GetAiStreamModeKey(context).ShouldBe("aistream_unary");

        context.Request.QueryString = new QueryString("?stream=true");
        KyrolusRateLimitPartitionKeys.GetAiStreamModeKey(context).ShouldBe("aistream_streaming");
        KyrolusRateLimitPartitionKeys.GetAiStreamModeAndClientKey(context).ShouldBe("aistream_streaming:198.51.100.42");

        var sseContext = new DefaultHttpContext();
        sseContext.Request.Headers.Accept = "text/event-stream";
        KyrolusRateLimitPartitionKeys.GetAiStreamModeKey(sseContext).ShouldBe("aistream_streaming");

        var headerContext = new DefaultHttpContext();
        headerContext.Request.Headers["X-AI-Stream"] = "1";
        KyrolusRateLimitPartitionKeys.GetAiStreamModeKey(headerContext).ShouldBe("aistream_streaming");
    }

    [Fact(DisplayName = "AI Token Context, Task Capability, Reasoning Effort, and Agent Recursion Depth resolve accurately")]
    public void Ai_Advanced_Keys_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Token Context Bucket
        KyrolusRateLimitPartitionKeys.GetAiTokenContextBucketKey(context).ShouldBe("ai_tokens_short");
        context.Request.Headers["X-Prompt-Tokens"] = "12000";
        KyrolusRateLimitPartitionKeys.GetAiTokenContextBucketKey(context).ShouldBe("ai_tokens_medium");
        KyrolusRateLimitPartitionKeys.GetAiTokenContextBucketAndClientKey(context).ShouldBe("ai_tokens_medium:198.51.100.42");
        context.Request.Headers["X-Prompt-Tokens"] = "80000";
        KyrolusRateLimitPartitionKeys.GetAiTokenContextBucketKey(context).ShouldBe("ai_tokens_massive");

        // Task Capability Family
        var taskContext = new DefaultHttpContext();
        taskContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetAiTaskCapabilityKey(taskContext).ShouldBe("ai_task_general");
        taskContext.Request.Path = "/v1/embeddings";
        KyrolusRateLimitPartitionKeys.GetAiTaskCapabilityKey(taskContext).ShouldBe("ai_task_embeddings");
        taskContext.Request.Headers["X-AI-Task"] = "vision-ocr";
        KyrolusRateLimitPartitionKeys.GetAiTaskCapabilityKey(taskContext).ShouldBe("ai_task_vision");
        KyrolusRateLimitPartitionKeys.GetAiTaskCapabilityAndClientKey(taskContext).ShouldBe("ai_task_vision:198.51.100.42");

        // Reasoning Effort
        var reasonContext = new DefaultHttpContext();
        reasonContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetAiReasoningEffortKey(reasonContext).ShouldBe("ai_reasoning_none");
        reasonContext.Request.Headers["X-AI-Reasoning-Effort"] = "high";
        KyrolusRateLimitPartitionKeys.GetAiReasoningEffortKey(reasonContext).ShouldBe("ai_reasoning_high");
        KyrolusRateLimitPartitionKeys.GetAiReasoningEffortAndClientKey(reasonContext).ShouldBe("ai_reasoning_high:198.51.100.42");

        // Agent Recursion Depth
        var agentContext = new DefaultHttpContext();
        agentContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetAiAgentRecursionDepthKey(agentContext).ShouldBe("ai_agent_direct");
        agentContext.Request.Headers["X-Agent-Depth"] = "3";
        KyrolusRateLimitPartitionKeys.GetAiAgentRecursionDepthKey(agentContext).ShouldBe("ai_agent_depth_3");
        KyrolusRateLimitPartitionKeys.GetAiAgentRecursionDepthAndClientKey(agentContext).ShouldBe("ai_agent_depth_3:198.51.100.42");
        agentContext.Request.Headers["X-Agent-Depth"] = "15";
        KyrolusRateLimitPartitionKeys.GetAiAgentRecursionDepthKey(agentContext, maxDepth: 10).ShouldBe("ai_agent_loop_risk");
    }
}
