using System.Text.Json;
using System.Text.Json.Nodes;

namespace Auditing.App;

/// <summary>
/// 审计 body 脱敏：完全由调用方配置的 path 列表驱动（无内置默认词）。
/// 单段 path（如 "idCardNo"）= 任意层级按名匹配；多段 path（如 "items[*].pin"）= 从根精确匹配，
/// 段内 "*" 通配任意键或任意数组元素。基于 JsonNode 树替换，不使用正则。
/// </summary>
public static class SensitivePathRedactor
{
    private const string Mask = "***";

    public static (string Value, bool Truncated) RedactAndCap(
        string? json,
        IReadOnlyList<string>? sensitivePaths,
        int maxLength)
    {
        if (string.IsNullOrEmpty(json))
        {
            return (json ?? string.Empty, false);
        }

        var redacted = NeedsScan(json, sensitivePaths)
            ? RedactCore(json, sensitivePaths)
            : json;

        if (maxLength > 0 && redacted.Length > maxLength)
        {
            return (redacted[..maxLength], true);
        }

        return (redacted, false);
    }

    // 快路径：未配置任何敏感 path，或 body 不含任何 path 叶子名时，跳过 JsonNode 解析。
    private static bool NeedsScan(string json, IReadOnlyList<string>? sensitivePaths)
    {
        if (sensitivePaths is null || sensitivePaths.Count == 0)
        {
            return false;
        }

        foreach (var path in sensitivePaths)
        {
            var leaf = LeafName(path);
            if (leaf.Length != 0 && json.Contains(leaf, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string RedactCore(string json, IReadOnlyList<string>? sensitivePaths)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            // 非合法 JSON（如纯文本 body），无法按树脱敏，原样返回交由限长处理。
            return json;
        }

        var normalized = Normalize(sensitivePaths);
        Walk(root, [], normalized);
        return root?.ToJsonString() ?? json;
    }

    private static void Walk(JsonNode? node, IReadOnlyList<string> path, IReadOnlyList<IReadOnlyList<string>> patterns)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kvp => kvp.Key).ToList())
                {
                    var childPath = Append(path, key);
                    if (IsSensitive(key, childPath, patterns))
                    {
                        obj[key] = Mask;
                    }
                    else
                    {
                        Walk(obj[key], childPath, patterns);
                    }
                }
                break;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    Walk(arr[i], Append(path, "*"), patterns);
                }
                break;
        }
    }

    private static bool IsSensitive(
        string key,
        IReadOnlyList<string> path,
        IReadOnlyList<IReadOnlyList<string>> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (Matches(pattern, path))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Matches(IReadOnlyList<string> pattern, IReadOnlyList<string> path)
    {
        // 单段 path：任意层级按名匹配。
        if (pattern.Count == 1)
        {
            var name = pattern[0];
            return name == "*" || path.Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        // 多段 path：从根精确匹配，段内 "*" 通配单个键/数组元素。
        if (pattern.Count != path.Count)
        {
            return false;
        }

        for (var i = 0; i < pattern.Count; i++)
        {
            if (pattern[i] == "*")
            {
                continue;
            }

            if (!string.Equals(pattern[i], path[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<IReadOnlyList<string>> Normalize(IReadOnlyList<string>? sensitivePaths)
    {
        if (sensitivePaths is null || sensitivePaths.Count == 0)
        {
            return [];
        }

        var result = new List<IReadOnlyList<string>>(sensitivePaths.Count);
        foreach (var raw in sensitivePaths)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var segments = raw
                .Replace("[*]", ".*", StringComparison.Ordinal)
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length != 0)
            {
                result.Add(segments);
            }
        }

        return result;
    }

    private static string LeafName(string path)
    {
        var normalized = path.Replace("[*]", ".*", StringComparison.Ordinal);
        var index = normalized.LastIndexOf('.');
        var leaf = index >= 0 ? normalized[(index + 1)..] : normalized;
        return leaf == "*" ? string.Empty : leaf;
    }

    private static IReadOnlyList<string> Append(IReadOnlyList<string> path, string segment)
    {
        var next = new List<string>(path.Count + 1);
        next.AddRange(path);
        next.Add(segment);
        return next;
    }
}
