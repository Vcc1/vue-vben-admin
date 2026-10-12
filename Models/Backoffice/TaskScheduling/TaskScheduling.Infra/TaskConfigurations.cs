using System.Text.Json;

namespace TaskScheduling.Infra;

internal sealed record ProcessTaskConfiguration(
    string Shell,
    string Executable,
    string[] Arguments,
    string WorkingDirectory,
    Dictionary<string, string>? Environment);

internal sealed record SqlTaskConfiguration(
    string DatabaseKey,
    string CommandText,
    Dictionary<string, JsonElement>? Parameters,
    int? CommandTimeoutSeconds);

internal sealed record ProgramTaskConfiguration(
    string HandlerKey,
    JsonElement Parameters);

internal sealed record TaskExecutionResult(string Summary);
