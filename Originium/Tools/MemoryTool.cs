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
    [Description("向记忆数据库写入一条记忆")]
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
    [Description("从记忆中查询")]
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
    [Description("删除指定记忆")]
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
