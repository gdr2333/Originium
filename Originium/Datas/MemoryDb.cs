using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Originium.Datas;

namespace Originium.Datas;

public class MemoryDb(DbContextOptions<MemoryDb> opt, Config config, ILogger<MemoryDb> logger) : DbContext(opt)
{
    public DbSet<MemoryItem> Memories { get; set; }
    private readonly ILogger<MemoryDb> _logger = logger;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        _logger.LogDebug("配置 MemoryItem 实体，向量维度: {Dimensions}", config.OpenAIEmbeddingDimension);
        modelBuilder.Entity<MemoryItem>()
            .Property(mi => mi.Embedding)
            .HasColumnType($"vector({config.OpenAIEmbeddingDimension})");
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        _logger.LogDebug("保存数据库更改");
        var result = base.SaveChanges();
        _logger.LogInformation("数据库更改保存完成，影响行数: {Rows}", result);
        return result;
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("异步保存数据库更改");
        var task = base.SaveChangesAsync(cancellationToken);
        return task.ContinueWith(t =>
        {
            if (!t.IsCanceled && !t.IsFaulted)
            {
                _logger.LogInformation("异步数据库更改保存完成，影响行数: {Rows}", t.Result);
            }
            return t.Result;
        }, cancellationToken);
    }
}
