using AgentPlatform.Contracts;
using HotelResearch.Contracts;
using OTARoomBuilder.Contracts;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OTARoomBuilder.AI;

public sealed class MafAiRoomBuilder(IAgentService agentService) : IAiRoomBuilder
{
    const string AgentName = "ota-room-builder-ai";
    const string ProcessorVersion = "1.0.0";
    const string PromptVersion = "1.0.0";

    const string JsonSchema = """
        {
          "type":"object","additionalProperties":false,
          "properties":{
            "decisions":{"type":"array","items":{"type":"object","additionalProperties":false,
              "properties":{
                "supplierRoomId":{"type":"integer"},"suitableForCreation":{"type":"boolean"},
                "roomNameChinese":{"type":["string","null"]},"roomNameEnglish":{"type":["string","null"]},"roomNameLocal":{"type":["string","null"]},
                "area":{"type":["string","null"]},"bedType":{"type":["string","null"]},"isNonSmoking":{"type":["boolean","null"]},
                "viewEnglish":{"type":["string","null"]},"facilities":{"type":"array","items":{"type":"string"}},"offersAndServices":{"type":"array","items":{"type":"string"}},
                "maxOccupancy":{"type":["integer","null"]},"recommendedMaxOccupancy":{"type":"integer"},"occupancySource":{"type":"string"},"reasonChinese":{"type":"string"}
              },"required":["supplierRoomId","suitableForCreation","roomNameChinese","roomNameEnglish","roomNameLocal","area","bedType","isNonSmoking","viewEnglish","facilities","offersAndServices","maxOccupancy","recommendedMaxOccupancy","occupancySource","reasonChinese"]
            }}
          },"required":["decisions"]
        }
        """;

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public async Task<IReadOnlyList<AiRoomDecision>> ProcessAsync(
        string mtHotelCode,
        IReadOnlyList<AiRoomCandidate> candidates,
        HotelFactsResult? facts,
        CancellationToken cancellationToken = default)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var request = new AgentRequest
        {
            AgentName = AgentName,
            EnableWebSearch = false,
            Message = BuildPrompt(mtHotelCode, candidates, facts),
            JsonResponseFormat = new AgentJsonResponseFormat
            {
                Name = "ota_room_builder_decisions",
                Description = "Supplier room cleaning and creation decisions.",
                Schema = JsonSchema
            },
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["capability"] = "ota-room-builder-ai",
                ["mtHotelCode"] = mtHotelCode,
                ["processorVersion"] = ProcessorVersion,
                ["promptVersion"] = PromptVersion
            }
        };

        var attempt = 0;
        while (true)
        {
            try
            {
                var response = await agentService.RunAsync(request, cancellationToken);
                var payload = JsonSerializer.Deserialize<Payload>(response.Text, JsonOptions)
                    ?? throw new InvalidOperationException("MAF returned an empty AI room decision payload.");
                return payload.Decisions;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                attempt++;
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Max(1, attempt * attempt))), cancellationToken);
            }
        }
    }

    static string BuildPrompt(
        string mtHotelCode,
        IReadOnlyList<AiRoomCandidate> candidates,
        HotelFactsResult? facts)
        => $$"""
            你是 OTA 房型清洗和创建判断代理，处理酒店 {{mtHotelCode}} 的供应商房型。
            51、32、1 的程序优先级规则已经在另一个支线完成；你不能按该优先级阻断本批房型，必须逐条判断所有输入。
            搜索事实只是证据，不能翻译、编造或补写事实。清洗后名称必须基于供应商原始 CN/EN 名称；去掉餐食、促销、套餐、退款/取消政策和销售描述。
            只有房型本身适合作为独立房型时 suitableForCreation=true。名称中没有明确入住人数时推荐 2，并将 occupancySource 写为“名称推断”；有明确人数则写入 maxOccupancy 并将 occupancySource 写为“供应商事实”或“搜索事实”。
            roomNameChinese、roomNameEnglish、roomNameLocal 只做语言归类和拆分，绝不翻译；没有对应语言就为 null。viewEnglish 只复制英文景观描述。
            reasonChinese 必须使用简体中文。输入 JSON：
            {{JsonSerializer.Serialize(new { candidates, searchFacts = facts }, JsonOptions)}}
            """;

    sealed record Payload(IReadOnlyList<AiRoomDecision> Decisions);
}
