namespace RoomMatch.Admin.Contracts;

public interface IWordRepository
{
    Task<List<WordDto>> GetWordsAsync(CancellationToken cancellationToken = default);
    Task<List<WordTypeDto>> GetWordTypesAsync(CancellationToken cancellationToken = default);
    Task InsertWordAsync(WordDto word, CancellationToken cancellationToken = default);
    Task UpdateWordAsync(WordDto word, CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

public record WordDto
{
    public int Id { get; set; }
    public string? WordTitle { get; set; }
    public string? WordsJson { get; set; }
    public int? EffectType { get; set; }
    public int? Level { get; set; }
    public string? IsMustNeed { get; set; }
    public int WordType_Id { get; set; }
    public int? State { get; set; }
    public bool? IsExcludedInOtaRoom { get; set; }
    public string? SimilarWordIds { get; set; }
    public string? MutuallyExclusiveWordIds { get; set; }
    public DateTime? CreateTime { get; set; }
    public DateTime? LastUpdateTime { get; set; }
}

public record WordTypeDto
{
    public int Id { get; set; }
    public string? WorldType { get; set; }
    public string? IsMustNeed { get; set; }
    public int? Level { get; set; }
}

public record WordView(List<WordDto> Words, List<WordTypeDto> WordTypes);
