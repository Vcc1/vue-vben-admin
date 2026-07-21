using System.Diagnostics;
using System.Security.Cryptography;
using HotelMatchingPipeline.Domain.Hotels;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Index.Strtree;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HotelMatchingPipeline.Infra.GeoJson;

public sealed record GeoBoundaryLoadSummary(
    int Adm0Count,
    int Adm1Count,
    int Adm2Count,
    int InvalidFeatureCount,
    int BoundaryVersion);

sealed record BoundaryNode(
    int Level,
    string Code,
    string Name,
    string CountryCode,
    Geometry Geometry,
    string? ParentCode = null,
    string? ParentParentCode = null);

public sealed class GeoBoundaryResolver : IHotelRegionResolver, IDisposable
{
    STRtree<BoundaryNode>? _adm0Index;
    STRtree<BoundaryNode>? _adm1Index;
    STRtree<BoundaryNode>? _adm2Index;
    Dictionary<string, BoundaryNode>? _adm0ByCode;
    Dictionary<string, BoundaryNode>? _adm1ByCode;
    Dictionary<string, BoundaryNode>? _adm2ByCode;
    bool _disposed;

    internal GeoBoundaryResolver(
        STRtree<BoundaryNode> adm0Index,
        STRtree<BoundaryNode> adm1Index,
        STRtree<BoundaryNode> adm2Index,
        Dictionary<string, BoundaryNode> adm0ByCode,
        Dictionary<string, BoundaryNode> adm1ByCode,
        Dictionary<string, BoundaryNode> adm2ByCode,
        int boundaryVersion)
    {
        _adm0Index = adm0Index;
        _adm1Index = adm1Index;
        _adm2Index = adm2Index;
        _adm0ByCode = adm0ByCode;
        _adm1ByCode = adm1ByCode;
        _adm2ByCode = adm2ByCode;
        BoundaryVersion = boundaryVersion;
    }

    public int BoundaryVersion { get; }

    public RegionResolution Resolve(decimal longitude, decimal latitude, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var point = NtsGeometryServices.Instance.CreateGeometryFactory(4326).CreatePoint(new Coordinate((double)longitude, (double)latitude));

        var adm2Matches = FindContaining(_adm2Index!, point);
        if (adm2Matches.Count == 1)
        {
            var adm2 = adm2Matches[0];
            var adm1 = adm2.ParentCode is not null && _adm1ByCode!.TryGetValue(adm2.ParentCode, out var adm1Value) ? adm1Value : null;
            var adm0 = adm2.ParentParentCode is not null && _adm0ByCode!.TryGetValue(adm2.ParentParentCode, out var adm0Value) ? adm0Value : null;
            return new RegionResolution(
                adm0?.Code ?? adm2.CountryCode,
                adm0?.Name,
                adm1?.Code,
                adm1?.Name,
                adm2.Code,
                adm2.Name,
                RegionResolveStatus.Resolved,
                BoundaryVersion);
        }

        if (adm2Matches.Count > 1)
        {
            return new RegionResolution(null, null, null, null, null, null, RegionResolveStatus.BoundaryAmbiguous, BoundaryVersion);
        }

        var adm1Matches = FindContaining(_adm1Index!, point);
        if (adm1Matches.Count == 1)
        {
            var adm1 = adm1Matches[0];
            var adm0 = adm1.ParentCode is not null && _adm0ByCode!.TryGetValue(adm1.ParentCode, out var adm0Value) ? adm0Value : null;
            return new RegionResolution(
                adm0?.Code ?? adm1.CountryCode,
                adm0?.Name,
                adm1.Code,
                adm1.Name,
                null,
                null,
                RegionResolveStatus.Resolved,
                BoundaryVersion);
        }

        if (adm1Matches.Count > 1)
        {
            return new RegionResolution(null, null, null, null, null, null, RegionResolveStatus.BoundaryAmbiguous, BoundaryVersion);
        }

        var adm0Matches = FindContaining(_adm0Index!, point);
        if (adm0Matches.Count == 1)
        {
            var adm0 = adm0Matches[0];
            return new RegionResolution(adm0.Code, adm0.Name, null, null, null, null, RegionResolveStatus.Resolved, BoundaryVersion);
        }

        return adm0Matches.Count > 1
            ? new RegionResolution(null, null, null, null, null, null, RegionResolveStatus.BoundaryAmbiguous, BoundaryVersion)
            : RegionResolution.NotFound(BoundaryVersion);
    }

