using HotelBusiness.App;
using HotelBusiness.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using Xunit;

namespace HotelBusiness.Infra.Tests;

public sealed class FileHotelBusinessStoreTests : IDisposable
{
    readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "MengtuV-HotelBusiness-Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SuccessfulVersionsAreSequential_AndFailedRunDoesNotConsumeVersion()
    {
        var store = CreateStore();
        var first = await store.StartRunAsync(CreateStart("MT-000001"));
        var firstResult = await store.CompleteRunAsync(
            first,
            CreateProcessor(),
            (version, _) => new TestResult { ResultVersion = version, Value = "first" });

        var failed = await store.StartRunAsync(CreateStart("MT-000001"));
        await store.FinishRunWithoutResultAsync(
            failed,
            HotelOperationRunStatus.Failed,
            new InvalidOperationException("upstream failed"));

        var second = await store.StartRunAsync(CreateStart("MT-000001"));
        var secondResult = await store.CompleteRunAsync(
            second,
            CreateProcessor(),
            (version, _) => new TestResult { ResultVersion = version, Value = "second" });

        Assert.Equal(1, firstResult.ResultVersion);
        Assert.Equal(2, secondResult.ResultVersion);

        var detail = await store.GetAsync("MT-000001");
        var operation = Assert.Single(detail!.Operations);
        Assert.Equal(2, operation.LatestSuccessfulVersion);
        Assert.Equal(2, operation.Versions.Count);
        Assert.Equal(3, operation.Runs.Count);
        Assert.Null(operation.Runs.Single(run => run.RunId == failed.RunId).ResultVersion);

        var latest = await store.GetResultAsync("MT-000001", "hotel-research", null);
        var versionOne = await store.GetResultAsync("MT-000001", "hotel-research", 1);
        Assert.Equal("second", latest!.Value.GetProperty("value").GetString());
        Assert.Equal("first", versionOne!.Value.GetProperty("value").GetString());

        var operationPath = Path.Combine(
            _root,
            "hotels",
            "MT-000001",
            "operations",
            "hotel-research");
        Assert.True(File.Exists(Path.Combine(operationPath, "results", "v000001.json")));
        Assert.True(File.Exists(Path.Combine(operationPath, "results", "v000002.json")));
        Assert.True(File.Exists(Path.Combine(operationPath, "runs", failed.RunId, "run.json")));
        Assert.Contains("\"fileSchemaVersion\": 1", await File.ReadAllTextAsync(Path.Combine(_root, "recent.json")));
        var recent = Assert.Single(await store.GetRecentAsync());
        Assert.Equal("English MT-000001", recent.HotelNameEnglish);
        Assert.Equal(HotelBusinessOperations.HotelResearch, Assert.Single(recent.Operations).OperationType);
    }

    [Fact]
    public async Task RecentHotelsIsLimitedToTwentyMostRecentItems()
    {
        var store = CreateStore();
        for (var index = 0; index < 21; index++)
        {
            var code = $"MT-{index:000000}";
            var run = await store.StartRunAsync(CreateStart(code));
            await store.FinishRunWithoutResultAsync(
                run,
                HotelOperationRunStatus.Cancelled,
                null);
        }

        var recent = await store.GetRecentAsync();

        Assert.Equal(20, recent.Count);
        Assert.Equal("MT-000020", recent[0].MTHotelCode);
        Assert.DoesNotContain(recent, item => item.MTHotelCode == "MT-000000");
    }

    [Fact]
    public void ConcurrencyGuard_BlocksSameHotelOperation_ButAllowsDifferentHotels()
    {
        var guard = new HotelOperationConcurrencyGuard();
        using var first = guard.TryAcquire("MT-000001", "hotel-research");
        using var otherHotel = guard.TryAcquire("MT-000002", "hotel-research");

        Assert.NotNull(first);
        Assert.NotNull(otherHotel);
        Assert.Null(guard.TryAcquire("MT-000001", "hotel-research"));

        first.Dispose();
        using var reacquired = guard.TryAcquire("MT-000001", "hotel-research");
        Assert.NotNull(reacquired);
    }

    IHotelBusinessStore CreateStore()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HotelBusiness:Storage:RootPath"] = _root
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(_root));
        services.AddHotelBusinessInfrastructure(configuration);
        return services.BuildServiceProvider().GetRequiredService<IHotelBusinessStore>();
    }

    static HotelOperationRunStart CreateStart(string code)
        => new()
        {
            MTHotelCode = code,
            HotelName = $"Hotel {code}",
            HotelNameEnglish = $"English {code}",
            OperationType = HotelBusinessOperations.HotelResearch,
            Processor = CreateProcessor()
        };

    static HotelProcessorDescriptor CreateProcessor()
        => new()
        {
            Name = "hotel-research",
            ProcessorVersion = "1.0.0",
            PromptVersion = "1.0.0",
            ResultSchemaVersion = 1,
            AgentName = "hotel-research",
            Provider = "openai-responses",
            Model = "gpt-test",
            WebSearchEnabled = true
        };

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    sealed record TestResult
    {
        public int FileSchemaVersion { get; init; } = 1;

        public required int ResultVersion { get; init; }

        public required string Value { get; init; }
    }

    sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "HotelBusiness.Infra.Tests";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
