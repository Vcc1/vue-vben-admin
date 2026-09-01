using AgentPlatform.Extensibility;
using HotelMatchingPipeline.App;
using HotelMatchingPipeline.App.Models;
using Microsoft.Extensions.AI;

namespace HotelMatchingPipeline.AgentTools;

internal sealed class HotelMatchingToolContributor(
    HotelDebugService hotelDebugService) : IAgentToolContributor
{
    public bool Supports(string agentName)
        => agentName is "operations" or "hotel-matching";

    public IReadOnlyList<AITool> CreateTools()
        =>
        [
            AIFunctionFactory.Create(
                AnalyzeHotelMatchingAsync,
                "analyze_hotel_matching",
                "查询并分析一个 OTA 或供应商酒店的匹配数据。sourceType 使用 ota 或 supplier；supplier 类型必须提供 supId。")
        ];

    async Task<HotelDebugAnalysis> AnalyzeHotelMatchingAsync(
        string sourceType,
        string hotelCode,
        int? supId,
        CancellationToken cancellationToken)
    {
        var parsedSourceType = sourceType.Trim().ToLowerInvariant() switch
        {
            "ota" => HotelDebugSourceType.Ota,
            "supplier" => HotelDebugSourceType.Supplier,
            _ => throw new ArgumentException("sourceType must be ota or supplier.", nameof(sourceType))
        };

        return await hotelDebugService.AnalyzeAsync(
            new HotelDebugRequest(parsedSourceType, hotelCode, supId),
            cancellationToken);
    }
}
