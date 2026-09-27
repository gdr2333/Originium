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
        "并调用 WriteMemory 将其持久化；回答前先用 SearchMemory 检索是否已有相关记忆。\n" +
        "【硬性规则】每条记忆必须是一个语义完整的信息单元（一个事实、一个偏好、一项决策、一个背景）。" +
        "严禁按行数、字节数、字符数等任何数量指标切割信息——即使只有一行，若包含多个独立语义单元，也必须拆分为多条记忆。" +
        "判断标准：将这条记忆单独拿出来，是否能让一个不看上下文的人理解其完整含义？如果不能，请继续拆分。";

    [McpServerPrompt(Name = "recall_first"), Description("先回忆再回答：按主题检索记忆后作答")]
    public static IEnumerable<ChatMessage> RecallFirst(
        [Description("想回忆的主题/关键词")] string? topic = null)
    {
        var q = string.IsNullOrWhiteSpace(topic) ? "用户当前问题" : topic;
        return [
            new ChatMessage(ChatRole.User, $"请先用 SearchMemory 检索「{q}」相关记忆，再结合检索结果回答用户。" +
                "注意：检索返回的是语义完整的记忆单元，请直接使用，不要按行数重新拆分或合并。"),
            new ChatMessage(ChatRole.Assistant, "好的，我会先检索长期记忆再作答。")
        ];
    }
}
