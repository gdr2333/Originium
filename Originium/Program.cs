using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Originium.Datas;
using Originium.Services;
using Originium.Tools;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
});
builder.Logging.SetMinimumLevel(LogLevel.Debug);

var config = JsonSerializer.Deserialize<Config>(File.ReadAllText("Config.json"))!;
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<EmbeddingsGeneratorManager>();
builder.Services.AddLogging();
builder.Services.AddSqlServer<MemoryDb>(config.MemoryDb);

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
    })
    .WithTools<MemoryTool>();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("Originium 服务启动中...");
    logger.LogInformation("配置文件已加载: 数据库={Database}, 嵌入模型={Model}, 维度={Dimensions}, 并发数={Concurrent}",
        config.MemoryDb, config.OpenAIEmbeddingModel, config.OpenAIEmbeddingDimension, config.OpenAIEmbeddingConcurrent);
}
catch (Exception ex)
{
    logger.LogError(ex, "加载配置文件时发生错误");
    throw;
}

try
{
    logger.LogInformation("MCP 服务正在初始化...");

    using (var scope = app.Services.CreateScope())
    {
        using var sqlsvr = scope.ServiceProvider.GetService<MemoryDb>();
        var dbLogger = scope.ServiceProvider.GetRequiredService<ILogger<MemoryDb>>();

        try
        {
            if (args.Contains("--cleardb"))
            {
                dbLogger.LogWarning("收到 --cleardb 参数，正在删除数据库...");
                sqlsvr!.Database.EnsureDeleted();
                dbLogger.LogInformation("数据库删除完成");
            }

            var isnew = sqlsvr!.Database.EnsureCreated();
            if (isnew)
            {
                dbLogger.LogInformation("数据库已创建或已存在");
            }
            else
            {
                dbLogger.LogDebug("数据库已存在，无需创建");
            }

            if (args.Contains("--regen-embeddings"))
            {
                var egmLogger = scope.ServiceProvider.GetRequiredService<ILogger<EmbeddingsGeneratorManager>>();
                var egm = scope.ServiceProvider.GetService<EmbeddingsGeneratorManager>();
                egmLogger.LogWarning("请求重新生成嵌入向量，正在处理......");

                var memories = await sqlsvr.Memories.ToListAsync();
                egmLogger.LogInformation("需要重新生成嵌入向量的记忆数量: {Count}", memories.Count);

                sqlsvr.Database.ExecuteSql($"ALTER TABLE MemoryItem ALTER COLUMN Embedding vector({config.OpenAIEmbeddingDimension}) NULL");

                List<Task> tsks = [];
                var processed = 0;
                foreach (var i in memories)
                {
                    var tsk = egm!.GetEmbedding(i.Content).ContinueWith(async e =>
                    {
                        i.Embedding = new(await e);
                        processed++;
                        if (processed % 10 == 0)
                        {
                            egmLogger.LogInformation("嵌入向量生成进度: {Processed}/{Total}", processed, memories.Count);
                        }
                    });
                    tsks.Add(tsk);
                }

                await Task.WhenAll(tsks);
                await sqlsvr.SaveChangesAsync();
                egmLogger.LogInformation("嵌入向量重新生成完成，共处理 {Count} 条记忆", memories.Count);
            }
        }
        catch (Exception ex)
        {
            dbLogger.LogError(ex, "数据库初始化过程中发生错误");
            throw;
        }
    }

    logger.LogInformation("MCP 服务启动完成，开始监听请求...");
    app.MapMcp();
    app.Run();
}
catch (Exception ex)
{
    logger.LogCritical(ex, "服务启动过程中发生致命错误，服务终止");
    throw;
}
