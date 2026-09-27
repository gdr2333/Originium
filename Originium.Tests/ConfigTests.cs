using Xunit;
using Originium.Datas;

namespace Originium.Tests;

public class ConfigTests
{
    [Fact]
    public void Config_PropertiesCanBeSet()
    {
        var config = new Config
        {
            MemoryDb = "Server=localhost;Database=test",
            OpenAIEmbeddingEndpoint = new Uri("http://localhost:8080"),
            OpenAIApiKey = "test-key",
            OpenAIEmbeddingModel = "bge-m3",
            OpenAIEmbeddingDimension = 1024,
            OpenAIEmbeddingConcurrent = 4,
            OpenAIEmbeddingIsFixedDimension = false,
            AdminPassword = "admin123"
        };

        Assert.Equal("Server=localhost;Database=test", config.MemoryDb);
        Assert.Equal(new Uri("http://localhost:8080"), config.OpenAIEmbeddingEndpoint);
        Assert.Equal("test-key", config.OpenAIApiKey);
        Assert.Equal("bge-m3", config.OpenAIEmbeddingModel);
        Assert.Equal(1024, config.OpenAIEmbeddingDimension);
        Assert.Equal(4, config.OpenAIEmbeddingConcurrent);
        Assert.False(config.OpenAIEmbeddingIsFixedDimension);
        Assert.Equal("admin123", config.AdminPassword);
    }

    [Fact]
    public void Config_NullableProperties_CanBeNull()
    {
        var config = new Config
        {
            AdminPassword = null,
            OpenAIApiKey = null
        };

        Assert.Null(config.AdminPassword);
        Assert.Null(config.OpenAIApiKey);
    }

    [Fact]
    public void Config_EmbeddingDimension_WithinValidRange()
    {
        var config = new Config { OpenAIEmbeddingDimension = 1 };
        Assert.Equal(1, config.OpenAIEmbeddingDimension);

        config = new Config { OpenAIEmbeddingDimension = 1998 };
        Assert.Equal(1998, config.OpenAIEmbeddingDimension);
    }
}
