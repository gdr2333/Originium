using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Originium.Datas;
using System.ComponentModel;

namespace Originium.Tools;

[McpServerResourceType]
public class MemoryResourceType(MemoryDb db)
{
    [McpServerResource(UriTemplate = "memory://recent", Name = "Recent Memories")]
    [Description("最近写入的记忆列表")]
    public async Task<string> Recent()
    {
        var items = await db.Memories
            .OrderByDescending(m => m.Id)
            .Take(20)
            .Select(m => $"[{m.Id}] {m.Content}")
            .ToArrayAsync();
        return items.Length == 0 ? "（暂无记忆）" : string.Join("\n", items);
    }

    [McpServerResource(UriTemplate = "memory://item/{id}", Name = "Memory Item")]
    [Description("按 ID 读取一条记忆")]
    public async Task<string> Item([Description("记忆 ID")] uint id)
    {
        var m = await db.Memories.FindAsync(id);
        return m is null ? $"记忆 {id} 不存在" : $"[{m.Id}] {m.Content}";
    }
}
