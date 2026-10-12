namespace AdminIdentity.App;

public sealed class AdminPermissionNode
{
    public required string Code { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool? IsEnabled { get; set; }
    public List<AdminPermissionNode> Children { get; } = [];
}

public sealed record AdminPermissionEntry(string Code, string Name, string? Description, bool IsEnabled);

public static class AdminPermissionTree
{
    public static IReadOnlyList<AdminPermissionNode> Build(IEnumerable<AdminPermissionEntry> entries)
    {
        var roots = new List<AdminPermissionNode>();
        var nodes = new Dictionary<string, AdminPermissionNode>(StringComparer.Ordinal);

        foreach (var entry in entries.OrderBy(x => x.Code, StringComparer.Ordinal))
        {
            if (!AdminPermissions.IsCatalogCode(entry.Code))
            {
                continue;
            }

            var parts = entry.Code.Split(':');
            var path = "";
            List<AdminPermissionNode> siblings = roots;
            foreach (var part in parts)
            {
                path = path.Length == 0 ? part : $"{path}:{part}";
                if (!nodes.TryGetValue(path, out var node))
                {
                    node = new AdminPermissionNode { Code = path, Name = part };
                    nodes.Add(path, node);
                    siblings.Add(node);
                }
                siblings = node.Children;
            }

            var leaf = nodes[entry.Code];
            leaf.Name = entry.Name;
            leaf.Description = entry.Description;
            leaf.IsEnabled = entry.IsEnabled;
        }

        return roots;
    }
}
