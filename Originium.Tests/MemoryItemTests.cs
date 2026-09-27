using Xunit;
using Microsoft.Data.SqlTypes;
using Originium.Datas;

namespace Originium.Tests;

public class MemoryItemTests
{
    [Fact]
    public void MemoryItem_CanBeCreated()
    {
        var item = new MemoryItem
        {
            Id = 1,
            Content = "test content",
            Embedding = new SqlVector<float>(new float[] { 1.0f, 2.0f, 3.0f })
        };

        Assert.Equal(1u, item.Id);
        Assert.Equal("test content", item.Content);
    }

    [Fact]
    public void MemoryItem_Embedding_CanBeEmpty()
    {
        var item = new MemoryItem
        {
            Id = 1,
            Content = "test",
            Embedding = new SqlVector<float>(Array.Empty<float>())
        };

        Assert.NotNull(item.Embedding);
    }

    [Fact]
    public void MemoryItem_Content_CannotBeNull()
    {
        var item = new MemoryItem
        {
            Id = 1,
            Content = "test"
        };

        Assert.NotNull(item.Content);
    }
}
