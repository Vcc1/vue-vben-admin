using Infrastructure.Sql.MengTuSup.Entitys;
using Infrastructure.Sql.RoomMapping;
using Microsoft.EntityFrameworkCore;
using OTADomain.Matchings;
using RoomMatch.Admin.Contracts;

namespace RoomMatch.Admin.Infra;

public sealed class WordRepository : IWordRepository
{
    private readonly RoomMappingDbContext _db;

    public WordRepository(RoomMappingDbContext db)
    {
        _db = db;
    }

    public async Task<List<WordDto>> GetWordsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.WordLists.Select(e => new WordDto
        {
            Id = e.Id,
            WordTitle = e.WordTitle,
            WordsJson = e.WordsJson,
            EffectType = e.EffectType.HasValue ? (int)e.EffectType.Value : null,
            Level = e.Level,
            IsMustNeed = e.IsMustNeed,
            WordType_Id = e.WordType_Id,
            State = e.State,
            IsExcludedInOtaRoom = e.IsExcludedInOtaRoom,
            SimilarWordIds = e.SimilarWordIds,
            MutuallyExclusiveWordIds = e.MutuallyExclusiveWordIds,
            CreateTime = e.CreateTime,
            LastUpdateTime = e.LastUpdateTime
        }).ToListAsync(cancellationToken);
    }

    public async Task<List<WordTypeDto>> GetWordTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.WordTypes.Select(e => new WordTypeDto
        {
            Id = e.Id,
            WorldType = e.WorldType,
            IsMustNeed = e.IsMustNeed,
            Level = e.Level
        }).ToListAsync(cancellationToken);
    }

    public async Task InsertWordAsync(WordDto word, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(word);
        _db.WordLists.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        word.Id = entity.Id;
    }

    public async Task UpdateWordAsync(WordDto word, CancellationToken cancellationToken = default)
    {
        var existing = await _db.WordLists.FirstOrDefaultAsync(e => e.Id == word.Id, cancellationToken);
        if (existing is null) return;

        existing.WordTitle = word.WordTitle;
        existing.WordsJson = word.WordsJson;
        existing.EffectType = word.EffectType.HasValue ? (EffectType)word.EffectType.Value : null;
        existing.Level = word.Level;
        existing.IsMustNeed = word.IsMustNeed;
        existing.WordType_Id = word.WordType_Id;
        existing.State = word.State;
        existing.IsExcludedInOtaRoom = word.IsExcludedInOtaRoom;
        existing.SimilarWordIds = word.SimilarWordIds;
        existing.MutuallyExclusiveWordIds = word.MutuallyExclusiveWordIds;
        existing.LastUpdateTime = word.LastUpdateTime;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.CommitTransactionAsync(cancellationToken);
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.RollbackTransactionAsync(cancellationToken);
    }

    private static WordList MapToEntity(WordDto dto) => new()
    {
        WordTitle = dto.WordTitle,
        WordsJson = dto.WordsJson,
        EffectType = dto.EffectType.HasValue ? (EffectType)dto.EffectType.Value : null,
        Level = dto.Level,
        IsMustNeed = dto.IsMustNeed,
        WordType_Id = dto.WordType_Id,
        State = dto.State,
        IsExcludedInOtaRoom = dto.IsExcludedInOtaRoom,
        SimilarWordIds = dto.SimilarWordIds,
        MutuallyExclusiveWordIds = dto.MutuallyExclusiveWordIds,
        CreateTime = dto.CreateTime
    };
}
