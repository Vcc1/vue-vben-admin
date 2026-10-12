using Microsoft.Extensions.Logging;
using RoomMatch.Admin.Contracts;

namespace RoomMatch.Admin.App;

public sealed class WordApp
{
    private readonly IWordRepository _wordRepository;
    private readonly ILogger<WordApp> _logger;

    public WordApp(IWordRepository wordRepository, ILogger<WordApp> logger)
    {
        _wordRepository = wordRepository;
        _logger = logger;
    }

    public async Task<WordView> GetData(CancellationToken cancellationToken = default)
    {
        var words = await _wordRepository.GetWordsAsync(cancellationToken);
        var wordTypes = await _wordRepository.GetWordTypesAsync(cancellationToken);
        return new WordView(words, wordTypes);
    }

    public async Task Update(List<WordDto> words, CancellationToken cancellationToken = default)
    {
        await _wordRepository.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var word in words)
            {
                if (word.Id <= 0)
                {
                    word.CreateTime = DateTime.Now;
                    await _wordRepository.InsertWordAsync(word, cancellationToken);
                }
                else
                {
                    word.LastUpdateTime = DateTime.Now;
                    await _wordRepository.UpdateWordAsync(word, cancellationToken);
                }
            }

            await _wordRepository.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update words failed");
            await _wordRepository.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
