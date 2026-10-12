using System.Net;

namespace Tests.Host.DebugTools;

internal static class DebugToolShellPage
{
    private static readonly DebugToolMenuItem[] MenuItems =
    [
        new("房型匹配调试", "/room-match", "RM"),
        new("静态数据查询", "/supplier-static-data", "SD"),
        new("酒店匹配结果", "/hotel-match-results", "MR"),
        new("酒店重复候选", "/hotel-match-conflicts", "MC"),
        new("酒店匹配投递", "/hotel-match-deliveries", "MD"),
        new("酒店 Redis 同步", "/hotel-match-redis-sync", "RS")
    ];

    public static string Render(string title, string activePath, string pageHtml)
    {
        var pageStyles = ExtractBetween(pageHtml, "<style>", "</style>");
        var pageBody = ExtractBetween(pageHtml, "<body>", "</body>");
        var encodedTitle = WebUtility.HtmlEncode(title);

        return $$"""
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>{{encodedTitle}} - Tests.Host</title>
    <style>
{{pageStyles}}

        :root {
            --host-sidebar: #17202b;
            --host-sidebar-hover: #222e3b;
            --host-sidebar-active: #2c3948;
            --host-border: #e2e7ec;
            --host-text: #1f2937;
            --host-muted: #768292;
            --host-brand: #2f78d0;
        }

        html, body {
            width: 100%;
            height: 100%;
            min-height: 100%;
            margin: 0;
            padding: 0;
            overflow: hidden;
            background: #f3f5f7;
        }

        .host-shell {
            width: 100%;
            height: 100vh;
            display: grid;
            grid-template-columns: 224px minmax(0, 1fr);
            overflow: hidden;
            color: var(--host-text);
            font-family: "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
        }

        .host-sidebar {
            min-height: 0;
            background: var(--host-sidebar);
            color: #d7dee7;
            display: flex;
            flex-direction: column;
        }

        .host-brand {
            height: 58px;
            padding: 0 18px;
            border-bottom: 1px solid rgba(255, 255, 255, 0.08);
            display: flex;
            align-items: center;
            gap: 11px;
        }

        .host-brand-mark {
            width: 30px;
            height: 30px;
            border-radius: 5px;
            background: var(--host-brand);
            color: #fff;
            display: grid;
            place-items: center;
            font-size: 13px;
            font-weight: 700;
        }

        .host-brand-name {
            min-width: 0;
            font-size: 15px;
            font-weight: 650;
            color: #fff;
        }

        .host-brand-subtitle {
            margin-top: 1px;
            color: #8f9baa;
            font-size: 11px;
        }

        .host-nav {
            padding: 16px 10px;
            display: flex;
            flex-direction: column;
            gap: 4px;
        }

        .host-nav-label {
            padding: 0 10px 8px;
            color: #748292;
            font-size: 11px;
            font-weight: 600;
        }

        .host-nav a {
            min-height: 40px;
            padding: 0 11px;
            border-radius: 5px;
            color: #bdc7d3;
            display: flex;
            align-items: center;
            gap: 11px;
            text-decoration: none;
            font-size: 14px;
            font-weight: 500;
        }

        .host-nav a:hover {
            background: var(--host-sidebar-hover);
            color: #fff;
        }

        .host-nav a.active {
            background: var(--host-sidebar-active);
            color: #fff;
            box-shadow: inset 3px 0 0 var(--host-brand);
        }

        .host-nav-icon {
            width: 26px;
            height: 22px;
            border: 1px solid rgba(255, 255, 255, 0.16);
            border-radius: 4px;
            display: grid;
            place-items: center;
            color: #91a0b1;
            font-size: 10px;
            font-weight: 700;
        }

        .host-main {
            min-width: 0;
            min-height: 0;
            display: grid;
            grid-template-rows: 58px minmax(0, 1fr);
            overflow: hidden;
            background: #f3f5f7;
        }

        .host-header {
            padding: 0 20px;
            border-bottom: 1px solid var(--host-border);
            background: #fff;
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 16px;
        }

        .host-header-title {
            min-width: 0;
            display: flex;
            align-items: center;
            gap: 10px;
            font-size: 15px;
            font-weight: 600;
        }

        .host-header-parent {
            color: var(--host-muted);
            font-size: 13px;
            font-weight: 400;
        }

        .host-environment {
            color: #357145;
            font-size: 12px;
            display: flex;
            align-items: center;
            gap: 7px;
        }

        .host-environment::before {
            content: "";
            width: 7px;
            height: 7px;
            border-radius: 50%;
            background: #3c9952;
        }

        .host-tool {
            min-width: 0;
            min-height: 0;
            overflow: auto;
        }

        .host-tool > .page {
            width: 100%;
            height: auto;
            min-height: 100%;
            padding: 16px;
            gap: 12px;
            overflow: visible;
        }

        .host-tool .card {
            border: 1px solid var(--host-border);
            border-radius: 6px;
            background: #fff;
            box-shadow: none;
            backdrop-filter: none;
            -webkit-backdrop-filter: none;
        }

        .host-tool .hero {
            display: none;
        }

        .host-tool input,
        .host-tool select,
        .host-tool button,
        .host-tool .metric,
        .host-tool .tab-button,
        .host-tool .relation-button {
            border-radius: 5px;
        }

        @media (max-width: 800px) {
            html, body { overflow: auto; }

            .host-shell {
                height: auto;
                min-height: 100vh;
                grid-template-columns: minmax(0, 1fr);
                overflow: visible;
            }

            .host-sidebar { min-height: auto; }
            .host-brand { height: 52px; }

            .host-nav {
                padding: 8px 10px;
                flex-direction: row;
                overflow-x: auto;
            }

            .host-nav-label { display: none; }
            .host-nav a { flex: 0 0 auto; }

            .host-main {
                min-height: calc(100vh - 108px);
                grid-template-rows: 52px minmax(0, 1fr);
                overflow: visible;
            }

            .host-tool { overflow: visible; }
            .host-header { padding: 0 14px; }
            .host-header-parent, .host-environment { display: none; }
            .host-tool > .page { padding: 10px; }
        }
    </style>
</head>
<body>
    <div class="host-shell">
        <aside class="host-sidebar">
            <div class="host-brand">
                <div class="host-brand-mark">MT</div>
                <div>
                    <div class="host-brand-name">Tests.Host</div>
                    <div class="host-brand-subtitle">系统调试工具</div>
                </div>
            </div>
            <nav class="host-nav" aria-label="调试工具">
                <div class="host-nav-label">调试工具</div>
{{RenderMenu(activePath)}}
            </nav>
        </aside>
        <main class="host-main">
            <header class="host-header">
                <div class="host-header-title">
                    <span class="host-header-parent">调试工具 /</span>
                    <span>{{encodedTitle}}</span>
                </div>
                <div class="host-environment">Development</div>
            </header>
            <section class="host-tool">
{{pageBody}}
            </section>
        </main>
    </div>
</body>
</html>
""";
    }

    private static string RenderMenu(string activePath)
    {
        return string.Join(Environment.NewLine, MenuItems.Select(item =>
        {
            var activeClass = string.Equals(item.Path, activePath, StringComparison.OrdinalIgnoreCase)
                ? " class=\"active\""
                : string.Empty;

            return $"                <a href=\"{item.Path}\"{activeClass}><span class=\"host-nav-icon\">{item.Icon}</span><span>{WebUtility.HtmlEncode(item.Title)}</span></a>";
        }));
    }

    private static string ExtractBetween(string html, string startMarker, string endMarker)
    {
        var start = html.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return string.Empty;
        }

        start += startMarker.Length;
        var end = html.IndexOf(endMarker, start, StringComparison.OrdinalIgnoreCase);
        return end < 0 ? html[start..] : html[start..end];
    }

    private sealed record DebugToolMenuItem(string Title, string Path, string Icon);
}
