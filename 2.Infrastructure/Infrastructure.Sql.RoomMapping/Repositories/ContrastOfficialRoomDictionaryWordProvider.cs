using Framework.Core.IOC;
using Infrastructure.Sql.MengTuYDOTA;
using Microsoft.Extensions.DependencyInjection;
using RoomMatch.Core.RoomDictionaryDataSource;

namespace Infrastructure.Sql.RoomMapping.Repositories
{
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)]
    //[Service(typeof(IContrastOfficialRoomDictionaryWordProvider), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)]
    public class ContrastOfficialRoomDictionaryWordProvider : IContrastOfficialRoomDictionaryWordProvider
    {
        private readonly IServiceProvider _serviceProvider;

        public ContrastOfficialRoomDictionaryWordProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IReadOnlyList<RoomDictionaryWordDefinition> Load()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();
            var types = context.WordTypes.ToList();
            var words = context.WordLists.ToList().OrderByDescending(e => e.Id).ToList();

            return words.Select(e =>
            {
                var type = types.First(t => t.Id == e.WordType_Id);
                return new RoomDictionaryWordDefinition
                {
                    Id = e.Id,
                    WordTypeId = e.WordType_Id,
                    WordTypeName = type.WorldType,
                    Level = e.Level,
                    WordTitle = e.WordTitle?.Trim() ?? string.Empty,
                    WordCN = e.WordCN ?? string.Empty,
                    WordEN = e.WordEN ?? string.Empty,
                    IsMustNeed = string.IsNullOrWhiteSpace(e.IsMustNeed) ? type.IsMustNeed ?? string.Empty : e.IsMustNeed,
                    EffectType = e.EffectType,
                    SynonymsWordList = e.SynonymsWordList?.Split(',').Where(w => !string.IsNullOrWhiteSpace(w)).ToList() ?? [],
                    AnotherNameWordList = e.AnotherNameWordList?.Split(',').Where(w => !string.IsNullOrWhiteSpace(w)).ToList() ?? []
                };
            }).ToList();
        }
    }
}
