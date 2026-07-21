using HotelMatchingPipeline.App;
using HotelMatchingPipeline.App.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace HotelMatchingPipeline.Infra.Tests;

public sealed class HotelStarLevelNormalizerTests
{
    [Theory]
    [InlineData("3.0", 3.0)]
    [InlineData("3.5", 3.5)]
    [InlineData("4.5", 4.5)]
    [InlineData("3*", 3.0)]
    [InlineData("3EST", 3.0)]
    [InlineData("3 STARS", 3.0)]
    [InlineData("4 ESTRELLAS", 4.0)]
    [InlineData("四星", 4.0)]
    [InlineData("五星", 5.0)]
    [InlineData("三星半", 3.5)]
    [InlineData("three stars", 3.0)]
    [InlineData("three and a half stars", 3.5)]
    [InlineData("three-star", 3.0)]
    [InlineData("3 ESTRELLAS Y MEDIA", 3.5)]
    public void Normalizes_common_star_level_formats(string rawValue, double expected)
    {
        var normalizer = Create();

        var result = normalizer.Normalize(null, rawValue);

        Assert.True(result.Success);
        Assert.Equal((decimal)expected, result.Value);
    }

    [Theory]
    [InlineData("012012")]
    [InlineData("20")]
    [InlineData("8")]
    [InlineData("经济型")]
    public void Rejects_supplier_codes_and_out_of_range_values(string rawValue)
    {
        var normalizer = Create();

        var result = normalizer.Normalize(87, rawValue);

        Assert.False(result.Success);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Supplier_mapping_runs_before_common_parser()
    {
        var options = new HotelStarLevelOptions
        {
            SupplierMappings = new()
            {
                [87] = new Dictionary<string, decimal?> { ["012012"] = 2.5m }
            }
        };
        var normalizer = new HotelStarLevelNormalizer(
            Options.Create(options),
            [new ConfiguredSupplierStarLevelConverter(Options.Create(options))]);

        var result = normalizer.Normalize(87, "012012");

        Assert.True(result.Success);
        Assert.Equal(2.5m, result.Value);
    }

    static HotelStarLevelNormalizer Create()
    {
        var options = Options.Create(new HotelStarLevelOptions());
        return new HotelStarLevelNormalizer(options,
            [new ConfiguredSupplierStarLevelConverter(options)]);
    }
}
