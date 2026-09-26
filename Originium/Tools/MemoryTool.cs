using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Originium.Datas;
using Originium.Services;
using System.ComponentModel;

namespace Originium.Tools;

public class MemoryTool(MemoryDb db, EmbeddingsGeneratorManager egm, ILogger<MemoryTool> logger)
{
    [McpServerTool]
    [Description("将一条信息写入长期记忆。当用户陈述重要事实、个人偏好、决策结论、任务背景或任何值得跨会话保留的信息时调用此工具，避免后续重复询问。")]
    public async Task WriteMemory(
        [Description("记忆内容")] string content
        )
    {
        try
        {
            logger.LogDebug("开始写入记忆，内容长度: {Length} 字符", content.Length);
            var embedding = await egm.GetEmbedding(content);
            logger.LogDebug("嵌入向量生成完成，维度: {Dimensions}", embedding.Length);

            await db.Memories.AddAsync(new() { Content = content, Embedding = new(embedding) });
            await db.SaveChangesAsync();
            logger.LogInformation("记忆写入成功: 内容长度={Length} 字符", content.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "写入记忆时发生错误");
            throw;
        }
    }

    [McpServerTool]
    [Description("从长期记忆中按语义相似度检索相关内容。在回答用户问题前调用，查找是否有可复用的历史记忆；返回最相关的若干条结果。")]
    public async Task<SearchResult[]> SearchMemory(
        [Description("要查询的内容")] string content
        )
    {
        try
        {
            logger.LogDebug("开始搜索记忆，查询内容: {Content}", content);
            SqlVector<float> vec = new(await egm.GetEmbedding(content));
            logger.LogDebug("查询嵌入向量生成完成");

            var results = await db.Memories
                .OrderBy(m => EF.Functions.VectorDistance("cosine", m.Embedding, vec))
                .Take(100)
                .Select(m => new SearchResult(m.Id, m.Content))
                .ToArrayAsync();

            logger.LogInformation("记忆搜索完成: 查询内容={Content}, 结果数量={Count}", content, results.Length);
            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "搜索记忆时发生错误");
            throw;
        }
    }

    [McpServerTool]
    [Description("按关键词从长期记忆中检索内容（SQL Server 全文检索路径，无需生成向量）。适合用具体词语、短语快速查找相关记忆；返回命中的若干条结果。")]
    public async Task<SearchResult[]> SearchKeywords(
        [Description("查询关键词，可包含多个词语")] string keywords,
        [Description("返回结果数量上限，默认 10")] int limit = 10
        )
    {
        try
        {
            logger.LogDebug("开始关键词搜索记忆: {Keywords}, 上限: {Limit}", keywords, limit);
            if (limit is < 1 or > 100) limit = 10;

            var results = await db.Memories
                .Where(m => EF.Functions.FreeText(m.Content, keywords))
                .OrderByDescending(m => m.Id)
                .Take(limit)
                .Select(m => new SearchResult(m.Id, m.Content))
                .ToArrayAsync();

            logger.LogInformation("关键词搜索完成: 关键词={Keywords}, 结果数量={Count}", keywords, results.Length);
            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "关键词搜索时发生错误");
            throw;
        }
    }

    [McpServerTool]
    [Description("按 ID 删除一条长期记忆。仅当用户明确要求遗忘或纠正某条过时/错误记忆时调用。")]
    public async Task DeleteMemory(
        [Description("要删除的记忆的id")] ulong id
        )
    {
        try
        {
            logger.LogDebug("开始删除记忆，ID: {Id}", id);
            var item = await db.Memories.FindAsync((uint)id);
            if (item != null)
            {
                db.Memories.Remove(item);
                await db.SaveChangesAsync();
                logger.LogInformation("记忆删除成功: ID={Id}", id);
            }
            else
            {
                logger.LogWarning("尝试删除记忆但ID不存在: {Id}", id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "删除记忆时发生错误，ID: {Id}", id);
            throw;
        }
    }
}

public record SearchResult(uint Id, string Content);
