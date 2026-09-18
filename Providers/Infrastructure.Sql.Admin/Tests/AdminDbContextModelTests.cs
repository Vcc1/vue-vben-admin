using Microsoft.EntityFrameworkCore;
using Infrastructure.Sql.RoomMapping;
using Xunit;

namespace Infrastructure.Sql.Admin.Tests;

public sealed class AdminDbContextModelTests
{
    private static readonly string[] ExpectedTables =
    [
        "Identity_ApiKeys",
        "Identity_RefreshTokens",
        "Identity_RoleClaims",
        "Identity_Roles",
        "Identity_UserAuditLogs",
        "Identity_UserClaims",
        "Identity_UserLogins",
        "Identity_UserRoles",
        "Identity_Users",
        "Identity_UserTokens",
        "Scheduler_TaskAudits",
        "Scheduler_TaskRuns",
        "Scheduler_Tasks"
    ];

    [Fact]
    public void ModelContainsOnlyExplicitAdminTablesInDboSchema()
    {
        using var context = CreateContext();
        var entityTypes = context.Model.GetEntityTypes().ToArray();
        var tables = entityTypes.Select(x => x.GetTableName()).Distinct().Order().ToArray();

        Assert.Equal(ExpectedTables, tables);
        Assert.All(entityTypes, entityType => Assert.Equal("dbo", entityType.GetSchema()));
        Assert.All(tables, table => Assert.True(
            table!.StartsWith("Identity_", StringComparison.Ordinal)
            || table.StartsWith("Scheduler_", StringComparison.Ordinal),
            $"Unexpected table mapping: {table}"));
    }

    [Fact]
    public void ContextDoesNotParticipateInBusinessDatabaseDiscovery()
    {
        var discoveryAttribute = typeof(AdminDbContext).CustomAttributes.SingleOrDefault(
            x => string.Equals(x.AttributeType.Name, "EFDbServiceAttribute", StringComparison.Ordinal));

        Assert.Null(discoveryAttribute);
    }

    [Fact]
    public void RoomMappingModelDoesNotContainAdminTables()
    {
        var options = new DbContextOptionsBuilder<RoomMappingDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=RoomMappingModelTests;Trusted_Connection=True")
            .Options;
        using var context = new RoomMappingDbContext(options);

        Assert.DoesNotContain(context.Model.GetEntityTypes(), entityType =>
        {
            var table = entityType.GetTableName();
            return table?.StartsWith("Identity_", StringComparison.Ordinal) == true
                || table?.StartsWith("Scheduler_", StringComparison.Ordinal) == true;
        });
    }

    private static AdminDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=AdminDbContextModelTests;Trusted_Connection=True")
            .Options;
        return new AdminDbContext(options);
    }
}
