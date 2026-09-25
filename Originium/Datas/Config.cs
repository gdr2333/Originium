namespace Originium.Datas;

public class Config
{
    public string MemoryDb { get; init; }
    public Uri OpenAIEmbeddingEndpoint { get; init; }
    public string? OpenAIApiKey { get; init; }
    public string OpenAIEmbeddingModel { get; init; }
    public int OpenAIEmbeddingDimension { get; init; }
    public int OpenAIEmbeddingConcurrent { get; init; }
    public bool OpenAIEmbeddingIsFixedDimension { get; init; }
}
