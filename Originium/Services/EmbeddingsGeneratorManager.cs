using Microsoft.Extensions.Logging;
using Originium.Datas;
using System.Diagnostics;
using System.Text.Json;

namespace Originium.Services;

public class EmbeddingsGeneratorManager : IDisposable
{
    private readonly ConcurrentManager concurrentManager;
    private readonly HttpClient client;
    private readonly Config config;
    private readonly ILogger<EmbeddingsGeneratorManager> logger;

    public EmbeddingsGeneratorManager(Config config, ILogger<EmbeddingsGeneratorManager> logger)
    {
        this.config = config;
        this.logger = logger;
        this.logger.LogDebug("EmbeddingsGeneratorManager 初始化，端点={Endpoint}, 模型={Model}",
            config.OpenAIEmbeddingEndpoint, config.OpenAIEmbeddingModel);

        concurrentManager = new(config.OpenAIEmbeddingConcurrent);
        client = new() { BaseAddress = config.OpenAIEmbeddingEndpoint };
        client.DefaultRequestHeaders.Authorization = new("Bearer", config.OpenAIApiKey);
    }

    public async Task<float[]> GetEmbedding(string text)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogDebug("开始生成嵌入向量，文本长度: {Length} 字符", text.Length);

        try
        {
            var result = await concurrentManager.EnqueueAsync(async () =>
            {
                logger.LogDebug("嵌入请求排队完成，开始发送 HTTP 请求");
                HttpResponseMessage resp;
                if (config.OpenAIEmbeddingIsFixedDimension)
                {
                    resp = await client.PostAsJsonAsync("",
                        new { model = config.OpenAIEmbeddingModel, input = text, encoding_format = "float" });
                }
                else
                {
                    resp = await client.PostAsJsonAsync("",
                        new { model = config.OpenAIEmbeddingModel, input = text,
                            dimensions = config.OpenAIEmbeddingDimension, encoding_format = "float" });
                }

                if (!resp.IsSuccessStatusCode)
                {
                    logger.LogWarning("嵌入 API 请求失败，状态码: {StatusCode}", resp.StatusCode);
                    resp.EnsureSuccessStatusCode();
                }

                var respjsobj = await resp.Content.ReadFromJsonAsync<JsonElement>();
                var embeddings = respjsobj.GetProperty("data")[0].GetProperty("embedding")
                    .EnumerateArray().Select(e => e.GetSingle()).ToArray();

                stopwatch.Stop();
                logger.LogDebug("嵌入向量生成完成，维度: {Dimensions}, 耗时: {Elapsed}ms",
                    embeddings.Length, stopwatch.ElapsedMilliseconds);
                return embeddings;
            });

            return result;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("嵌入向量生成请求被取消");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "生成嵌入向量时发生错误，文本长度: {Length}", text.Length);
            throw;
        }
    }

    public void Dispose()
    {
        logger.LogDebug("EmbeddingsGeneratorManager 正在释放资源");
        concurrentManager.Dispose();
        client.Dispose();
    }
}
