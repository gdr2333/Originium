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
    [Description("将一条信息写入长期记忆。当用户陈述重要事实、个人偏好、决策结论、任务背景或任何值得跨会话保留的信息时调用此工具，避免后续重复询问。每次调用必须传入一条语义完整的信息单元（一个事实/偏好/决策/背景），禁止将同一条信息按字节长度拆成多次调用；若有多条独立信息，请分别调用本工具或使用 WriteMemories 批量提交。")]
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
    [Description("批量写入多条语义完整的长期记忆。每个元素必须是一条语义完整的信息单元（一个事实/偏好/决策/背景），禁止将同一条信息按字节长度拆成多个元素。用于一次性提交多条独立记忆，避免多次调用 WriteMemory 造成语义割裂；任一嵌入生成失败则整体不写入。")]
    public async Task<WriteMemoriesResult> WriteMemories(
        [Description("待写入的记忆内容数组，每个元素为一条语义完整的记忆")] string[] contents
        )
    {
        try
        {
            if (contents is null || contents.Length == 0)
            {
                logger.LogWarning("批量写入记忆被调用但 contents 为空");
                return new WriteMemoriesResult(0);
            }

            logger.LogDebug("开始批量写入记忆，条数: {Count}", contents.Length);

            var embeddingTasks = new Task<float[]>[contents.Length];
            for (int i = 0; i < contents.Length; i++)
            {
                embeddingTasks[i] = egm.GetEmbedding(contents[i]);
            }
            var embeddings = await Task.WhenAll(embeddingTasks);

            var items = new MemoryItem[contents.Length];
            for (int i = 0; i < contents.Length; i++)
            {
                items[i] = new() { Content = contents[i], Embedding = new(embeddings[i]) };
            }

            await db.Memories.AddRangeAsync(items);
            await db.SaveChangesAsync();
            logger.LogInformation("批量记忆写入成功: 条数={Count}", items.Length);
            return new WriteMemoriesResult((uint)items.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "批量写入记忆时发生错误");
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

            var condition = FullTextQuery.BuildContainsExpression(keywords);
            var results = await db.Memories
                .Where(m => EF.Functions.Contains(m.Content, condition))
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

public record WriteMemoriesResult(uint Count);
