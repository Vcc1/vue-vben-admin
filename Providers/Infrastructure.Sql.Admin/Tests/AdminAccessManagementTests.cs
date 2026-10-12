using AdminIdentity.App;
using AdminIdentity.Infra;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace Infrastructure.Sql.Admin.Tests;

public sealed class AdminAccessManagementTests
{
    [Fact]
    public async Task RepeatedRoleGrantSave_PreservesExistingKeys_AndAuditsChanges()
    {
        await using var fixture = await Fixture.CreateAsync();
        var role = await fixture.Service.CreateRoleAsync(new("TestRole"), default);
        await fixture.Service.CreatePermissionAsync(new("paycardhub:cards:read", "Read", null), default);

        await fixture.Service.SetRolePermissionsAsync(role.Name, ["paycardhub:cards:read", "paycardhub:*"], default);
        await fixture.Service.SetRolePermissionsAsync(role.Name, ["paycardhub:cards:read", "paycardhub:*"], default);
        await fixture.Service.SetRolePermissionsAsync(role.Name, ["paycardhub:*"], default);

        var grants = await fixture.Db.RolePermissions.Where(x => x.RoleName == role.Name)
            .Select(x => x.PermissionCode).ToListAsync();
        Assert.Equal(["paycardhub:*"], grants);
        Assert.Equal(3, await fixture.Db.UserAuditLogs.CountAsync(x => x.Action == "SetRolePermissions"));
    }

    [Fact]
    public async Task PermissionCodeIsUnique_AndWildcardCannotEnterDirectory()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreatePermissionAsync(new("a:b", "First", "Description"), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CreatePermissionAsync(new("a:b", "Second", null), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Service.CreatePermissionAsync(new("a:*", "Wildcard", null), default));
        Assert.Equal(1, await fixture.Db.Permissions.CountAsync());
    }

    [Fact]
    public async Task GrantsRejectUnknownExactAndOversizedWildcard()
    {
        await using var fixture = await Fixture.CreateAsync();
        var role = await fixture.Service.CreateRoleAsync(new("TestRole"), default);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Service.SetRolePermissionsAsync(role.Name, ["missing:read"], default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Service.SetRolePermissionsAsync(role.Name, [$"{new string('a', 99)}:*"], default));
        Assert.Empty(await fixture.Db.RolePermissions.ToListAsync());
    }

