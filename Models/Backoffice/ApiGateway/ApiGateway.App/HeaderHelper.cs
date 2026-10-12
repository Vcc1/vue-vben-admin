using System.Text.Json;

namespace ApiGateway.App;

public static class HeaderHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>安全敏感 Header，禁止通过配置覆盖</summary>
    private static readonly HashSet<string> BlockedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Cookie", "Host", "X-Api-Key",
        "Content-Length", "Transfer-Encoding", "Connection"
    };

    public static Dictionary<string, string>? ParseHeaders(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static string? SerializeHeaders(Dictionary<string, string>? headers)
    {
        if (headers is null || headers.Count == 0) return null;
        return JsonSerializer.Serialize(headers, JsonOptions);
    }

    /// <summary>
    /// 合并 API 级和 Module 级 Header。Module 级覆盖 API 级同名项。
    /// 自动过滤安全敏感 Header。
    /// </summary>
    public static Dictionary<string, string> MergeHeaders(
        Dictionary<string, string>? apiHeaders,
        Dictionary<string, string>? moduleHeaders)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (apiHeaders is not null)
        {
            foreach (var kv in apiHeaders)
            {
                if (!BlockedHeaders.Contains(kv.Key))
                    result[kv.Key] = kv.Value;
            }
        }

        if (moduleHeaders is not null)
        {
            foreach (var kv in moduleHeaders)
            {
                if (!BlockedHeaders.Contains(kv.Key))
                    result[kv.Key] = kv.Value;
            }
        }

        return result;
    }
}
