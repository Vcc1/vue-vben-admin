using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Concurrent;
using HotelMatchingPipeline.Domain.Hotels;
using HotelMatchingPipeline.Infra.GeoJson;
using Xunit;

namespace HotelMatchingPipeline.Infra.Tests;

public sealed class GeoBoundaryResolverTests
{
    [Fact]
    public async Task Resolver_returns_adm2_and_its_parents()
    {
        var directory = Directory.CreateTempSubdirectory("hotel-boundaries-");
        try
        {
            await WriteBoundaryAsync(directory.FullName, 0, "TST", "Test Country", "TST", 0, 0, 10, 10);
            await WriteBoundaryAsync(directory.FullName, 1, "ADM1-1", "Test Province", "TST", 1, 1, 9, 9);
            await WriteBoundaryAsync(directory.FullName, 2, "ADM2-1", "Test City", "TST", 2, 2, 8, 8);

            var loaded = await GeoBoundaryLoader.LoadAsync(directory.FullName, 7);
            using var resolver = loaded.Resolver;
            var region = resolver.Resolve(5, 5);

            Assert.Equal(RegionResolveStatus.Resolved, region.Status);
            Assert.Equal("TST", region.Level1Code);
            Assert.Equal("ADM1-1", region.Level2Code);
            Assert.Equal("ADM2-1", region.Level3Code);
            Assert.Equal(7, region.BoundaryVersion);

            var firstVersion = await GeoBoundaryLoader.ComputeBoundaryVersionAsync(directory.FullName);
            await File.AppendAllTextAsync(Path.Combine(directory.FullName, "geoBoundariesCGAZ_ADM2.geojson"), " ");
            var changedVersion = await GeoBoundaryLoader.ComputeBoundaryVersionAsync(directory.FullName);
            Assert.NotEqual(firstVersion, changedVersion);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Loader_counts_feature_without_properties_as_invalid_without_failing()
    {
        var directory = Directory.CreateTempSubdirectory("hotel-boundaries-invalid-");
        try
        {
            await WriteBoundaryAsync(directory.FullName, 0, "TST", "Test Country", "TST", 0, 0, 10, 10);
            await WriteBoundaryAsync(directory.FullName, 1, "ADM1-1", "Test Province", "TST", 1, 1, 9, 9);
            await WriteBoundaryAsync(directory.FullName, 2, "ADM2-1", "Test City", "TST", 2, 2, 8, 8);

            var path = Path.Combine(directory.FullName, "geoBoundariesCGAZ_ADM2.geojson");
            var document = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            document["features"]!.AsArray().Add(new JsonObject { ["type"] = "Feature" });
            await File.WriteAllTextAsync(path, document.ToJsonString());

            var loaded = await GeoBoundaryLoader.LoadAsync(directory.FullName, 7);
            using var resolver = loaded.Resolver;

            Assert.Equal(1, loaded.Summary.InvalidFeatureCount);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Loader_merges_disconnected_features_with_the_same_boundary_code()
    {
        var directory = Directory.CreateTempSubdirectory("hotel-boundaries-duplicate-");
        try
        {
            await WriteBoundaryAsync(directory.FullName, 0, "TST", "Test Country", "TST", 0, 0, 10, 10);
            await WriteBoundaryAsync(directory.FullName, 1, "ADM1-1", "Test Province", "TST", 1, 1, 9, 9);
            await WriteBoundaryAsync(directory.FullName, 2, "ADM2-1", "Test City", "TST", 2, 2, 8, 8);

            var path = Path.Combine(directory.FullName, "geoBoundariesCGAZ_ADM0.geojson");
            var document = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            document["features"]!.AsArray().Add(JsonSerializer.SerializeToNode(new
            {
                type = "Feature",
                properties = new { shapeGroup = "TST", shapeType = "ADM0", shapeName = "Test Country" },
                geometry = new
                {
                    type = "Polygon",
                    coordinates = new[]
                    {
                        new[]
                        {
                            new[] { 20, 20 }, new[] { 30, 20 }, new[] { 30, 30 },
                            new[] { 20, 30 }, new[] { 20, 20 }
                        }
                    }
                }
            }));
            await File.WriteAllTextAsync(path, document.ToJsonString());

            var loaded = await GeoBoundaryLoader.LoadAsync(directory.FullName, 7);
            using var resolver = loaded.Resolver;
            var region = resolver.Resolve(25, 25);

            Assert.Equal(1, loaded.Summary.Adm0Count);
            Assert.Equal(RegionResolveStatus.Resolved, region.Status);
            Assert.Equal("TST", region.Level1Code);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Loader_links_adm1_by_country_code_without_spatial_containment()
    {
        var directory = Directory.CreateTempSubdirectory("hotel-boundaries-country-link-");
        try
        {
            await WriteBoundaryAsync(directory.FullName, 0, "TST", "Test Country", "TST", 0, 0, 10, 10);
            await WriteBoundaryAsync(directory.FullName, 1, "ADM1-1", "Test Province", "TST", 20, 20, 30, 30);
            await WriteBoundaryAsync(directory.FullName, 2, "ADM2-1", "Test City", "TST", 22, 22, 28, 28);

            var loaded = await GeoBoundaryLoader.LoadAsync(directory.FullName, 7);
            using var resolver = loaded.Resolver;
            var region = resolver.Resolve(25, 25);

            Assert.Equal(RegionResolveStatus.Resolved, region.Status);
            Assert.Equal("TST", region.Level1Code);
            Assert.Equal("Test Country", region.Level1Name);
            Assert.Equal("ADM1-1", region.Level2Code);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Loader_limits_adm2_parent_candidates_to_the_child_country()
    {
        var directory = Directory.CreateTempSubdirectory("hotel-boundaries-country-index-");
        try
        {
            await WriteBoundaryAsync(directory.FullName, 0, "TST", "Test Country", "TST", 0, 0, 10, 10);
            await WriteBoundaryAsync(directory.FullName, 1, "ADM1-1", "Test Province", "TST", 0, 0, 10, 10);
            await AppendBoundaryAsync(directory.FullName, 1, "ADM1-2", "Other Province", "OTH", 0, 0, 10, 10);
            await WriteBoundaryAsync(directory.FullName, 2, "ADM2-1", "Test City", "TST", 2, 2, 8, 8);

            var loaded = await GeoBoundaryLoader.LoadAsync(directory.FullName, 7);
            using var resolver = loaded.Resolver;
            var region = resolver.Resolve(5, 5);

            Assert.Equal(RegionResolveStatus.Resolved, region.Status);
            Assert.Equal("ADM1-1", region.Level2Code);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Loader_falls_back_to_spatial_parent_linking_when_country_code_is_unknown()
    {
        var directory = Directory.CreateTempSubdirectory("hotel-boundaries-country-fallback-");
        try
        {
            await WriteBoundaryAsync(directory.FullName, 0, "TST", "Test Country", "TST", 0, 0, 10, 10);
            await WriteBoundaryAsync(directory.FullName, 1, "ADM1-1", "Test Province", "UNKNOWN", 1, 1, 9, 9);
            await WriteBoundaryAsync(directory.FullName, 2, "ADM2-1", "Test City", "UNKNOWN", 2, 2, 8, 8);

            var loaded = await GeoBoundaryLoader.LoadAsync(directory.FullName, 7);
            using var resolver = loaded.Resolver;
            var region = resolver.Resolve(5, 5);

            Assert.Equal(RegionResolveStatus.Resolved, region.Status);
            Assert.Equal("TST", region.Level1Code);
            Assert.Equal("Test Country", region.Level1Name);
            Assert.Equal("ADM1-1", region.Level2Code);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Loader_keeps_disputed_features_out_of_administrative_boundaries()
    {
        var directory = Directory.CreateTempSubdirectory("hotel-boundaries-disputed-");
        try
        {
            await WriteBoundaryAsync(directory.FullName, 0, "TST", "Test Country", "TST", 0, 0, 10, 10);
            await AppendBoundaryAsync(directory.FullName, 0, "DISP-1", "Disputed Area", "999", 20, 20, 30, 30, "DISP");
            await WriteBoundaryAsync(directory.FullName, 1, "ADM1-1", "Test Province", "TST", 1, 1, 9, 9);
            await WriteBoundaryAsync(directory.FullName, 2, "ADM2-1", "Test City", "TST", 2, 2, 8, 8);

            var loaded = await GeoBoundaryLoader.LoadAsync(directory.FullName, 7);
            using var resolver = loaded.Resolver;

            Assert.Equal(1, loaded.Summary.Adm0Count);
            Assert.Equal(1, loaded.Summary.InvalidFeatureCount);
            Assert.Equal(RegionResolveStatus.NotFound, resolver.Resolve(25, 25).Status);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Loader_reads_versioned_cache_and_rebuilds_a_corrupt_cache()
    {
        var directory = Directory.CreateTempSubdirectory("hotel-boundaries-cache-source-");
        var cacheDirectory = Directory.CreateTempSubdirectory("hotel-boundaries-cache-");
        try
        {
            await WriteBoundaryAsync(directory.FullName, 0, "TST", "Test Country", "TST", 0, 0, 10, 10);
            await WriteBoundaryAsync(directory.FullName, 1, "ADM1-1", "Test Province", "TST", 1, 1, 9, 9);
            await WriteBoundaryAsync(directory.FullName, 2, "ADM2-1", "Test City", "TST", 2, 2, 8, 8);

            using (var first = (await GeoBoundaryLoader.LoadAsync(
                       directory.FullName,
                       7,
                       cacheDirectory: cacheDirectory.FullName)).Resolver)
            {
                Assert.Equal("ADM2-1", first.Resolve(5, 5).Level3Code);
            }

            var cacheFile = Assert.Single(cacheDirectory.EnumerateFiles("*.bin"));
            var cacheHitMessages = new ConcurrentQueue<string>();
            using (var cached = (await GeoBoundaryLoader.LoadAsync(
                       directory.FullName,
                       7,
                       1024L * 1024 * 1024,
                       progress: cacheHitMessages.Enqueue,
                       warning: cacheHitMessages.Enqueue,
                       cacheDirectory: cacheDirectory.FullName)).Resolver)
            {
                Assert.Equal("ADM2-1", cached.Resolve(5, 5).Level3Code);
            }

            Assert.Contains(cacheHitMessages, message => message.Contains("cache loading completed", StringComparison.OrdinalIgnoreCase));
            await File.WriteAllTextAsync(cacheFile.FullName, "corrupt cache");
            var messages = new ConcurrentQueue<string>();
            using (var second = (await GeoBoundaryLoader.LoadAsync(
                       directory.FullName,
                       7,
                       1024L * 1024 * 1024,
                       progress: messages.Enqueue,
                       warning: messages.Enqueue,
                       cacheDirectory: cacheDirectory.FullName)).Resolver)
            {
                Assert.Equal("ADM2-1", second.Resolve(5, 5).Level3Code);
            }

            Assert.Contains(messages, message => message.Contains("cache loading failed", StringComparison.OrdinalIgnoreCase));
            Assert.True(new FileInfo(cacheFile.FullName).Length > "corrupt cache".Length);
        }
        finally
        {
            directory.Delete(recursive: true);
            cacheDirectory.Delete(recursive: true);
        }
    }

    static async Task WriteBoundaryAsync(
        string directory,
        int level,
        string code,
        string name,
        string group,
        double minX,
        double minY,
        double maxX,
        double maxY)
    {
        var properties = level == 0
            ? new Dictionary<string, object?> { ["shapeGroup"] = group, ["shapeType"] = "ADM0", ["shapeName"] = name }
            : new Dictionary<string, object?> { ["shapeGroup"] = group, ["shapeType"] = $"ADM{level}", ["shapeName"] = name, ["shapeID"] = code };
        var document = new
        {
            type = "FeatureCollection",
            features = new[]
            {
                new
                {
                    type = "Feature",
                    properties,
                    geometry = new
                    {
                        type = "Polygon",
                        coordinates = new[]
                        {
                            new[]
                            {
                                new[] { minX, minY }, new[] { maxX, minY }, new[] { maxX, maxY },
                                new[] { minX, maxY }, new[] { minX, minY }
                            }
                        }
                    }
                }
            }
        };
        var path = Path.Combine(directory, $"geoBoundariesCGAZ_ADM{level}.geojson");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(document));
    }

    static async Task AppendBoundaryAsync(
        string directory,
        int level,
        string code,
        string name,
        string group,
        double minX,
        double minY,
        double maxX,
        double maxY,
        string? shapeType = null)
    {
        var path = Path.Combine(directory, $"geoBoundariesCGAZ_ADM{level}.geojson");
        var document = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        var points = new[]
        {
            new[] { minX, minY },
            new[] { maxX, minY },
            new[] { maxX, maxY },
            new[] { minX, maxY },
            new[] { minX, minY }
        };
        document["features"]!.AsArray().Add(JsonSerializer.SerializeToNode(new
        {
            type = "Feature",
            properties = new { shapeName = name, shapeID = code, shapeGroup = group, shapeType = shapeType ?? $"ADM{level}" },
            geometry = new { type = "Polygon", coordinates = new[] { points } }
        }));
        await File.WriteAllTextAsync(path, document.ToJsonString());
    }
}
