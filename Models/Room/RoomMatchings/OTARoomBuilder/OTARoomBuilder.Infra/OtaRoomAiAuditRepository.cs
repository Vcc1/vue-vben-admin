using Infrastructure.Sql.RoomMapping;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.EntityFrameworkCore;
using OTARoomBuilder.Contracts;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Infrastructure.OTARoomBuilder;

public sealed class OtaRoomAiAuditRepository(RoomMappingDbContext dbContext) : IAiRoomAuditRepository
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public async Task<bool> ShouldProcessAsync(
        AiRoomCandidate candidate,
        int? searchResultVersion,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.OtaRoomAiDecisions.FirstOrDefaultAsync(
            item => item.SupId == candidate.SupId
                && item.SHotelCode == candidate.SHotelCode
                && item.SRoomCode == candidate.SRoomCode
                && item.MTHotelCode == candidate.MTHotelCode,
            cancellationToken);
        return existing is null
            || existing.SuitableForCreation
            || !string.Equals(existing.SourceRoomNameChinese, candidate.RoomNameChinese, StringComparison.Ordinal)
            || !string.Equals(existing.SourceRoomNameEnglish, candidate.RoomNameEnglish, StringComparison.Ordinal)
            || existing.SearchResultVersion != searchResultVersion
            || existing.ProcessorVersion != "1.0.0"
            || existing.PromptVersion != "1.0.0";
    }

    public async Task UpsertDecisionAsync(
        AiRoomCandidate candidate,
        AiRoomDecision decision,
        int? searchResultVersion,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.OtaRoomAiDecisions.AsTracking().FirstOrDefaultAsync(
            item => item.SupId == candidate.SupId
                && item.SHotelCode == candidate.SHotelCode
                && item.SRoomCode == candidate.SRoomCode
                && item.MTHotelCode == candidate.MTHotelCode,
            cancellationToken);
        entity ??= new OtaRoomAiDecision
        {
            SupId = candidate.SupId,
            SHotelCode = candidate.SHotelCode,
            SRoomCode = candidate.SRoomCode,
            MTHotelCode = candidate.MTHotelCode
        };
        entity.SourceRoomNameChinese = candidate.RoomNameChinese;
        entity.SourceRoomNameEnglish = candidate.RoomNameEnglish;
        entity.SearchResultVersion = searchResultVersion;
        entity.ProcessorVersion = "1.0.0";
        entity.PromptVersion = "1.0.0";
        entity.SuitableForCreation = decision.SuitableForCreation;
        entity.CleanedRoomNameChinese = decision.RoomNameChinese;
        entity.CleanedRoomNameEnglish = decision.RoomNameEnglish;
        entity.CleanedRoomNameLocal = decision.RoomNameLocal;
        entity.DecisionJson = JsonSerializer.Serialize(decision, JsonOptions);
        entity.ReasonChinese = decision.ReasonChinese;
        entity.UpdatedAt = DateTime.UtcNow;
        if (entity.Id == 0)
        {
            await dbContext.OtaRoomAiDecisions.AddAsync(entity, cancellationToken);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordCreateFailureAsync(
        AiRoomCandidate candidate,
        int attempts,
        string errorMessage,
        int? searchResultVersion,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.OtaRoomAiCreateFailures.AsTracking().FirstOrDefaultAsync(
            item => item.SupId == candidate.SupId
                && item.SHotelCode == candidate.SHotelCode
                && item.SRoomCode == candidate.SRoomCode
                && item.MTHotelCode == candidate.MTHotelCode,
            cancellationToken);
        entity ??= new OtaRoomAiCreateFailure
        {
            SupId = candidate.SupId,
            SHotelCode = candidate.SHotelCode,
            SRoomCode = candidate.SRoomCode,
            MTHotelCode = candidate.MTHotelCode
        };
        entity.Attempts = attempts;
        entity.ErrorMessage = errorMessage[..Math.Min(2000, errorMessage.Length)];
        entity.Status = "open";
        entity.SearchResultVersion = searchResultVersion;
        entity.ProcessorVersion = "1.0.0";
        entity.PromptVersion = "1.0.0";
        entity.UpdatedAt = DateTime.UtcNow;
        if (entity.Id == 0)
        {
            await dbContext.OtaRoomAiCreateFailures.AddAsync(entity, cancellationToken);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkCreateSucceededAsync(
        AiRoomCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.OtaRoomAiCreateFailures.AsTracking().FirstOrDefaultAsync(
            item => item.SupId == candidate.SupId
                && item.SHotelCode == candidate.SHotelCode
                && item.SRoomCode == candidate.SRoomCode
                && item.MTHotelCode == candidate.MTHotelCode,
            cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.Status = "resolved";
        entity.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