    static List<BoundaryNode> FindContaining(STRtree<BoundaryNode> index, Point point)
        => index.Query(point.EnvelopeInternal)
            .Where(candidate => candidate.Geometry.Covers(point))
            .ToList();

    void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GeoBoundaryResolver));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _adm0Index = null;
        _adm1Index = null;
        _adm2Index = null;
        _adm0ByCode = null;
        _adm1ByCode = null;
        _adm2ByCode = null;
    }
}

public static class GeoBoundaryLoader
{
    public static async Task<int> ComputeBoundaryVersionAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1 << 20];
        foreach (var level in new[] { 0, 1, 2 })
        {
            var path = Path.Combine(directory, $"geoBoundariesCGAZ_ADM{level}.geojson");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("GeoBoundary file was not found.", path);
            }

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, FileOptions.SequentialScan);
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                hash.AppendData(buffer, 0, read);
            }
        }

        var digest = hash.GetHashAndReset();
        var version = BitConverter.ToInt32(digest, 0) & int.MaxValue;
        return version == 0 ? 1 : version;
    }

    public static async Task<(GeoBoundaryResolver Resolver, GeoBoundaryLoadSummary Summary)> LoadAsync(
        string directory,
        int boundaryVersion,
        CancellationToken cancellationToken = default,
        string? cacheDirectory = null)
        => await LoadAsync(directory, boundaryVersion, 10L * 1024 * 1024 * 1024, cancellationToken, null, null, cacheDirectory);

    public static async Task<(GeoBoundaryResolver Resolver, GeoBoundaryLoadSummary Summary)> LoadAsync(
        string directory,
        int boundaryVersion,
        long memoryBudgetBytes,
        CancellationToken cancellationToken = default,
        Action<string>? progress = null,
        Action<string>? warning = null,
        string? cacheDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfLessThan(memoryBudgetBytes, 1);
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(4326);
        var all = new List<BoundaryNode>();
        var invalid = 0;
        var loadClock = Stopwatch.StartNew();
        using var heartbeat = progress is null
            ? null
            : new Timer(
                _ => progress($"GeoJSON initialization is still running. ElapsedSeconds={loadClock.Elapsed.TotalSeconds:F0}"),
                null,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5));

        if (!string.IsNullOrWhiteSpace(cacheDirectory))
        {
            var cached = GeoBoundaryCache.TryRead(
                cacheDirectory,
                boundaryVersion,
                cancellationToken,
                progress,
                warning);
            if (cached is not null)
            {
                var cachedAdm0 = cached.Adm0.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
                var cachedAdm1 = cached.Adm1.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
                var cachedAdm2 = cached.Adm2.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
                EnsureMemoryBudget(memoryBudgetBytes, cacheDirectory);
                return CreateLoaded(
                    cachedAdm0,
                    cachedAdm1,
                    cachedAdm2,
                    cached.InvalidFeatureCount,
                    boundaryVersion,
                    progress);
            }
        }

        foreach (var level in new[] { 0, 1, 2 })
        {
            var path = Path.Combine(directory, $"geoBoundariesCGAZ_ADM{level}.geojson");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("GeoBoundary file was not found.", path);
            }

            progress?.Invoke($"ADM{level} boundary loading started. File={path}");
            var features = await ReadFeaturesAsync(path, factory, memoryBudgetBytes, cancellationToken, progress, warning);
            all.AddRange(features.Nodes);
            invalid += features.InvalidFeatureCount;
            EnsureMemoryBudget(memoryBudgetBytes, path);
            var reasons = features.InvalidReasons.Count == 0
                ? "none"
                : string.Join(", ", features.InvalidReasons.Select(x => $"{x.Key}={x.Value}"));
            progress?.Invoke($"ADM{level} boundary loading completed. Features={features.Nodes.Count}, Invalid={features.InvalidFeatureCount}, Reasons={reasons}");
        }

        progress?.Invoke("Merging boundary geometries started.");
        var adm0 = MergeByCode(all.Where(x => x.Level == 0), "ADM0", progress);
        var adm1 = MergeByCode(all.Where(x => x.Level == 1), "ADM1", progress);
        var adm2 = MergeByCode(all.Where(x => x.Level == 2), "ADM2", progress);

        var adm1Nodes = LinkAdm1Parents(adm1.Values, adm0.Values, cancellationToken, progress);
        var adm2Nodes = LinkAdm2Parents(adm2.Values, adm1Nodes, cancellationToken, progress);
        adm1 = adm1Nodes.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        adm2 = adm2Nodes.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

        var loaded = CreateLoaded(adm0, adm1, adm2, invalid, boundaryVersion, progress);
        if (!string.IsNullOrWhiteSpace(cacheDirectory))
        {
            GeoBoundaryCache.Write(
                cacheDirectory,
                boundaryVersion,
                adm0.Values.ToArray(),
                adm1.Values.ToArray(),
                adm2.Values.ToArray(),
                invalid,
                cancellationToken,
                progress,
                warning);
        }

        return loaded;
    }

    static (GeoBoundaryResolver Resolver, GeoBoundaryLoadSummary Summary) CreateLoaded(
        Dictionary<string, BoundaryNode> adm0,
        Dictionary<string, BoundaryNode> adm1,
        Dictionary<string, BoundaryNode> adm2,
        int invalid,
        int boundaryVersion,
        Action<string>? progress)
    {
        var adm0Index = BuildIndex(adm0.Values);
        var adm1Index = BuildIndex(adm1.Values);
        var adm2Index = BuildIndex(adm2.Values);
        progress?.Invoke("Boundary spatial index construction started.");
        adm0Index.Build();
        adm1Index.Build();
        adm2Index.Build();
        progress?.Invoke("Boundary spatial index construction completed.");

        var resolver = new GeoBoundaryResolver(adm0Index, adm1Index, adm2Index, adm0, adm1, adm2, boundaryVersion);
        return (resolver, new GeoBoundaryLoadSummary(adm0.Count, adm1.Count, adm2.Count, invalid, boundaryVersion));
    }

    static Dictionary<string, BoundaryNode> MergeByCode(
        IEnumerable<BoundaryNode> nodes,
        string levelName,
        Action<string>? progress)
    {
        var nodeList = nodes.ToArray();
        var result = new Dictionary<string, BoundaryNode>(StringComparer.OrdinalIgnoreCase);
        var clock = Stopwatch.StartNew();
        var lastProgress = TimeSpan.Zero;
        for (var index = 0; index < nodeList.Length; index++)
        {
            var node = nodeList[index];
            if (result.TryGetValue(node.Code, out var existing))
            {
                result[node.Code] = existing with { Geometry = existing.Geometry.Union(node.Geometry) };
            }
            else
            {
                result.Add(node.Code, node);
            }

            if (progress is not null && clock.Elapsed - lastProgress >= TimeSpan.FromSeconds(5))
            {
                lastProgress = clock.Elapsed;
                progress($"{levelName} geometry merge progress. Processed={index + 1}, Total={nodeList.Length}, Percent={(index + 1) * 100d / nodeList.Length:F1}");
            }
        }

        progress?.Invoke($"{levelName} geometry merge completed. Processed={nodeList.Length}, Unique={result.Count}");
        return result;
    }

    static void EnsureMemoryBudget(long memoryBudgetBytes, string path)
    {
        var privateBytes = Process.GetCurrentProcess().PrivateMemorySize64;
        if (privateBytes > memoryBudgetBytes)
        {
            throw new InvalidOperationException(
                $"GeoBoundary loading exceeded the configured memory budget. File={path}, PrivateBytes={privateBytes}, BudgetBytes={memoryBudgetBytes}.");
        }
    }

    static STRtree<BoundaryNode> BuildIndex(IEnumerable<BoundaryNode> nodes)
    {
        var index = new STRtree<BoundaryNode>();
        foreach (var node in nodes)
        {
            index.Insert(node.Geometry.EnvelopeInternal, node);
        }

        return index;
    }

    static IReadOnlyList<BoundaryNode> LinkAdm1Parents(
        IEnumerable<BoundaryNode> children,
        IEnumerable<BoundaryNode> parents,
        CancellationToken cancellationToken,
        Action<string>? progress)
    {
        var childList = children.ToArray();
        var parentByCode = parents.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var fallbackLookup = BuildParentLookup(parentByCode.Values);
        var result = new BoundaryNode[childList.Length];
        var degree = GetParentLinkDegree();
        progress?.Invoke($"ADM1 parent linking started. Children={childList.Length}, Parallelism={degree}, Mode=CountryCodeDictionary");

        RunParentLinking(
            childList,
            result,
            1,
            degree,
            fallbackLookup,
            cancellationToken,
            progress,
            (child, _) =>
            {
                if (parentByCode.TryGetValue(child.CountryCode, out var directParent))
                {
                    return directParent;
                }

                return FindUniqueParent(fallbackLookup, child.Geometry.InteriorPoint, fallbackLookup.GlobalIndex);
            },
            static (child, parent) => child with { ParentCode = parent?.Code });

        progress?.Invoke($"ADM1 parent linking completed. Processed={childList.Length}");
        return result;
    }

    static IReadOnlyList<BoundaryNode> LinkAdm2Parents(
        IEnumerable<BoundaryNode> children,
        IEnumerable<BoundaryNode> parents,
        CancellationToken cancellationToken,
        Action<string>? progress)
    {
        var childList = children.ToArray();
        var parentLookup = BuildParentLookup(parents);
        var result = new BoundaryNode[childList.Length];
        var degree = GetParentLinkDegree();
        progress?.Invoke($"ADM2 parent linking started. Children={childList.Length}, Parallelism={degree}, Mode=CountryCodeSpatialIndex");

        RunParentLinking(
            childList,
            result,
            2,
            degree,
            parentLookup,
            cancellationToken,
            progress,
            (child, lookup) =>
            {
                var countryLookup = lookup.ByCountry.TryGetValue(child.CountryCode, out var groupedLookup)
                    ? groupedLookup
                    : lookup.GlobalIndex;
                return FindUniqueParent(lookup, child.Geometry.InteriorPoint, countryLookup);
            },
            static (child, parent) => child with
            {
                ParentCode = parent?.Code,
                ParentParentCode = parent?.ParentCode
            });

        progress?.Invoke($"ADM2 parent linking completed. Processed={childList.Length}");
        return result;
    }

    static void RunParentLinking(
        IReadOnlyList<BoundaryNode> children,
        BoundaryNode[] result,
        int childLevel,
        int degree,
        ParentLookup lookup,
        CancellationToken cancellationToken,
        Action<string>? progress,
        Func<BoundaryNode, ParentLookup, BoundaryNode?> findParent,
        Func<BoundaryNode, BoundaryNode?, BoundaryNode> attachParent)
    {
        var processed = 0;
        using var timer = progress is null
            ? null
            : new Timer(
                _ =>
                {
                    var count = Volatile.Read(ref processed);
                    var percent = children.Count == 0 ? 100d : count * 100d / children.Count;
                    progress($"ADM{childLevel} parent linking progress. Processed={count}, Total={children.Count}, Percent={percent:F1}");
                },
                null,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5));

        Parallel.For(
            0,
            children.Count,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = degree
            },
            index =>
            {
                var child = children[index];
                var parent = findParent(child, lookup);
                result[index] = attachParent(child, parent);
                Interlocked.Increment(ref processed);
            });
    }

    static ParentLookup BuildParentLookup(IEnumerable<BoundaryNode> parents)
    {
        var parentList = parents.ToArray();
        var preparedByCode = new Dictionary<string, IPreparedGeometry>(StringComparer.OrdinalIgnoreCase);
        foreach (var parent in parentList)
        {
            preparedByCode[parent.Code] = PreparedGeometryFactory.Prepare(parent.Geometry);
        }

        var globalIndex = BuildIndex(parentList);
        globalIndex.Build();
        var byCountry = new Dictionary<string, STRtree<BoundaryNode>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in parentList.GroupBy(x => x.CountryCode, StringComparer.OrdinalIgnoreCase))
        {
            var index = BuildIndex(group);
            index.Build();
            byCountry[group.Key] = index;
        }

        return new ParentLookup(globalIndex, byCountry, preparedByCode);
    }

    static BoundaryNode? FindUniqueParent(
        ParentLookup lookup,
        Point representativePoint,
        STRtree<BoundaryNode> index)
    {
        BoundaryNode? match = null;
        foreach (var candidate in index.Query(representativePoint.EnvelopeInternal))
        {
            if (!lookup.PreparedByCode[candidate.Code].Covers(representativePoint))
            {
                continue;
            }

            if (match is not null)
            {
                return null;
            }

            match = candidate;
        }

        return match;
    }

    static int GetParentLinkDegree()
        => Math.Max(1, Environment.ProcessorCount - 1);

    sealed record ParentLookup(
        STRtree<BoundaryNode> GlobalIndex,
        Dictionary<string, STRtree<BoundaryNode>> ByCountry,
        Dictionary<string, IPreparedGeometry> PreparedByCode);

    static async Task<(
        IReadOnlyList<BoundaryNode> Nodes,
        int InvalidFeatureCount,
        IReadOnlyDictionary<string, int> InvalidReasons)> ReadFeaturesAsync(
        string path,
        GeometryFactory factory,
        long memoryBudgetBytes,
        CancellationToken cancellationToken,
        Action<string>? progress,
        Action<string>? warning)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
        using var textReader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true, bufferSize: 1 << 20, leaveOpen: true);
        using var jsonReader = new JsonTextReader(textReader)
        {
            CloseInput = false,
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double,
            MaxDepth = 128
        };
        var result = new List<BoundaryNode>();
        var invalid = 0;
        var invalidReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        var featureIndex = 0;
        void AddInvalid(
            string reason,
            JObject? properties = null,
            string? geometryType = null,
            Exception? exception = null)
        {
            invalid++;
            invalidReasons[reason] = invalidReasons.GetValueOrDefault(reason) + 1;
            var exceptionMessage = exception?.Message.Replace('\r', ' ').Replace('\n', ' ');
            warning?.Invoke(
                $"Invalid GeoJSON feature skipped. File={Path.GetFileName(path)}, FeatureIndex={featureIndex}, " +
                $"Reason={reason}, ShapeGroup={properties?.Value<string>("shapeGroup") ?? "<null>"}, " +
                $"ShapeId={properties?.Value<string>("shapeID") ?? "<null>"}, " +
                $"ShapeName={properties?.Value<string>("shapeName") ?? "<null>"}, " +
                $"ShapeType={properties?.Value<string>("shapeType") ?? "<null>"}, " +
                $"GeometryType={geometryType ?? "<null>"}, " +
                $"ExceptionType={exception?.GetType().FullName ?? "<none>"}, " +
                $"ExceptionMessage={exceptionMessage ?? "<none>"}");
        }

        var foundFeatures = false;
        var progressClock = Stopwatch.StartNew();
        var lastProgress = TimeSpan.Zero;
        while (await jsonReader.ReadAsync(cancellationToken))
        {
            if (jsonReader.TokenType != JsonToken.PropertyName
                || !string.Equals((string?)jsonReader.Value, "features", StringComparison.Ordinal))
            {
                continue;
            }

            if (!await jsonReader.ReadAsync(cancellationToken) || jsonReader.TokenType != JsonToken.StartArray)
            {
                throw new InvalidDataException($"GeoJSON features value is not an array: {path}");
            }

            foundFeatures = true;
            while (await jsonReader.ReadAsync(cancellationToken) && jsonReader.TokenType != JsonToken.EndArray)
            {
                cancellationToken.ThrowIfCancellationRequested();
                featureIndex++;
                if (jsonReader.TokenType != JsonToken.StartObject)
                {
                    AddInvalid("feature_not_object");
                    continue;
                }

                JObject? properties = null;
                string? geometryType = null;
                try
                {
                    var feature = await JObject.LoadAsync(jsonReader, cancellationToken);
                    properties = feature["properties"] as JObject;
                    geometryType = feature["geometry"]?.Value<string>("type");
                    if (properties is null)
                    {
                        AddInvalid("properties_missing_or_invalid", properties, geometryType);
                        continue;
                    }

                    var shapeType = properties.Value<string>("shapeType") ?? string.Empty;
                    if (!TryParseLevel(shapeType, out var level))
                    {
                        AddInvalid("unsupported_shape_type", properties, geometryType);
                        continue;
                    }
                    var countryCode = properties.Value<string>("shapeGroup") ?? string.Empty;
                    var code = level == 0 ? countryCode : properties.Value<string>("shapeID") ?? string.Empty;
                    var name = properties.Value<string>("shapeName") ?? code;
                    var geometry = ReadGeometry(feature["geometry"], factory);
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        AddInvalid("boundary_code_missing", properties, geometryType);
                        continue;
                    }
                    if (geometry is null)
                    {
                        AddInvalid("geometry_missing_or_unsupported", properties, geometryType);
                        continue;
                    }
                    if (geometry.IsEmpty)
                    {
                        AddInvalid("geometry_empty", properties, geometryType);
                        continue;
                    }

                    if (!geometry.IsValid)
                    {
                        geometry = geometry.Buffer(0);
                    }

                    result.Add(new BoundaryNode(level, code, name, countryCode, geometry));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    AddInvalid("feature_parse_error", properties, geometryType, ex);
                }

                if (progress is not null && progressClock.Elapsed - lastProgress >= TimeSpan.FromSeconds(5))
                {
                    lastProgress = progressClock.Elapsed;
                    progress($"Reading {Path.GetFileName(path)}. Features={result.Count}, Invalid={invalid}, ElapsedSeconds={lastProgress.TotalSeconds:F0}");
                }

                if ((result.Count + invalid) % 250 == 0)
                {
                    EnsureMemoryBudget(memoryBudgetBytes, path);
                }
            }

            break;
        }

        if (!foundFeatures)
        {
            throw new InvalidDataException($"GeoJSON FeatureCollection is missing features: {path}");
        }

        return (result, invalid, invalidReasons);
    }

    static bool TryParseLevel(string shapeType, out int level)
    {
        if (shapeType.EndsWith("ADM0", StringComparison.OrdinalIgnoreCase))
        {
            level = 0;
            return true;
        }

        if (shapeType.EndsWith("ADM1", StringComparison.OrdinalIgnoreCase))
        {
            level = 1;
            return true;
        }

        if (shapeType.EndsWith("ADM2", StringComparison.OrdinalIgnoreCase))
        {
            level = 2;
            return true;
        }

        level = default;
        return false;
    }

    static Geometry? ReadGeometry(JToken? geometryElement, GeometryFactory factory)
    {
        if (geometryElement is null || geometryElement.Type == JTokenType.Null)
        {
            return null;
        }

        var type = geometryElement.Value<string>("type");
        var coordinates = geometryElement["coordinates"] as JArray;
        if (coordinates is null)
        {
            return null;
        }

        return type switch
        {
            "Polygon" => ReadPolygon(coordinates, factory),
            "MultiPolygon" => ReadMultiPolygon(coordinates, factory),
            _ => null
        };
    }

    static Polygon ReadPolygon(JArray coordinates, GeometryFactory factory)
    {
        var rings = coordinates.Children<JArray>().Select(ring => factory.CreateLinearRing(ReadCoordinates(ring))).ToArray();
        if (rings.Length == 0)
        {
            return factory.CreatePolygon();
        }

        return factory.CreatePolygon(rings[0], rings.Skip(1).ToArray());
    }

    static MultiPolygon ReadMultiPolygon(JArray coordinates, GeometryFactory factory)
        => factory.CreateMultiPolygon(coordinates.Children<JArray>().Select(x => ReadPolygon(x, factory)).ToArray());

    static Coordinate[] ReadCoordinates(JArray ring)
        => ring.Children<JArray>()
            .Where(item => item.Count >= 2)
            .Select(item => new Coordinate(item[0]!.Value<double>(), item[1]!.Value<double>()))
            .ToArray();
}
