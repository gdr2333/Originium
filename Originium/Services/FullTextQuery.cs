namespace Originium.Services;

/// <summary>
/// 将用户输入的关键词转换为合法的 SQL Server CONTAINS 检索表达式。
/// CONTAINS 的第二个参数是布尔检索表达式（"词" AND/OR "词"），
/// 直接传入含空格/引号等字符的原始文本会触发语法错误 7630。
/// </summary>
public static class FullTextQuery
{
    public static string BuildContainsExpression(string? keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return string.Empty;
        }

        var terms = keywords
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Replace("\"", string.Empty))
            .Where(t => t.Length > 0)
            .Select(t => $"\"{t}\"");

        return string.Join(" OR ", terms);
    }
}
