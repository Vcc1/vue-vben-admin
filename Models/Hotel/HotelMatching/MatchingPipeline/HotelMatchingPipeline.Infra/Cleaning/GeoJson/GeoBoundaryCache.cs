using System.Diagnostics;
using NetTopologySuite.IO;

namespace HotelMatchingPipeline.Infra.GeoJson;

sealed record GeoBoundaryCacheData(
    IReadOnlyList<BoundaryNode> Adm0,
    IReadOnlyList<BoundaryNode> Adm1,
    IReadOnlyList<BoundaryNode> Adm2,
    int InvalidFeatureCount);

static class GeoBoundaryCache
{
    const string Magic = "HMGBCACHE";
    const int FormatVersion = 1;
    const int MaximumNodeCount = 1_000_000;

    public static GeoBoundaryCacheData? TryRead(
        string cacheDirectory,
        int boundaryVersion,
        CancellationToken cancellationToken,
        Action<string>? progress,
        Action<string>? warning)
    {
        var path = GetPath(cacheDirectory, boundaryVersion);
        if (!File.Exists(path))
        {
            progress?.Invoke($"GeoJSON boundary cache miss. File={path}");
            return null;
        }

        var clock = Stopwatch.StartNew();
        progress?.Invoke($"GeoJSON boundary cache loading started. File={path}");
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                1 << 20,
                FileOptions.SequentialScan);
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: false);
            if (!string.Equals(reader.ReadString(), Magic, StringComparison.Ordinal))
            {
                throw new InvalidDataException("GeoJSON boundary cache magic value does not match.");
            }

            var formatVersion = reader.ReadInt32();
            if (formatVersion != FormatVersion)
            {
                throw new InvalidDataException(
                    $"GeoJSON boundary cache format does not match. Expected={FormatVersion}, Actual={formatVersion}.");
            }

            var cachedBoundaryVersion = reader.ReadInt32();
            if (cachedBoundaryVersion != boundaryVersion)
            {
                throw new InvalidDataException(
                    $"GeoJSON boundary cache version does not match. Expected={boundaryVersion}, Actual={cachedBoundaryVersion}.");
            }

            var invalidFeatureCount = reader.ReadInt32();
            var wkbReader = new WKBReader();
            var adm0 = ReadNodes(reader, wkbReader, cancellationToken);
            var adm1 = ReadNodes(reader, wkbReader, cancellationToken);
            var adm2 = ReadNodes(reader, wkbReader, cancellationToken);
            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException(
                    $"GeoJSON boundary cache contains unexpected trailing data. RemainingBytes={stream.Length - stream.Position}.");
            }

            progress?.Invoke(
                $"GeoJSON boundary cache loading completed. File={path}, " +
                $"ADM0={adm0.Count}, ADM1={adm1.Count}, ADM2={adm2.Count}, " +
                $"ElapsedMs={clock.ElapsedMilliseconds}");
            return new GeoBoundaryCacheData(adm0, adm1, adm2, invalidFeatureCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warning?.Invoke(
                $"GeoJSON boundary cache loading failed; source files will be rebuilt. File={path}, " +
                $"ExceptionType={ex.GetType().FullName}, ExceptionMessage={Clean(ex.Message)}");
            return null;
        }
    }

    public static void Write(
        string cacheDirectory,
        int boundaryVersion,
        IReadOnlyCollection<BoundaryNode> adm0,
        IReadOnlyCollection<BoundaryNode> adm1,
        IReadOnlyCollection<BoundaryNode> adm2,
        int invalidFeatureCount,
        CancellationToken cancellationToken,
        Action<string>? progress,
        Action<string>? warning)
    {
        var path = GetPath(cacheDirectory, boundaryVersion);
        var temporaryPath = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        var clock = Stopwatch.StartNew();
        progress?.Invoke($"GeoJSON boundary cache writing started. File={path}");
        try
        {
            Directory.CreateDirectory(cacheDirectory);
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       1 << 20,
                       FileOptions.SequentialScan))
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: false))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(boundaryVersion);
                writer.Write(invalidFeatureCount);
                var wkbWriter = new WKBWriter(ByteOrder.LittleEndian, handleSRID: true);
                WriteNodes(writer, wkbWriter, adm0, cancellationToken);
                WriteNodes(writer, wkbWriter, adm1, cancellationToken);
                WriteNodes(writer, wkbWriter, adm2, cancellationToken);
            }

            File.Move(temporaryPath, path, overwrite: true);
            progress?.Invoke(
                $"GeoJSON boundary cache writing completed. File={path}, " +
                $"Bytes={new FileInfo(path).Length}, ElapsedMs={clock.ElapsedMilliseconds}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warning?.Invoke(
                $"GeoJSON boundary cache writing failed; initialization will continue without a new cache. File={path}, " +
                $"ExceptionType={ex.GetType().FullName}, ExceptionMessage={Clean(ex.Message)}");
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch
            {
                // A stale temporary file is harmless and must not fail initialization.
            }
        }
    }

    static List<BoundaryNode> ReadNodes(
        BinaryReader reader,
        WKBReader wkbReader,
        CancellationToken cancellationToken)
    {
        var count = reader.ReadInt32();
        if (count is < 0 or > MaximumNodeCount)
        {
            throw new InvalidDataException($"GeoJSON boundary cache node count is invalid: {count}.");
        }

        var result = new List<BoundaryNode>(count);
        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var level = reader.ReadInt32();
            var code = reader.ReadString();
            var name = reader.ReadString();
            var countryCode = reader.ReadString();
            var parentCode = ReadNullableString(reader);
            var parentParentCode = ReadNullableString(reader);
            var geometryLength = reader.ReadInt32();
            if (geometryLength <= 0 || geometryLength > reader.BaseStream.Length - reader.BaseStream.Position)
            {
                throw new InvalidDataException(
                    $"GeoJSON boundary cache geometry length is invalid. Node={code}, Length={geometryLength}.");
            }

            var geometryBytes = reader.ReadBytes(geometryLength);
            if (geometryBytes.Length != geometryLength)
            {
                throw new EndOfStreamException(
                    $"GeoJSON boundary cache geometry ended early. Node={code}, Expected={geometryLength}, Actual={geometryBytes.Length}.");
            }

            var geometry = wkbReader.Read(geometryBytes);
            if (geometry.SRID == 0)
            {
                geometry.SRID = 4326;
            }

            result.Add(new BoundaryNode(level, code, name, countryCode, geometry, parentCode, parentParentCode));
        }

        return result;
    }

    static void WriteNodes(
        BinaryWriter writer,
        WKBWriter wkbWriter,
        IReadOnlyCollection<BoundaryNode> nodes,
        CancellationToken cancellationToken)
    {
        writer.Write(nodes.Count);
        foreach (var node in nodes.OrderBy(static node => node.Code, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            writer.Write(node.Level);
            writer.Write(node.Code);
            writer.Write(node.Name);
            writer.Write(node.CountryCode);
            WriteNullableString(writer, node.ParentCode);
            WriteNullableString(writer, node.ParentParentCode);
            var geometryBytes = wkbWriter.Write(node.Geometry);
            writer.Write(geometryBytes.Length);
            writer.Write(geometryBytes);
        }
    }

    static string GetPath(string cacheDirectory, int boundaryVersion)
        => Path.Combine(cacheDirectory, $"geoboundary-v{FormatVersion}-{boundaryVersion}.bin");

    static string? ReadNullableString(BinaryReader reader)
        => reader.ReadBoolean() ? reader.ReadString() : null;

    static void WriteNullableString(BinaryWriter writer, string? value)
    {
        writer.Write(value is not null);
        if (value is not null)
        {
            writer.Write(value);
        }
    }

    static string Clean(string value)
        => value.Replace('\r', ' ').Replace('\n', ' ');
}
