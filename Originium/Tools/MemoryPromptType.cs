using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Originium.Tools;

[McpServerPromptType]
public class MemoryPromptType
{
    [McpServerPrompt(Name = "auto_remember"), Description("进入自动记忆模式：指示 AI 在对话中主动识别并持久化关键信息")]
    public static string AutoRemember() =>
        "在本次对话中，请主动识别用户陈述的事实、偏好、决策与任务背景，" +
        "并调用 WriteMemory 将其持久化；回答前先用 SearchMemory 检索是否已有相关记忆。";

    [McpServerPrompt(Name = "recall_first"), Description("先回忆再回答：按主题检索记忆后作答")]
    public static IEnumerable<ChatMessage> RecallFirst(
        [Description("想回忆的主题/关键词")] string? topic = null)
    {
        var q = string.IsNullOrWhiteSpace(topic) ? "用户当前问题" : topic;
        return [
            new ChatMessage(ChatRole.User, $"请先用 SearchMemory 检索「{q}」相关记忆，再结合检索结果回答用户。"),
            new ChatMessage(ChatRole.Assistant, "好的，我会先检索长期记忆再作答。")
        ];
    }
}
