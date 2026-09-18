using System.Text.RegularExpressions;

namespace ApiGateway.Infra;

internal static partial class GatewayPermissionMatcher
{
    /// <summary>
    /// Matches an actual request path against a spec path that may contain {param} placeholders.
    /// e.g. spec "/hotels/{hotelId}" matches "/hotels/123".
    /// Used by the proxy middleware for endpoint-level matching.
    /// </summary>
    internal static bool PathMatches(string specPath, string actualPath)
    {
        // Normalize: ensure leading slash, no trailing slash
        specPath = "/" + specPath.Trim('/');
        actualPath = "/" + actualPath.Trim('/');

        // Fast path: no parameters → exact match
        if (!specPath.Contains('{'))
            return specPath.Equals(actualPath, StringComparison.OrdinalIgnoreCase);

        // Build regex: escape everything, then replace escaped \{...\} with [^/]+
        var escaped = Regex.Escape(specPath);
        var pattern = ParamPlaceholderRegex().Replace(escaped, "[^/]+");
        return Regex.IsMatch(actualPath, "^" + pattern + "$", RegexOptions.IgnoreCase);
    }

    [GeneratedRegex(@"\\{[^}]+\\}")]
    private static partial Regex ParamPlaceholderRegex();
}
