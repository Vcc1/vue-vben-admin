using Microsoft.Extensions.DependencyInjection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentPlatform.Api;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddAgentPlatformApi(this IMvcBuilder builder)
        => builder
            .AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly)
            .AddJsonOptions(options =>
            {
                var json = options.JsonSerializerOptions;
                json.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                json.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                json.PropertyNameCaseInsensitive = true;
                json.WriteIndented = true;
                json.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            });
}
