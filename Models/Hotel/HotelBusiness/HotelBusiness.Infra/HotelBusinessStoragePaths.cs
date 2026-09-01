using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HotelBusiness.Infra;

internal sealed class HotelBusinessStoragePaths
{
    public HotelBusinessStoragePaths(
        IHostEnvironment environment,
        IOptions<HotelBusinessStorageOptions> options)
    {
        var configuredPath = options.Value.RootPath;
        RootPath = Path.GetFullPath(
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath));
    }

    public string RootPath { get; }

    public string RecentFile => Path.Combine(RootPath, "recent.json");

    public string GetHotelDirectory(string mtHotelCode)
        => UnderRoot(Path.Combine(RootPath, "hotels", mtHotelCode));

    public string GetHotelFile(string mtHotelCode)
        => Path.Combine(GetHotelDirectory(mtHotelCode), "hotel.json");

    public string GetOperationDirectory(string mtHotelCode, string operationType)
        => UnderRoot(Path.Combine(GetHotelDirectory(mtHotelCode), "operations", operationType));

    public string GetOperationFile(string mtHotelCode, string operationType)
        => Path.Combine(GetOperationDirectory(mtHotelCode, operationType), "operation.json");

    public string GetResultFile(string mtHotelCode, string operationType, int resultVersion)
        => Path.Combine(
            GetOperationDirectory(mtHotelCode, operationType),
            "results",
            $"v{resultVersion:000000}.json");

    public string GetRunDirectory(string mtHotelCode, string operationType, string runId)
        => UnderRoot(Path.Combine(
            GetOperationDirectory(mtHotelCode, operationType),
            "runs",
            runId));

    public string GetRunFile(string mtHotelCode, string operationType, string runId)
        => Path.Combine(GetRunDirectory(mtHotelCode, operationType, runId), "run.json");

    public string GetConversationFile(string mtHotelCode, string operationType, string runId)
        => Path.Combine(GetRunDirectory(mtHotelCode, operationType, runId), "conversation.json");

    string UnderRoot(string candidate)
    {
        var path = Path.GetFullPath(candidate);
        var prefix = RootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Hotel business path resolves outside the storage root.");
        }

        return path;
    }
}
