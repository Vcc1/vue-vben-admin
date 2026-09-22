namespace AdminIdentity.App;

public static class AdminPermissions
{
    public static bool IsCatalogCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && code.Trim() == code &&
        !code.Contains('*') && !code.StartsWith(':') && !code.EndsWith(':') &&
        !code.Contains("::", StringComparison.Ordinal);

    public static bool IsGrantCode(string? code) =>
        code == "*" ||
        (code is not null && code.EndsWith(":*", StringComparison.Ordinal) &&
         IsCatalogCode(code[..^2])) ||
        IsCatalogCode(code);
}

public interface IRolePermissionCatalog
{
    IReadOnlyDictionary<string, IReadOnlyList<string>> GetRolePermissions();
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
