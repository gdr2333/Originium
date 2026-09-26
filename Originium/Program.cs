using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using Originium.Components;
using Originium.Datas;
using Originium.Services;
using Originium.Tools;
using System.Security.Claims;
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
builder.Services.AddSingleton<AdminAuthenticator>();
builder.Services.AddLogging();
builder.Services.AddDbContextFactory<MemoryDb>(o => o.UseSqlServer(config.MemoryDb));

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Originium.Admin";
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "Originium",
            Version = "1.0.0",
            Title = "Originium Memory Service",
            Description = "基于 MCP 的 AI 长期记忆服务"
        };
    })
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
    })
    .WithTools<MemoryTool>()
    .WithPrompts<MemoryPromptType>()
    .WithResources<MemoryResourceType>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapPost("/admin/login", async (HttpContext http, AdminAuthenticator auth, IAntiforgery antiforgery) =>
{
    if (!await IsAntiforgeryValid(http, antiforgery))
    {
        return Results.BadRequest();
    }

    var form = await http.Request.ReadFormAsync();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    if (!auth.Verify(password))
    {
        return Results.Redirect("/login?error=1");
    }

    var identity = new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "admin")],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity));

    return Results.Redirect(IsLocalUrl(returnUrl) ? returnUrl : "/memories");
});

app.MapPost("/admin/logout", async (HttpContext http, IAntiforgery antiforgery) =>
{
    if (!await IsAntiforgeryValid(http, antiforgery))
    {
        return Results.BadRequest();
    }

    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

static bool IsLocalUrl(string? url)
    => !string.IsNullOrWhiteSpace(url)
        && url.StartsWith('/')
        && !url.StartsWith("//")
        && !url.StartsWith("/\\");

static async Task<bool> IsAntiforgeryValid(HttpContext http, IAntiforgery antiforgery)
{
    try
    {
        await antiforgery.ValidateRequestAsync(http);
        return true;
    }
    catch (AntiforgeryValidationException)
    {
        return false;
    }
}

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
            if (config.OpenAIEmbeddingDimension is < 1 or > 1998)
            {
                dbLogger.LogError("OpenAIEmbeddingDimension {Dimension} 超出 SQL Server vector 支持范围 (1-1998)",
                    config.OpenAIEmbeddingDimension);
                throw new InvalidOperationException("OpenAIEmbeddingDimension 配置非法");
            }

            var pending = sqlsvr!.Database.GetPendingMigrations().ToList();
            if (args.Contains("--cleardb"))
            {
                dbLogger.LogWarning("收到 --cleardb 参数，正在删除数据库...");
                sqlsvr.Database.EnsureDeleted();
                pending.Clear();
                dbLogger.LogInformation("数据库删除完成，将由迁移机制重建");
            }

            await sqlsvr.Database.MigrateAsync();
            dbLogger.LogInformation("数据库迁移已应用，共 {Count} 个迁移: {Migrations}",
                pending.Count + 1, string.Join(", ", pending));

            var actualDim = await sqlsvr.Database.SqlQuery<int>($"""
                SELECT (CAST(MAX(max_length) AS int) - 8) / 4 AS [Value]
                FROM sys.columns
                WHERE object_id = OBJECT_ID(N'dbo.Memories') AND name = N'Embedding'
                """).FirstOrDefaultAsync();
            if (actualDim != config.OpenAIEmbeddingDimension)
            {
                dbLogger.LogError(
                    "嵌入向量维度不一致: 数据库列为 vector({Actual}), 配置为 {Config}。请将 Config.json 的 OpenAIEmbeddingDimension 调整为 {Actual}，或改回原维度后生成并应用新迁移 (dotnet ef migrations add)，必要时用 --cleardb 重建",
                    actualDim, config.OpenAIEmbeddingDimension, actualDim);
                throw new InvalidOperationException("Embedding 列维度与配置不一致，服务拒绝启动");
            }
            dbLogger.LogDebug("嵌入向量维度校验通过: {Dimension}", actualDim);

            if (args.Contains("--regen-embeddings"))
            {
                var egmLogger = scope.ServiceProvider.GetRequiredService<ILogger<EmbeddingsGeneratorManager>>();
                var egm = scope.ServiceProvider.GetService<EmbeddingsGeneratorManager>();
                egmLogger.LogWarning("请求重新生成嵌入向量，正在处理......");

                var memories = await sqlsvr.Memories.ToListAsync();
                egmLogger.LogInformation("需要重新生成嵌入向量的记忆数量: {Count}", memories.Count);

                sqlsvr.Database.ExecuteSql($"ALTER TABLE Memories ALTER COLUMN Embedding vector({config.OpenAIEmbeddingDimension}) NULL");

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
