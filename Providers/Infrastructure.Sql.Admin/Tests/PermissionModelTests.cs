using AdminIdentity.App;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Sql.Admin.Tests;

public sealed class PermissionModelTests
{
    [Fact]
    public void DirectoryCodesRejectWildcard_ButGrantsAllowTerminalWildcard()
    {
        Assert.True(AdminPermissions.IsCatalogCode("paycardhub:cards:read"));
        Assert.False(AdminPermissions.IsCatalogCode("paycardhub:*"));
        Assert.False(AdminPermissions.IsCatalogCode("paycardhub::read"));
        Assert.True(AdminPermissions.IsGrantCode("*"));
        Assert.True(AdminPermissions.IsGrantCode("paycardhub:*"));
        Assert.True(AdminPermissions.IsGrantCode("paycardhub:cards:*"));
        Assert.False(AdminPermissions.IsGrantCode("paycardhub:*:read"));
    }

    [Fact]
    public void TreeBuildsArbitraryDepth_AndUsesExplicitNodeMetadata()
    {
        var roots = AdminPermissionTree.Build([
            new("paycardhub:cards:read:detail:export", "导出", "导出明细", true),
            new("paycardhub:cards", "卡片", "卡片管理", false),
            new("paycardhub:*", "invalid", null, true)
        ]);

        var root = Assert.Single(roots);
        Assert.Equal("paycardhub", root.Code);
        Assert.Null(root.IsEnabled);
        var cards = Assert.Single(root.Children);
        Assert.Equal("卡片", cards.Name);
        Assert.Equal("卡片管理", cards.Description);
        Assert.False(cards.IsEnabled);
        var export = Assert.Single(Assert.Single(Assert.Single(cards.Children).Children).Children);
        Assert.Equal("paycardhub:cards:read:detail:export", export.Code);
        Assert.Equal("导出明细", export.Description);
    }

    [Fact]
    public void RoleGrantIsNotConstrainedByCatalogForeignKey()
    {
        var options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PermissionModelTests;Trusted_Connection=True")
            .Options;
        using var db = new AdminDbContext(options);
        var permission = db.Model.FindEntityType(typeof(AdminPermission))!;
        var grant = db.Model.FindEntityType(typeof(AdminRolePermission))!;

        Assert.NotNull(permission.FindProperty(nameof(AdminPermission.Description)));
        Assert.Null(permission.FindProperty("GroupName"));
        Assert.Empty(grant.GetForeignKeys());
    }
}
