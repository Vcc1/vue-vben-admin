using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

var options = RenderOptions.Parse(args);
var sourceRoot = Path.GetFullPath(options.SourceDirectory);
var outputRoot = Path.GetFullPath(options.OutputDirectory);

if (!Directory.Exists(sourceRoot))
{
    Console.Error.WriteLine($"Documentation source directory was not found: {sourceRoot}");
    return 1;
}

var documents = Directory
    .EnumerateFiles(sourceRoot, "*.md", SearchOption.AllDirectories)
    .Select(path => MarkdownDocument.Parse(path, sourceRoot))
    .OrderBy(document => document.RelativePath, StringComparer.OrdinalIgnoreCase)
    .ToArray();

var catalog = Catalog.Load(Path.Combine(sourceRoot, "catalog.json"));
var assets = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
    .Where(path => !string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(Path.GetFileName(path), "catalog.json", StringComparison.OrdinalIgnoreCase))
    .Select(path => Path.GetRelativePath(sourceRoot, path).Replace('\\', '/'))
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (documents.Length == 0)
{
    Console.Error.WriteLine($"No Markdown documents found under: {sourceRoot}");
    return 1;
}

if (options.Clean && Directory.Exists(outputRoot))
{
    Directory.Delete(outputRoot, recursive: true);
}

