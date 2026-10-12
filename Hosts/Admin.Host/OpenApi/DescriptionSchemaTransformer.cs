using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.ComponentModel;
using System.Reflection;

namespace Admin.Host.OpenApi;

public sealed class DescriptionSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var description = context.JsonPropertyInfo?.AttributeProvider?
            .GetCustomAttributes(typeof(DescriptionAttribute), inherit: true)
            .OfType<DescriptionAttribute>()
            .FirstOrDefault()
            ?.Description;

        description ??= context.JsonTypeInfo.Type
            .GetCustomAttribute<DescriptionAttribute>(inherit: true)
            ?.Description;

        if (!string.IsNullOrWhiteSpace(description))
        {
            schema.Description = description;
        }

        return Task.CompletedTask;
    }
}