    [Fact]
    public async Task BuiltInRoleCannotBeDeleted()
    {
        await using var fixture = await Fixture.CreateAsync();
        var roleManager = fixture.Scope.ServiceProvider.GetRequiredService<RoleManager<AdminRole>>();
        Assert.True((await roleManager.CreateAsync(new AdminRole { Name = AdminRoles.Admin })).Succeeded);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.DeleteRoleAsync(AdminRoles.Admin, default));
        Assert.NotNull(await roleManager.FindByNameAsync(AdminRoles.Admin));
    }

    [Fact]
    public async Task PermissionFile_RoundTripsSpecialCharacters_AndAppliesOnlySelectedChanges()
    {
        await using var fixture = await Fixture.CreateAsync();
        const string description = "中文,冒号:引号\"、反斜杠\\、换行\n及 +";
        await fixture.Service.CreatePermissionAsync(new("old:read", "旧权限", description), default);
        await fixture.Service.CreatePermissionAsync(new("remove:read", "保留", null), default);
        var exported = await fixture.Transfer.ExportAsync("permissions", default);
        var json = JsonSerializer.Serialize(exported, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var parsed = JsonSerializer.Deserialize<AccessTransferDocument>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(description, parsed.Permissions!.Single(x => x.Code == "old:read").Description);
        Assert.Empty((await fixture.Transfer.CompareAsync(exported, default)).Changes);

        var incoming = new AccessTransferDocument(exported.Format, exported.Version, "permissions",
            [new("old:read", "改名", description, false),
             new("new:read", "新增", "逗号,\"引号\"\n换行", true)], null);
        var preview = await fixture.Transfer.CompareAsync(incoming, default);
        Assert.Equal(3, preview.Changes.Count);
        Assert.Equal(2, await fixture.Db.Permissions.CountAsync());

        var result = await fixture.Transfer.ApplyAsync(new(incoming, preview.Baseline,
            preview.Changes.Where(x => x.Action != "delete").Select(x => x.Key).ToArray()), default);
        Assert.Equal(2, result.AppliedCount);
        Assert.Equal(3, await fixture.Db.Permissions.CountAsync());
        Assert.Equal(description, (await fixture.Db.Permissions.SingleAsync(x => x.Code == "old:read")).Description);
        Assert.False((await fixture.Db.Permissions.SingleAsync(x => x.Code == "old:read")).IsEnabled);
        Assert.Equal("逗号,\"引号\"\n换行", (await fixture.Db.Permissions.SingleAsync(x => x.Code == "new:read")).Description);
        Assert.Single(await fixture.Db.UserAuditLogs.Where(x => x.Action == "ImportPermissions").ToListAsync());
    }

    [Fact]
    public async Task ApplyRejectsStaleComparisonWithoutWriting()
    {
        await using var fixture = await Fixture.CreateAsync();
        var incoming = new AccessTransferDocument("mengtu-admin-access", 1, "permissions",
            [new("a:read", "A", null, true)], null);
        var preview = await fixture.Transfer.CompareAsync(incoming, default);
        await fixture.Service.CreatePermissionAsync(new("else:read", "B", null), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Transfer.ApplyAsync(
            new(incoming, preview.Baseline, ["a:read"]), default));
        Assert.Null(await fixture.Db.Permissions.FindAsync("a:read"));
        Assert.Empty(await fixture.Db.UserAuditLogs.Where(x => x.Action == "ImportPermissions").ToListAsync());
    }

    [Fact]
    public async Task RoleFile_ReportsBlockedDeletionsAndMissingPermissions()
    {
        await using var fixture = await Fixture.CreateAsync();
        var roleManager = fixture.Scope.ServiceProvider.GetRequiredService<RoleManager<AdminRole>>();
        Assert.True((await roleManager.CreateAsync(new AdminRole { Name = AdminRoles.Admin })).Succeeded);
        var assigned = await fixture.Service.CreateRoleAsync(new("Assigned"), default);
        var userManager = fixture.Scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
        var user = new AdminUser { UserName = "assigned", DisplayName = "Assigned" };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, assigned.Name)).Succeeded);

        var incoming = new AccessTransferDocument("mengtu-admin-access", 1, "roles", null,
            [new("NewRole", ["not-imported:read"])]);
        var preview = await fixture.Transfer.CompareAsync(incoming, default);
        Assert.Equal(3, preview.Changes.Count);
        Assert.All(preview.Changes, x => Assert.NotNull(x.BlockReason));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Transfer.ApplyAsync(
            new(incoming, preview.Baseline, ["NewRole"]), default));
        Assert.Null(await roleManager.FindByNameAsync("NewRole"));
    }

    [Fact]
    public async Task RoleFile_ImportsWildcardAndIgnoresGrantOrder()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreatePermissionAsync(new("paycardhub:cards:read", "Read", null), default);
        var file = new AccessTransferDocument("mengtu-admin-access", 1, "roles", null,
            [new("Reader", ["paycardhub:*", "paycardhub:cards:read"])]);
        var preview = await fixture.Transfer.CompareAsync(file, default);
        Assert.Single(preview.Changes);
        await fixture.Transfer.ApplyAsync(new(file, preview.Baseline, ["Reader"]), default);
        Assert.Empty((await fixture.Transfer.CompareAsync(file, default)).Changes);
        var reordered = file with { Roles = [new("Reader", ["paycardhub:cards:read", "paycardhub:*"])] };
        Assert.Empty((await fixture.Transfer.CompareAsync(reordered, default)).Changes);
    }

    [Fact]
    public async Task PermissionDeletion_RemovesExactGrantsOnlyWhenSelected()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreatePermissionAsync(new("paycardhub:cards:read", "Read", null), default);
        var role = await fixture.Service.CreateRoleAsync(new("Reader"), default);
        await fixture.Service.SetRolePermissionsAsync(role.Name,
            ["paycardhub:cards:read", "paycardhub:*"], default);
        var emptyFile = new AccessTransferDocument("mengtu-admin-access", 1, "permissions", [], null);
        var preview = await fixture.Transfer.CompareAsync(emptyFile, default);
        var deletion = Assert.Single(preview.Changes);
        Assert.Equal("delete", deletion.Action);
        Assert.Contains("1 条", deletion.Impact);
        Assert.Single(await fixture.Db.Permissions.ToListAsync());

        await fixture.Transfer.ApplyAsync(new(emptyFile, preview.Baseline, [deletion.Key]), default);
        Assert.Empty(await fixture.Db.Permissions.ToListAsync());
        Assert.Equal(["paycardhub:*"], await fixture.Db.RolePermissions
            .Select(x => x.PermissionCode).ToListAsync());
    }

    [Fact]
    public async Task TransferRejectsDuplicateCodesAndUnsupportedVersion()
    {
        await using var fixture = await Fixture.CreateAsync();
        var duplicate = new AccessTransferDocument("mengtu-admin-access", 1, "permissions",
            [new("a:read", "A", null, true), new("A:READ", "B", null, true)], null);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Transfer.CompareAsync(duplicate, default));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Transfer.CompareAsync(
            duplicate with { Version = 2 }, default));
        Assert.Empty(await fixture.Db.Permissions.ToListAsync());
    }

    [Fact]
    public async Task TransferShowsAndBlocksCaseOnlyIdentifierConflicts()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreatePermissionAsync(new("cards:read", "Read", null), default);
        var role = await fixture.Service.CreateRoleAsync(new("Reader"), default);

        var permissionFile = new AccessTransferDocument("mengtu-admin-access", 1, "permissions",
            [new("Cards:read", "Read", null, true)], null);
        var permissionPreview = await fixture.Transfer.CompareAsync(permissionFile, default);
        var permissionChange = Assert.Single(permissionPreview.Changes);
        Assert.Equal("update", permissionChange.Action);
        Assert.NotNull(permissionChange.BlockReason);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Transfer.ApplyAsync(
            new(permissionFile, permissionPreview.Baseline, [permissionChange.Key]), default));

        var roleFile = new AccessTransferDocument("mengtu-admin-access", 1, "roles", null,
            [new("reader", [])]);
        var rolePreview = await fixture.Transfer.CompareAsync(roleFile, default);
        var roleChange = Assert.Single(rolePreview.Changes);
        Assert.Equal("update", roleChange.Action);
        Assert.NotNull(roleChange.BlockReason);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Transfer.ApplyAsync(
            new(roleFile, rolePreview.Baseline, [roleChange.Key]), default));

        Assert.Equal("cards:read", (await fixture.Db.Permissions.SingleAsync()).Code);
        Assert.Equal("Reader", role.Name);
        Assert.Empty(await fixture.Db.UserAuditLogs.Where(x =>
            x.Action == "ImportPermissions" || x.Action == "ImportRoles").ToListAsync());
    }

    [Fact]
    public async Task RoleDeletion_RemovesGrantsButDoesNotTouchOtherRoles()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreatePermissionAsync(new("cards:read", "Read", null), default);
        var old = await fixture.Service.CreateRoleAsync(new("Old"), default);
        var keep = await fixture.Service.CreateRoleAsync(new("Keep"), default);
        await fixture.Service.SetRolePermissionsAsync(old.Name, ["cards:read"], default);
        var file = new AccessTransferDocument("mengtu-admin-access", 1, "roles", null,
            [new(keep.Name, [])]);
        var preview = await fixture.Transfer.CompareAsync(file, default);
        var deletion = Assert.Single(preview.Changes);
        Assert.Equal("delete", deletion.Action);
        Assert.Null(deletion.BlockReason);

        await fixture.Transfer.ApplyAsync(new(file, preview.Baseline, [old.Name]), default);
        var roleManager = fixture.Scope.ServiceProvider.GetRequiredService<RoleManager<AdminRole>>();
        Assert.Null(await roleManager.FindByNameAsync(old.Name));
        Assert.NotNull(await roleManager.FindByNameAsync(keep.Name));
        Assert.Empty(await fixture.Db.RolePermissions.ToListAsync());
    }

    [Fact]
    public async Task PermissionPreviewBecomesStaleWhenAffectedRoleChangesEvenIfGrantCountIsSame()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreatePermissionAsync(new("cards:read", "Read", null), default);
        var first = await fixture.Service.CreateRoleAsync(new("First"), default);
        var second = await fixture.Service.CreateRoleAsync(new("Second"), default);
        await fixture.Service.SetRolePermissionsAsync(first.Name, ["cards:read"], default);
        var file = new AccessTransferDocument("mengtu-admin-access", 1, "permissions", [], null);
        var preview = await fixture.Transfer.CompareAsync(file, default);
        Assert.Contains("First", Assert.Single(preview.Changes).Impact);

        await fixture.Service.SetRolePermissionsAsync(first.Name, [], default);
        await fixture.Service.SetRolePermissionsAsync(second.Name, ["cards:read"], default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Transfer.ApplyAsync(
            new(file, preview.Baseline, ["cards:read"]), default));
        Assert.NotNull(await fixture.Db.Permissions.FindAsync("cards:read"));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly SqliteConnection connection;
        private readonly IServiceScope scope;

        private Fixture(ServiceProvider provider, SqliteConnection connection, IServiceScope scope)
        {
            this.provider = provider;
            this.connection = connection;
            this.scope = scope;
            Db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
            Service = scope.ServiceProvider.GetRequiredService<AdminAccessManagementService>();
            Transfer = scope.ServiceProvider.GetRequiredService<AdminAccessTransferService>();
        }

        public AdminDbContext Db { get; }
        public AdminAccessManagementService Service { get; }
        public AdminAccessTransferService Transfer { get; }
        public IServiceScope Scope => scope;

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AdminDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<AdminUser>().AddRoles<AdminRole>().AddEntityFrameworkStores<AdminDbContext>();
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<ICurrentActorAccessor>(new TestActor());
            services.AddSingleton<IRolePermissionCatalog>(new TestCatalog());
            services.AddScoped<AdminAccessManagementService>();
            services.AddScoped<AdminAccessTransferService>();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var fixture = new Fixture(provider, connection, scope);
            await fixture.Db.Database.EnsureCreatedAsync();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            scope.Dispose();
            await provider.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class TestActor : ICurrentActorAccessor
    {
        public CurrentActor Current => CurrentActor.System;
    }

    private sealed class TestCatalog : IRolePermissionCatalog
    {
        public IReadOnlyDictionary<string, IReadOnlyList<string>> GetRolePermissions() =>
            new Dictionary<string, IReadOnlyList<string>>();

        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