Directory.CreateDirectory(outputRoot);
foreach (var document in documents)
{
    var outputPath = Path.Combine(outputRoot, HtmlRenderer.OutputPath(document.RelativePath));
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    await File.WriteAllTextAsync(outputPath, HtmlRenderer.Render(document, documents), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

foreach (var asset in assets)
{
    var destination = Path.Combine(outputRoot, asset.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    File.Copy(Path.Combine(sourceRoot, asset.Replace('/', Path.DirectorySeparatorChar)), destination, overwrite: true);
}

var indexPath = Path.Combine(outputRoot, "index.html");
await File.WriteAllTextAsync(indexPath, HtmlRenderer.RenderIndex(documents, catalog, assets), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
await File.WriteAllTextAsync(Path.Combine(outputRoot, "catalog-by-path.html"), HtmlRenderer.RenderByPath(catalog, documents), new UTF8Encoding(false));
await File.WriteAllTextAsync(Path.Combine(outputRoot, "catalog-by-feature.html"), HtmlRenderer.RenderByFeature(catalog, documents), new UTF8Encoding(false));
await File.WriteAllTextAsync(Path.Combine(outputRoot, "assets.html"), HtmlRenderer.RenderAssets(assets, catalog, documents), new UTF8Encoding(false));
await File.WriteAllTextAsync(Path.Combine(outputRoot, "models-structure.html"), HtmlRenderer.RenderModelsStructure(catalog, documents), new UTF8Encoding(false));
await File.WriteAllTextAsync(Path.Combine(outputRoot, "models-structure.svg"), HtmlRenderer.RenderModelsStructureSvg(catalog), new UTF8Encoding(false));

var manifest = documents.Select(document => new
{
    path = document.RelativePath.Replace('\\', '/'),
    output = HtmlRenderer.OutputPath(document.RelativePath),
    title = document.Title,
    headings = document.Headings.Select(heading => new { level = heading.Level, text = heading.Text, anchor = heading.Anchor })
});
await File.WriteAllTextAsync(Path.Combine(outputRoot, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
}), new UTF8Encoding(false));

Console.WriteLine($"Rendered {documents.Length} document(s) to {outputRoot}");
return 0;

internal sealed record RenderOptions(string SourceDirectory, string OutputDirectory, bool Clean)
{
    public static RenderOptions Parse(string[] args)
    {
        var source = GetValue(args, "--source") ?? Path.Combine("Documentation", "Source");
        var output = GetValue(args, "--output") ?? Path.Combine("Documentation", "site");
        return new RenderOptions(source, output, args.Contains("--clean", StringComparer.OrdinalIgnoreCase));
    }

    private static string? GetValue(string[] args, string name)
    {
        var index = Array.FindIndex(args, argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}

internal sealed record MarkdownHeading(int Level, string Text, string Anchor);

internal sealed record CatalogFeature(string Id, string Name, string Summary, string? Document, string? Asset);
internal sealed record CatalogProject(string Id, string Name, string Path, string Summary, IReadOnlyList<CatalogFeature> Features, IReadOnlyList<string> Documents);

internal sealed record Catalog(IReadOnlyList<CatalogProject> Projects, IReadOnlyList<CatalogNode> Structure)
{
    public static Catalog Load(string catalogPath)
    {
        var configured = File.Exists(catalogPath)
            ? JsonSerializer.Deserialize<Catalog>(File.ReadAllText(catalogPath, Encoding.UTF8), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            : new Catalog(Array.Empty<CatalogProject>(), Array.Empty<CatalogNode>());
        return configured ?? new Catalog(Array.Empty<CatalogProject>(), Array.Empty<CatalogNode>());
    }
}

internal sealed record CatalogNode(string Name, string? Path, string? Document, IReadOnlyList<CatalogNode> Children);

internal sealed record MarkdownDocument(string RelativePath, string Title, IReadOnlyList<MarkdownHeading> Headings, IReadOnlyList<string> Lines)
{
    public static MarkdownDocument Parse(string path, string sourceRoot)
    {
        var relativePath = Path.GetRelativePath(sourceRoot, path);
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var headings = new List<MarkdownHeading>();
        var usedAnchors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inCode = false;
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inCode = !inCode;
                continue;
            }
            if (inCode)
            {
                continue;
            }
            var match = Regex.Match(line, "^(#{1,6})\\s+(.+?)\\s*$");
            if (!match.Success)
            {
                continue;
            }

            var text = Regex.Replace(match.Groups[2].Value, "\\s+#+\\s*$", string.Empty).Trim();
            var anchor = Slugify(text);
            var baseAnchor = anchor;
            var suffix = 2;
            while (!usedAnchors.Add(anchor))
            {
                anchor = $"{baseAnchor}-{suffix++}";
            }
            headings.Add(new MarkdownHeading(match.Groups[1].Value.Length, text, anchor));
        }

        var title = headings.FirstOrDefault(heading => heading.Level == 1)?.Text
            ?? Path.GetFileNameWithoutExtension(path);
        return new MarkdownDocument(relativePath, title, headings, lines);
    }

    private static string Slugify(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, "[^\\p{L}\\p{Nd}]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "section" : normalized;
    }
}

internal static class HtmlRenderer
{
    public static string Render(MarkdownDocument document, IReadOnlyList<MarkdownDocument> allDocuments)
    {
        var body = new StringBuilder();
        body.AppendLine($"<header><a class=\"brand\" href=\"{RelativeLink(document.RelativePath, "index.html")}\">MengtuV 文档</a></header>");
        body.AppendLine("<div class=\"layout\"><aside><h2>目录</h2><nav class=\"side-nav\">");
        body.AppendLine($"<a href=\"{RelativeLink(document.RelativePath, "index.html")}\">总览</a>");
        body.AppendLine($"<a href=\"{RelativeLink(document.RelativePath, "models-structure.html")}\">Models 结构图</a>");
        foreach (var item in allDocuments)
        {
            var current = string.Equals(item.RelativePath, document.RelativePath, StringComparison.OrdinalIgnoreCase) ? " class=\"current\"" : string.Empty;
            body.AppendLine($"<a{current} href=\"{RelativeLink(document.RelativePath, OutputPath(item.RelativePath))}\">{Encode(item.Title)}</a>");
        }
        body.AppendLine("</nav></aside><main>");
        body.AppendLine($"<p class=\"crumb\">{Encode(document.RelativePath.Replace('\\', '/'))}</p>");
        RenderTableOfContents(body, document);
        RenderMarkdown(body, document);
        body.AppendLine("</main></div>");
        return Page(document.Title, body.ToString());
    }

    public static string RenderIndex(IReadOnlyList<MarkdownDocument> documents, Catalog catalog, IReadOnlyList<string> assets)
    {
        var body = new StringBuilder("<header><span class=\"brand\">MengtuV 文档</span></header><div class=\"layout catalog-layout\">");
        RenderCatalogSidebar(body, catalog, documents, "index.html");
        body.AppendLine("<main class=\"index\">");
        body.AppendLine("<h1>MengtuV 项目目录</h1><p>项目和功能说明来自 <code>Documentation/Source/catalog.json</code>；项目列表同时由解决方案中的全部项目自动补齐。</p>");
        body.AppendLine($"<p class=\"stats\">项目 {catalog.Projects.Count} 个，说明文档 {documents.Count} 个，资源 {assets.Count} 个。</p><h2>项目和功能</h2><ul class=\"project-list\">");
        foreach (var project in catalog.Projects)
        {
            body.AppendLine($"<li><strong>{Encode(project.Name)}</strong><code>{Encode(project.Path)}</code><p>{Inline(project.Summary)}</p>");
            if (project.Features.Count > 0)
            {
                body.AppendLine("<ul class=\"feature-list\">");
                foreach (var feature in project.Features)
                {
                    var target = feature.Document is not null ? OutputPath(feature.Document) : feature.Asset;
                    var link = target is null ? string.Empty : $" <a href=\"{Encode(target)}\">查看</a>";
                    body.AppendLine($"<li><b>{Encode(feature.Name)}</b>：{Inline(feature.Summary)}{link}</li>");
                }
                body.AppendLine("</ul>");
            }
            body.AppendLine("</li>");
        }
        body.AppendLine("</ul><h2>说明文档</h2><ul class=\"document-list\">");
        foreach (var document in documents)
        {
            body.AppendLine($"<li><a href=\"{Encode(OutputPath(document.RelativePath))}\">{Encode(document.Title)}</a><span>{Encode(document.RelativePath.Replace('\\', '/'))}</span></li>");
        }
        body.AppendLine("</ul>");
        if (assets.Count > 0)
        {
            body.AppendLine("<h2>资源文件</h2><ul class=\"document-list\">");
            foreach (var asset in assets)
            {
                var extension = Path.GetExtension(asset).ToLowerInvariant();
                var preview = extension is ".svg" or ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp"
                    ? $"<img class=\"asset-preview\" src=\"{Encode(asset)}\" alt=\"{Encode(Path.GetFileName(asset))}\">"
                    : string.Empty;
                body.AppendLine($"<li>{preview}<a href=\"{Encode(asset)}\">{Encode(Path.GetFileName(asset))}</a><span>{Encode(asset)}</span></li>");
            }
            body.AppendLine("</ul>");
        }
        body.AppendLine("</main></div>");
        return Page("项目目录", body.ToString());
    }

    public static string RenderByPath(Catalog catalog, IReadOnlyList<MarkdownDocument> documents)
    {
        var body = new StringBuilder("<header><a class=\"brand\" href=\"index.html\">MengtuV 文档</a></header><div class=\"layout catalog-layout\">");
        RenderCatalogSidebar(body, catalog, documents, "catalog-by-path.html");
        body.AppendLine("<main class=\"index\">");
        body.AppendLine("<h1>按项目路径</h1><p>按物理路径聚合项目，解决方案数字前缀只存在于虚拟目录，不影响这里的路径。</p>");
        foreach (var group in catalog.Projects.GroupBy(project => Path.GetDirectoryName(project.Path)?.Replace('\\', '/') ?? ".", StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            body.AppendLine($"<section class=\"path-group\"><h2><code>{Encode(group.Key)}</code></h2><ul class=\"path-list\">");
            foreach (var project in group.OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase))
                body.AppendLine($"<li><strong>{Encode(project.Name)}</strong><span>{Inline(project.Summary)}</span></li>");
            body.AppendLine("</ul></section>");
        }
        body.AppendLine("</main></div>");
        return Page("按项目路径", body.ToString());
    }

    public static string RenderByFeature(Catalog catalog, IReadOnlyList<MarkdownDocument> documents)
    {
        var body = new StringBuilder("<header><a class=\"brand\" href=\"index.html\">MengtuV 文档</a></header><div class=\"layout catalog-layout\">");
        RenderCatalogSidebar(body, catalog, documents, "catalog-by-feature.html");
        body.AppendLine("<main class=\"index\">");
        body.AppendLine("<h1>按功能</h1><p>功能来自各项目的 <code>features</code> 配置，适合从能力反查实现项目。</p><ul class=\"feature-catalog\">");
        var features = catalog.Projects.SelectMany(project => project.Features.Select(feature => (project, feature)))
            .GroupBy(item => item.feature.Name, StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var group in features)
        {
            body.AppendLine($"<li><h2>{Encode(group.Key)}</h2><ul>");
            foreach (var item in group)
            {
                var target = item.feature.Document is not null ? OutputPath(item.feature.Document) : item.feature.Asset;
                var link = target is null ? string.Empty : $" <a href=\"{Encode(target)}\">查看</a>";
                body.AppendLine($"<li><strong>{Encode(item.project.Name)}</strong> <span>{Inline(item.feature.Summary)}</span>{link}</li>");
            }
            body.AppendLine("</ul></li>");
        }
        body.AppendLine("</ul></main></div>");
        return Page("按功能", body.ToString());
    }

    public static string RenderAssets(IReadOnlyList<string> assets, Catalog catalog, IReadOnlyList<MarkdownDocument> documents)
    {
        var body = new StringBuilder("<header><a class=\"brand\" href=\"index.html\">MengtuV 文档</a></header><div class=\"layout catalog-layout\">");
        RenderCatalogSidebar(body, catalog, documents, "assets.html");
        body.AppendLine("<main class=\"asset-gallery\">");
        body.AppendLine("<h1>SVG 与资源</h1><p>SVG 图按原始比例全宽展示，不放入固定尺寸卡片；其他资源提供原文件入口。</p>");
        foreach (var asset in assets)
        {
            var extension = Path.GetExtension(asset).ToLowerInvariant();
            body.AppendLine($"<section class=\"asset-item\"><h2><a href=\"{Encode(asset)}\">{Encode(asset)}</a></h2>");
            if (extension == ".svg") body.AppendLine($"<img class=\"svg-unbounded\" src=\"{Encode(asset)}\" alt=\"{Encode(Path.GetFileName(asset))}\">");
            else if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp") body.AppendLine($"<img class=\"raster-preview\" src=\"{Encode(asset)}\" alt=\"{Encode(Path.GetFileName(asset))}\">");
            else body.AppendLine($"<p><a href=\"{Encode(asset)}\">打开文件</a></p>");
            body.AppendLine("</section>");
        }
        body.AppendLine("</main></div>");
        return Page("SVG 与资源", body.ToString());
    }

    public static string RenderModelsStructure(Catalog catalog, IReadOnlyList<MarkdownDocument> documents)
    {
        var body = new StringBuilder("<header><a class=\"brand\" href=\"index.html\">MengtuV 文档</a></header><div class=\"layout catalog-layout\">");
        RenderCatalogSidebar(body, catalog, documents, "models-structure.html");
        body.AppendLine("<main class=\"structure-page\">");
        body.AppendLine("<h1>Models 项目结构</h1><p>结构来自已生成文档中的 <code>catalog.json</code>，驱动不会扫描源码或项目文件。</p>");
        body.AppendLine(RenderModelsStructureSvg(catalog));
        body.AppendLine("</main></div>");
        return Page("Models 项目结构", body.ToString());
    }

    public static string RenderModelsStructureSvg(Catalog catalog)
    {
        var nodes = new List<(CatalogNode Node, int Depth, int Index, int ParentIndex)>();
        var index = 0;
        void Visit(IEnumerable<CatalogNode> children, int depth, int parentIndex)
        {
            foreach (var node in children)
            {
                var current = index++;
                nodes.Add((node, depth, current, parentIndex));
                Visit(node.Children ?? Array.Empty<CatalogNode>(), depth + 1, current);
            }
        }
        Visit(catalog.Structure ?? Array.Empty<CatalogNode>(), 0, -1);
        var rowHeight = 56;
        var height = Math.Max(180, (nodes.Count + 1) * rowHeight);
        var svg = new StringBuilder($"<svg class=\"svg-structure\" xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1280 {height}\" role=\"img\" aria-label=\"Models 项目结构\">");
        svg.AppendLine("<style>.node{fill:#fff;stroke:#6a7d91;stroke-width:1.5}.node.root{fill:#dce9f4;stroke:#145da0}.label{font:16px system-ui,Segoe UI,sans-serif;fill:#17202b}.path{font:12px ui-monospace,Consolas,monospace;fill:#6a7480}.edge{stroke:#a9b4c0;stroke-width:1.5;fill:none}</style>");
        foreach (var item in nodes.Where(item => item.ParentIndex >= 0))
        {
            var parent = nodes[item.ParentIndex];
            var parentY = (parent.Index + 1) * rowHeight + 20;
            var childY = (item.Index + 1) * rowHeight + 20;
            var parentX = 36 + parent.Depth * 54;
            var childX = 36 + item.Depth * 54;
            svg.AppendLine($"<path class=\"edge\" d=\"M {parentX + 470} {parentY} H {childX - 18} V {childY}\"/>");
        }
        foreach (var item in nodes)
        {
            var y = (item.Index + 1) * rowHeight;
            var x = 36 + item.Depth * 54;
            var rootClass = item.Depth == 0 ? " root" : string.Empty;
            var linkStart = string.IsNullOrWhiteSpace(item.Node.Document) ? string.Empty : $"<a href=\"{EncodeSvg(OutputPath(item.Node.Document!))}\">";
            var linkEnd = string.IsNullOrWhiteSpace(item.Node.Document) ? string.Empty : "</a>";
            svg.AppendLine(linkStart);
            svg.AppendLine($"<rect class=\"node{rootClass}\" x=\"{x}\" y=\"{y}\" width=\"470\" height=\"40\" rx=\"5\"/>");
            svg.AppendLine($"<text class=\"label\" x=\"{x + 14}\" y=\"{y + 19}\">{EncodeSvg(item.Node.Name)}</text>");
            if (!string.IsNullOrWhiteSpace(item.Node.Path)) svg.AppendLine($"<text class=\"path\" x=\"{x + 14}\" y=\"{y + 34}\">{EncodeSvg(item.Node.Path!)}</text>");
            svg.AppendLine(linkEnd);
        }
        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private static string EncodeSvg(string value) => WebUtility.HtmlEncode(value);

    private static void RenderCatalogNav(StringBuilder body) => body.AppendLine("<nav class=\"catalog-nav\"><a href=\"index.html\">总览</a><a href=\"models-structure.html\">Models 结构图</a><a href=\"catalog-by-path.html\">按项目路径</a><a href=\"catalog-by-feature.html\">按功能</a><a href=\"assets.html\">SVG 与资源</a></nav>");

    private static void RenderCatalogSidebar(StringBuilder body, Catalog catalog, IReadOnlyList<MarkdownDocument> documents, string current)
    {
        body.AppendLine("<aside class=\"catalog-sidebar\"><h2>目录</h2><nav class=\"side-nav\">");
        foreach (var entry in new[] { ("总览", "index.html"), ("Models 结构图", "models-structure.html"), ("按项目路径", "catalog-by-path.html"), ("按功能", "catalog-by-feature.html"), ("SVG 与资源", "assets.html") })
        {
            var active = string.Equals(entry.Item2, current, StringComparison.OrdinalIgnoreCase) ? " class=\"current\"" : string.Empty;
            body.AppendLine($"<a{active} href=\"{entry.Item2}\">{entry.Item1}</a>");
        }
        body.AppendLine("<h3>项目文档</h3>");
        foreach (var document in documents.OrderBy(document => document.RelativePath, StringComparer.OrdinalIgnoreCase))
            body.AppendLine($"<a href=\"{Encode(OutputPath(document.RelativePath))}\">{Encode(document.Title)}</a>");
        body.AppendLine("</nav></aside>");
    }

    private static void RenderTableOfContents(StringBuilder output, MarkdownDocument document)
    {
        if (document.Headings.Count <= 1)
        {
            return;
        }

        output.AppendLine("<section class=\"toc\"><strong>目录</strong><ul>");
        foreach (var heading in document.Headings.Where(heading => heading.Level > 1))
        {
            output.AppendLine($"<li class=\"level-{heading.Level}\"><a href=\"#{heading.Anchor}\">{Encode(heading.Text)}</a></li>");
        }
        output.AppendLine("</ul></section>");
    }

    private static void RenderMarkdown(StringBuilder output, MarkdownDocument document)
    {
        var inCode = false;
        var paragraph = new List<string>();
        var tableRows = new List<string>();
        string? openListTag = null;
        var headingIndex = 0;
        foreach (var rawLine in document.Lines)
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                FlushTable();
                FlushParagraph(output, paragraph);
                CloseList();
                if (!inCode) { output.AppendLine("<pre><code>"); } else { output.AppendLine("</code></pre>"); }
                inCode = !inCode;
                continue;
            }
            if (inCode)
            {
                output.AppendLine(Encode(rawLine));
                continue;
            }
            if (string.IsNullOrWhiteSpace(line))
            {
                FlushTable();
                FlushParagraph(output, paragraph);
                CloseList();
                continue;
            }
            if (line.TrimStart().StartsWith('|') && line.TrimEnd().EndsWith('|'))
            {
                FlushParagraph(output, paragraph);
                tableRows.Add(line.Trim());
                continue;
            }
            FlushTable();
            var heading = Regex.Match(line, "^(#{1,6})\\s+(.+?)\\s*$");
            if (heading.Success)
            {
                FlushParagraph(output, paragraph);
                CloseList();
                var text = Regex.Replace(heading.Groups[2].Value, "\\s+#+\\s*$", string.Empty).Trim();
                var anchor = document.Headings[headingIndex++].Anchor;
                output.AppendLine($"<h{heading.Groups[1].Value.Length} id=\"{anchor}\">{Inline(text)}</h{heading.Groups[1].Value.Length}>");
                continue;
            }
            var listItem = Regex.Match(line, "^\\s*(?<marker>[-*]|\\d+[.)])\\s+(?<text>.+)$");
            if (listItem.Success)
            {
                FlushParagraph(output, paragraph);
                var listTag = char.IsDigit(listItem.Groups["marker"].Value[0]) ? "ol" : "ul";
                if (!string.Equals(openListTag, listTag, StringComparison.Ordinal))
                {
                    CloseList();
                    output.AppendLine($"<{listTag}>");
                    openListTag = listTag;
                }
                output.AppendLine($"<li>{Inline(listItem.Groups["text"].Value)}</li>");
                continue;
            }
            paragraph.Add(line);
        }
        FlushTable();
        FlushParagraph(output, paragraph);
        CloseList();

        void FlushTable()
        {
            if (tableRows.Count == 0) return;
            var rows = tableRows
                .Select(row => row.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray())
                .Where(row => row.Length > 0)
                .ToArray();
            tableRows.Clear();
            if (rows.Length == 0) return;
            output.AppendLine("<table><thead><tr>");
            foreach (var cell in rows[0]) output.Append($"<th>{Inline(cell)}</th>");
            output.AppendLine("</tr></thead>");
            var dataRows = rows.Skip(1).Where(row => !row.All(cell => Regex.IsMatch(cell, "^:?-{3,}:?$")));
            if (dataRows.Any())
            {
                output.AppendLine("<tbody>");
                foreach (var row in dataRows)
                {
                    output.Append("<tr>");
                    foreach (var cell in row) output.Append($"<td>{Inline(cell)}</td>");
                    output.AppendLine("</tr>");
                }
                output.AppendLine("</tbody>");
            }
            output.AppendLine("</table>");
        }

        void CloseList()
        {
            if (openListTag is null) return;
            output.AppendLine($"</{openListTag}>");
            openListTag = null;
        }
    }

    private static void FlushParagraph(StringBuilder output, List<string> paragraph)
    {
        if (paragraph.Count == 0) return;
        output.AppendLine($"<p>{Inline(string.Join(" ", paragraph))}</p>");
        paragraph.Clear();
    }

    private static string Inline(string text)
    {
        var encoded = Encode(text);
        encoded = Regex.Replace(encoded, "`([^`]+)`", "<code>$1</code>");
        encoded = Regex.Replace(encoded, "\\*\\*([^*]+)\\*\\*", "<strong>$1</strong>");
        encoded = Regex.Replace(encoded, "\\[([^]]+)\\]\\(([^)]+)\\)", "<a href=\"$2\">$1</a>");
        return encoded;
    }

    private static string Page(string title, string body) => $"<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>{Encode(title)} - MengtuV</title><style>{Css}</style></head><body>{body}</body></html>";
    private static string RelativeLink(string from, string to)
    {
        var directory = Path.GetDirectoryName(from);
        return Encode(Path.GetRelativePath(string.IsNullOrEmpty(directory) ? "." : directory, to).Replace('\\', '/'));
    }
    public static string OutputPath(string relativePath) => string.Equals(relativePath, "index.md", StringComparison.OrdinalIgnoreCase)
        ? "overview.html"
        : Path.ChangeExtension(relativePath, ".html").Replace('\\', '/');
    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private const string Css = "body{margin:0;color:#20242b;background:#f6f7f9;font:16px/1.65 system-ui,-apple-system,Segoe UI,sans-serif}header{background:#17202b;color:#fff;padding:18px max(24px,calc((100% - 1840px)/2));box-sizing:border-box}.brand{color:#fff;font-weight:700;text-decoration:none}.layout{width:min(1840px,calc(100% - 48px));margin:0 auto;display:grid;grid-template-columns:clamp(220px,18vw,320px) minmax(0,1fr);gap:clamp(28px,4vw,80px);padding:32px 0}.catalog-layout .index{width:auto;margin:0}.catalog-sidebar{position:sticky;top:24px;align-self:start}aside{border-right:1px solid #d8dde5;padding-right:20px}aside h2{font-size:15px;margin:0 0 12px}nav a{display:block;color:#4d5866;text-decoration:none;padding:5px 8px;border-radius:4px}nav a:hover,nav a.current{background:#e4e9f0;color:#17202b}.crumb{color:#77818d;font-size:13px}main{min-width:0}h1,h2,h3,h4{line-height:1.25;color:#17202b}h1{font-size:34px}h2{font-size:25px;margin-top:34px}h3{font-size:20px;margin-top:26px}.toc{border-left:3px solid #6a7d91;background:#fff;padding:12px 18px;margin:24px 0}.toc ul{margin:6px 0;padding-left:20px;list-style:none}.toc .level-3{padding-left:16px}.toc .level-4,.toc .level-5,.toc .level-6{padding-left:32px}a{color:#145da0}code{background:#e9edf2;border-radius:3px;padding:2px 5px;font-size:.92em}pre{background:#202832;color:#edf2f7;padding:16px;overflow:auto;border-radius:5px}pre code{background:none;padding:0}table{border-collapse:collapse;width:100%;margin:16px 0;background:#fff}th,td{border:1px solid #d8dde5;text-align:left;padding:8px 10px}th{background:#e9edf2}li{margin:4px 0}.index{width:min(1760px,calc(100% - 48px));margin:0 auto;padding:48px 0}.catalog-nav{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:28px}.catalog-nav a{background:#fff;border:1px solid #cbd4df;padding:7px 12px;border-radius:4px;text-decoration:none}.project-list{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,420px),1fr));gap:14px;padding:0;list-style:none}.project-list>li{background:#fff;border:1px solid #d8dde5;border-radius:6px;padding:16px;min-width:0}.project-list>li>code{display:block;color:#77818d;margin-top:5px;overflow-wrap:anywhere}.feature-list{margin-top:10px}.stats{color:#5d6875}.document-list,.path-list,.feature-catalog{list-style:none;padding:0}.document-list li,.path-list li{display:flex;align-items:center;justify-content:space-between;gap:20px;border-bottom:1px solid #d8dde5;padding:12px 0}.document-list span,.path-list span{color:#77818d;font-size:13px}.path-group{margin:24px 0}.feature-catalog>li{background:#fff;border:1px solid #d8dde5;padding:8px 16px;margin:12px 0}.asset-preview{width:180px;height:72px;object-fit:contain;object-position:left center;background:#f6f7f9;border:1px solid #d8dde5;margin-right:12px;padding:4px}.asset-gallery{width:100%;margin:0 auto;padding:36px 0}.asset-gallery .catalog-nav,.asset-gallery h1,.asset-gallery>p{width:min(1840px,calc(100% - 48px));margin-left:auto;margin-right:auto}.asset-item{width:100%;margin:48px 0 72px}.asset-item h2{width:min(1840px,calc(100% - 48px));margin:0 auto 16px}.svg-unbounded,.svg-structure{display:block;width:100%;height:auto;max-width:none}.raster-preview{display:block;width:auto;max-width:100%;height:auto;margin:0 auto}.structure-page{width:100%}@media(max-width:760px){.layout{display:block;width:calc(100% - 32px);padding:20px 0}.catalog-sidebar{position:static}aside{border-right:0;border-bottom:1px solid #d8dde5;padding:0 0 16px;margin-bottom:24px}.index{width:calc(100% - 32px);padding:28px 0}.document-list li,.path-list li{display:block}.document-list span,.path-list span{display:block;margin-top:3px}.asset-preview{display:block;margin-bottom:6px}.asset-gallery .catalog-nav,.asset-gallery h1,.asset-gallery>p,.asset-item h2{width:calc(100% - 32px)}}@media(min-width:1800px){body{font-size:17px}.layout{grid-template-columns:340px minmax(0,1fr)}.project-list{grid-template-columns:repeat(3,minmax(0,1fr))}}html{font-size:clamp(.9375rem,0.72vw,1.375rem)}body{font-size:1rem}header{padding:1.1rem 4vw}.layout{width:92vw;max-width:none;grid-template-columns:16vw minmax(0,1fr);gap:3vw;padding:2.4vw 0}.catalog-layout .index,.index{width:100%;max-width:none;padding:2.8vw 0}.catalog-sidebar{top:1.5rem}aside{border-right:.0625rem solid #d8dde5;padding-right:1.2vw}aside h2{font-size:1rem;margin-bottom:.7rem}nav a{padding:.35rem .55rem;border-radius:.25rem}.crumb{font-size:.8rem}h1{font-size:clamp(2rem,2.2vw,3.2rem)}h2{font-size:clamp(1.35rem,1.5vw,2.2rem);margin-top:2.1rem}h3{font-size:clamp(1.1rem,1.15vw,1.6rem);margin-top:1.5rem}.toc{padding:.8rem 1.1rem;margin:1.5rem 0}pre{padding:1rem;border-radius:.3rem}table{margin:1rem 0}th,td{padding:.5rem .65rem}.project-list{grid-template-columns:repeat(auto-fit,minmax(min(100%,24rem),1fr));gap:1rem}.project-list>li{border:.0625rem solid #d8dde5;border-radius:.35rem;padding:1rem}.catalog-nav{gap:.5rem;margin-bottom:1.5rem}.catalog-nav a{border:.0625rem solid #cbd4df;padding:.45rem .75rem;border-radius:.25rem}.document-list li,.path-list li{gap:1rem;padding:.75rem 0;border-bottom:.0625rem solid #d8dde5}.document-list span,.path-list span{font-size:.8rem}.path-group{margin:1.5rem 0}.feature-catalog>li{border:.0625rem solid #d8dde5;padding:.5rem 1rem;margin:.75rem 0}.asset-gallery .catalog-nav,.asset-gallery h1,.asset-gallery>p,.asset-item h2{width:92vw}.asset-item{margin:3vw 0 4vw}.asset-item h2{margin-bottom:1rem}.asset-preview{width:11rem;height:4.5rem;border:.0625rem solid #d8dde5;margin-right:.75rem;padding:.25rem}.asset-gallery{padding:2.5vw 0}@media(max-width:48rem){.layout{display:block;width:92vw;padding:1.5rem 0}.catalog-sidebar{position:static}aside{border-right:0;border-bottom:.0625rem solid #d8dde5;padding:0 0 1rem;margin-bottom:1.5rem}.index{width:100%;padding:1.5rem 0}.document-list li,.path-list li{display:block}.document-list span,.path-list span{display:block;margin-top:.25rem}.asset-preview{display:block;margin-bottom:.4rem}.asset-gallery .catalog-nav,.asset-gallery h1,.asset-gallery>p,.asset-item h2{width:92vw}}";
}
