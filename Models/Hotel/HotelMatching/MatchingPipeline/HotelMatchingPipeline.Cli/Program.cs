using Framework.Core.IOC;
using HotelMatchingPipeline.App;
using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Cli;
using HotelMatchingPipeline.Domain.Hotels;
using HotelMatchingPipeline.Infra;
using HotelMatchingPipeline.Infra.GeoJson;
using Infrastructure.Sql.RoomMapping;
using MengTu.Connectmate.Client;
using MengTu.Connectmate.Client.Tool;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using VBase.AppMetrics;
using VBaseFramework.ServiceDefaults;
using HotelResearch.Client;

Environment.SetEnvironmentVariable("TZ", "Asia/Shanghai");
TimeZoneInfo.ClearCachedData();
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var command = args
    .FirstOrDefault(static arg => !arg.StartsWith("-", StringComparison.Ordinal))
    ?.Trim()
    .ToLowerInvariant() ?? "run-all";
var commandArguments = args
    .Where(static arg => !arg.StartsWith("-", StringComparison.Ordinal))
    .Skip(1)
    .ToArray();
if (command == "help" || args.Any(static arg => arg is "--help" or "-h"))
{
    Console.WriteLine("Usage: HotelMatchingPipeline.Cli run-all | clean-once | match-loop | match-debug <SupHotelId>");
    return;
}

if (command is not "run-all" and not "clean-once" and not "match-loop" and not "match-debug")
{
    Console.Error.WriteLine($"Unknown command: {command}");
    Environment.ExitCode = 2;
    return;
}

int? debugSupHotelId = null;
if (command == "match-debug")
{
    if (commandArguments.Length == 0
        || !int.TryParse(commandArguments[0], out var parsedSupHotelId)
        || parsedSupHotelId <= 0)
    {
        Console.Error.WriteLine("Usage: HotelMatchingPipeline.Cli match-debug <SupHotelId>");
        Environment.ExitCode = 2;
        return;
    }

    debugSupHotelId = parsedSupHotelId;
}

var host = Serve.Run(new RunOptions
{
    Args = args,
    ServiceName = "MT-HotelMatchingPipeline"
}, (services, configuration) =>
{
    services.AddConnectmateDatabaseSerializer<ConnectmateFallbackSerializer>();
    services.AddConnectmateRemoteConfiguration(configuration);
    services.AddAppMetrics(configuration, options =>
    {
        options.ServiceName = "HotelMatchingPipeline";
        options.MeterName = "AppMetrics";
        options.AdditionalMeterNames = options.AdditionalMeterNames
            .Concat([HotelCleaningMetrics.ScopeName])
            .Distinct()
            .ToArray();
    });
    services.AddSingleton<HotelCleaningMetrics>();
    services.AddSingleton<HotelMatchingMetrics>();
    services
        .AddHotelMatchingInfrastructure(configuration)
        .AddBusiness([typeof(RoomMappingDbContext).Assembly]);
    if (command == "run-all")
    {
        services.AddHotelMatchingApplication(configuration);
    }
    else if (command == "clean-once")
    {
        services.AddHotelCleaningServices(configuration);
    }
    else
    {
        services.AddHotelMatchingServices(configuration);
    }

    var agentHost = configuration["AgentPlatform:BaseAddress"] ?? "http://127.0.0.1:5217/";
    services.AddHotelFactsClient(client => client.BaseAddress = new Uri(agentHost));

    if (command is "run-all" or "clean-once")
    {
        var cleaningOptions = new HotelCleaningOptions();
        configuration.GetSection(HotelCleaningOptions.SectionName).Bind(cleaningOptions);
        var stopwatch = Stopwatch.StartNew();
        Console.WriteLine(
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff} Info HotelMatchingPipeline.GeoJson " +
            $"GeoJSON initialization started. Directory={cleaningOptions.GeoJsonDirectory}, " +
            $"MemoryBudgetGiB={cleaningOptions.MemoryBudgetGiB}");
        try
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff} Info HotelMatchingPipeline.GeoJson GeoJSON boundary hash calculation started.");
            var boundaryVersion = GeoBoundaryLoader
                .ComputeBoundaryVersionAsync(cleaningOptions.GeoJsonDirectory)
                .GetAwaiter()
                .GetResult();
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff} Info HotelMatchingPipeline.GeoJson GeoJSON boundary hash calculation completed. BoundaryVersion={boundaryVersion}");
            var loaded = GeoBoundaryLoader
                .LoadAsync(
                    cleaningOptions.GeoJsonDirectory,
                    boundaryVersion,
                    cleaningOptions.MemoryBudgetGiB * 1024L * 1024 * 1024,
                    progress: message => Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff} Info HotelMatchingPipeline.GeoJson {message}"),
                    warning: message => Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff} Warn HotelMatchingPipeline.GeoJson {message}"),
                    cacheDirectory: Path.Combine(cleaningOptions.GeoJsonDirectory, "cache", "geoboundary"))
                .GetAwaiter()
                .GetResult();

            configuration[$"{HotelCleaningOptions.SectionName}:RegionFenceVersion"] =
                boundaryVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
            services.AddSingleton<IHotelRegionResolver>(loaded.Resolver);
            Console.WriteLine(
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff} Info HotelMatchingPipeline.GeoJson " +
                $"GeoJSON initialization completed. ElapsedMs={stopwatch.ElapsedMilliseconds}, " +
                $"BoundaryVersion={boundaryVersion}, ADM0={loaded.Summary.Adm0Count}, " +
                $"ADM1={loaded.Summary.Adm1Count}, ADM2={loaded.Summary.Adm2Count}, " +
                $"Invalid={loaded.Summary.InvalidFeatureCount}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff} Error HotelMatchingPipeline.GeoJson " +
                $"GeoJSON initialization failed. ElapsedMs={stopwatch.ElapsedMilliseconds}, " +
                $"Directory={cleaningOptions.GeoJsonDirectory}, Exception={ex}");
            throw;
        }
    }

    services.AddSingleton(new HotelMatchingPipelineCliOptions(command, debugSupHotelId));
    services.AddHostedService<HotelMatchingPipelineWorker>();
});

await host.RunAsync();
