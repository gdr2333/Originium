using Xunit;
using Originium.Services;

namespace Originium.Tests;

public class FullTextQueryTests
{
    [Fact]
    public void BuildContainsExpression_EmptyString_ReturnsEmpty()
    {
        var result = FullTextQuery.BuildContainsExpression(null);
        Assert.Empty(result);

        result = FullTextQuery.BuildContainsExpression(string.Empty);
        Assert.Empty(result);

        result = FullTextQuery.BuildContainsExpression("   ");
        Assert.Empty(result);
    }

    [Fact]
    public void BuildContainsExpression_SingleTerm_WrappedInQuotes()
    {
        var result = FullTextQuery.BuildContainsExpression("hello");
        Assert.Equal("\"hello\"", result);
    }

    [Fact]
    public void BuildContainsExpression_MultipleTerms_JoinedWithOr()
    {
        var result = FullTextQuery.BuildContainsExpression("hello world");
        Assert.Equal("\"hello\" OR \"world\"", result);
    }

    [Fact]
    public void BuildContainsExpression_MultipleSpaces_HandledCorrectly()
    {
        var result = FullTextQuery.BuildContainsExpression("hello    world   test");
        Assert.Equal("\"hello\" OR \"world\" OR \"test\"", result);
    }

    [Fact]
    public void BuildContainsExpression_WithQuotes_QuotesRemoved()
    {
        var result = FullTextQuery.BuildContainsExpression("\"hello\" \"world\"");
        Assert.Equal("\"hello\" OR \"world\"", result);
    }

    [Fact]
    public void BuildContainsExpression_MixedQuotes_Handled()
    {
        var result = FullTextQuery.BuildContainsExpression("hello\"world test");
        Assert.Equal("\"helloworld\" OR \"test\"", result);
    }

    [Fact]
    public void BuildContainsExpression_SingleQuote_Preserved()
    {
        var result = FullTextQuery.BuildContainsExpression("hello'world");
        Assert.Equal("\"hello'world\"", result);
    }

    [Fact]
    public void BuildContainsExpression_TabsAndNewlines_Handled()
    {
        var result = FullTextQuery.BuildContainsExpression("hello\tworld\ntest");
        Assert.Equal("\"hello\" OR \"world\" OR \"test\"", result);
    }
}
