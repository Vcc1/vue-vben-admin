using AgentPlatform.Contracts;
using HotelBusiness.App;
using HotelBusiness.Contracts;
using HotelResearch.Contracts;
using System.Text.Json;
using System.Runtime.CompilerServices;
using Xunit;

namespace HotelResearch.App.Tests;

public sealed class HotelResearchServiceTests
{
    [Fact]
    public async Task ResearchAsync_ReturnsNullWithoutCallingAgent_WhenHotelDoesNotExist()
    {
        var agent = new FakeAgentService();
        var store = new FakeHotelBusinessStore();
        var service = CreateService(new FakeSeedReader(null), agent, store);

        var result = await service.ResearchAsync(new HotelResearchRequest { MTHotelCode = "MT404" });

        Assert.Null(result);
        Assert.Null(agent.LastRequest);
    }

    [Fact]
    public async Task ResearchAsync_UsesHotelResearchAgentWebSearchAndJsonSchema()
    {
        var seed = CreateSeed();
        var agent = new FakeAgentService(new AgentResponse
        {
            ConversationId = "conversation-1",
            AgentName = "hotel-research",
            Text = """
                {
                  "sites": [
                    {
                      "siteName": "Official website",
                      "siteUrl": "https://hotel.example/property",
                      "sourceUrls": ["https://hotel.example/rooms"],
                      "hotelNameChinese": "测试酒店",
                      "hotelNameEnglish": "Test Hotel",
                      "hotelNameLocal": null,
                      "aliases": ["Test Hotel"],
                      "longitude": 120.1,
                      "latitude": 30.2,
                      "starRating": 5,
                      "hotelPhone": "+86 571 1234 5678",
                      "addressChinese": "测试地址",
                      "addressEnglish": "Test Address",
                      "addressLocal": null,
                      "identityVerification": {
                        "status": "confirmed",
                        "matchedSeedName": "Test Hotel",
                        "matchedCandidateName": "Test Hotel",
                        "nameMatchKind": "sameWordsSameOrder",
                        "matchedSignals": ["酒店名称一致", "完整地址一致"],
                        "conflicts": [],
                        "coordinateDistanceMeters": null,
                        "summary": "酒店名称和完整地址均一致，可确认是同一家酒店。"
                      },
                      "rooms": [
                        {
                          "roomNameChinese": "豪华大床房",
                          "roomNameEnglish": "Deluxe King Room",
                          "roomNameLocal": null,
                          "area": "35 m²",
                          "bedType": "1 king bed",
                          "isNonSmoking": true,
                          "viewEnglish": "City view",
                          "facilities": ["Wi-Fi"],
                          "offersAndServices": ["Breakfast included"]
                        },
                        {
                          "roomNameChinese": null,
                          "roomNameEnglish": null,
                          "roomNameLocal": "豪华大床房 Deluxe King Suite",
                          "area": null,
                          "bedType": null,
                          "isNonSmoking": null,
                          "viewEnglish": null,
                          "facilities": [],
                          "offersAndServices": []
                        }
                      ]
                    }
                  ]
                }
                """,
            SearchQueries = ["Test Hotel official website"],
            Sources = [new AgentSource { Url = "https://hotel.example/property", Title = "Test Hotel" }],
            Usage = new AgentUsage { TotalTokens = 100 },
            Provider = "openai",
            Model = "gpt-5"
        });
        var store = new FakeHotelBusinessStore();
        var service = CreateService(new FakeSeedReader(seed), agent, store);

        var result = await service.ResearchAsync(new HotelResearchRequest { MTHotelCode = " MT100 " });

        Assert.NotNull(result);
        Assert.Equal("MT100", result.MTHotelCode);
        Assert.Equal("conversation-1", result.ConversationId);
        Assert.Equal("run-1", result.RunId);
        Assert.Equal(1, result.ResultVersion);
        Assert.Equal("1.3.1", result.Processor.ProcessorVersion);
        Assert.Equal("3.1.0", result.Processor.PromptVersion);
        Assert.Equal(2, result.Processor.ResultSchemaVersion);
        Assert.Equal("openai", result.Processor.Provider);
        Assert.Equal("gpt-5", result.Processor.Model);
        Assert.Single(result.Sites);
        Assert.Equal("confirmed", result.Sites[0].IdentityVerification?.Status);
        Assert.Equal("酒店名称和完整地址均一致，可确认是同一家酒店。", result.Sites[0].IdentityVerification?.Summary);
        Assert.Equal(
            ["酒店名称一致", "完整地址一致"],
            result.Sites[0].IdentityVerification!.MatchedSignals);
        Assert.Equal(2, result.Sites[0].Rooms.Count);
        Assert.Equal("豪华大床房", result.Sites[0].Rooms[0].RoomNameChinese);
        Assert.Equal("Deluxe King Room", result.Sites[0].Rooms[0].RoomNameEnglish);
        Assert.Null(result.Sites[0].Rooms[0].RoomNameLocal);
        Assert.Null(result.Sites[0].Rooms[1].RoomNameChinese);
        Assert.Null(result.Sites[0].Rooms[1].RoomNameEnglish);
        Assert.Equal("豪华大床房 Deluxe King Suite", result.Sites[0].Rooms[1].RoomNameLocal);
        Assert.Empty(result.RejectedCandidates);
        Assert.Equal(2, result.Sites[0].SourceUrls.Count);
        Assert.Equal(100, result.Usage?.TotalTokens);
        Assert.NotNull(agent.LastRequest);
        Assert.Equal("hotel-research", agent.LastRequest.AgentName);
        Assert.True(agent.LastRequest.EnableWebSearch);
        Assert.NotNull(agent.LastRequest.JsonResponseFormat);
        Assert.Contains("additionalProperties", agent.LastRequest.JsonResponseFormat.Schema);
        Assert.Contains("identityVerification", agent.LastRequest.JsonResponseFormat.Schema);
        Assert.Contains("an exact name match is not mandatory", agent.LastRequest.Message);
        Assert.Contains("at least two independent strong property signals", agent.LastRequest.Message);
        Assert.Contains("identity decision is authoritative", agent.LastRequest.Message);
        Assert.Contains("Write identityVerification.summary", agent.LastRequest.Message);
        Assert.Contains("split it only when the Chinese segment", agent.LastRequest.Message);
        Assert.Contains("never translate", agent.LastRequest.Message);
        Assert.Equal("MT100", agent.LastRequest.Metadata?["mtHotelCode"]);
        Assert.Equal("run-1", agent.LastRequest.StorageScope?.RunId);
        Assert.Equal(HotelOperationRunStatus.Completed, store.Status);
    }

    [Fact]
    public async Task ResearchAsync_UsesAiIdentityDecision_WithoutProgrammaticNameMatching()
    {
        var agent = new FakeAgentService(new AgentResponse
        {
            ConversationId = "conversation-1",
            AgentName = "hotel-research",
            Text = """
                {
                  "sites": [
                    {
                      "siteName": "OTA",
                      "siteUrl": "https://ota.example/property",
                      "sourceUrls": [],
                      "hotelNameChinese": null,
                      "hotelNameEnglish": "Test Hotel Apartments",
                      "hotelNameLocal": null,
                      "aliases": ["Test Hotel"],
                      "longitude": null,
                      "latitude": null,
                      "starRating": 5,
                      "hotelPhone": null,
                      "addressChinese": null,
                      "addressEnglish": "Test Address",
                      "addressLocal": null,
                      "identityVerification": {
                        "status": "confirmed",
                        "matchedSeedName": "Test Hotel",
                        "matchedCandidateName": "Test Hotel Apartments",
                        "nameMatchKind": "officialAlias",
                        "matchedSignals": ["完整地址一致"],
                        "conflicts": [],
                        "coordinateDistanceMeters": null,
                        "summary": "完整地址一致，AI 判定为同一家酒店。"
                      },
                      "rooms": []
                    }
                  ]
                }
                """
        });
        var service = CreateService(
            new FakeSeedReader(CreateSeed()),
            agent,
            new FakeHotelBusinessStore());

        var result = await service.ResearchAsync(
            new HotelResearchRequest { MTHotelCode = "MT100" });

        Assert.NotNull(result);
        var accepted = Assert.Single(result.Sites);
        Assert.Equal("Test Hotel Apartments", accepted.HotelNameEnglish);
        Assert.Equal("confirmed", accepted.IdentityVerification?.Status);
        Assert.Empty(result.RejectedCandidates);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResearchAsync_RejectsBlankHotelCode(string code)
    {
        var service = CreateService(
            new FakeSeedReader(null),
            new FakeAgentService(),
            new FakeHotelBusinessStore());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ResearchAsync(new HotelResearchRequest { MTHotelCode = code }));
    }

    [Fact]
    public async Task ResearchAsync_RecordsFailedRun_WhenAgentReturnsInvalidJson()
    {
        var store = new FakeHotelBusinessStore();
        var agent = new FakeAgentService(new AgentResponse
        {
            ConversationId = "conversation-1",
            AgentName = "hotel-research",
            Text = "not-json"
        });
        var service = CreateService(new FakeSeedReader(CreateSeed()), agent, store);

        await Assert.ThrowsAsync<HotelResearchExecutionException>(() =>
            service.ResearchAsync(new HotelResearchRequest { MTHotelCode = "mt100" }));

        Assert.Equal(HotelOperationRunStatus.Failed, store.Status);
        Assert.Equal(0, store.CompletedCount);
    }

    [Fact]
    public async Task ResolveAsync_ReuseOrRefresh_ReturnsReadableStoredResultWithoutCallingAgent()
    {
        var stored = CreateStoredResult();
        var store = new FakeHotelBusinessStore { StoredResult = JsonSerializer.SerializeToElement(stored) };
        var agent = new FakeAgentService();
        var service = CreateService(new FakeSeedReader(CreateSeed()), agent, store);

        var result = await service.ResolveAsync(new HotelResearchResolveRequest
        {
            MTHotelCode = " mt100 ",
            Mode = HotelResearchResolveMode.ReuseOrRefresh
        });

        Assert.NotNull(result);
        Assert.Equal(7, result.ResultVersion);
        Assert.Null(agent.LastRequest);
        Assert.Equal(0, store.CompletedCount);
    }

    [Fact]
    public async Task ResolveAsync_ReuseOnly_ThrowsWhenNoReadableStoredResultExists()
    {
        var service = CreateService(
            new FakeSeedReader(CreateSeed()),
            new FakeAgentService(),
            new FakeHotelBusinessStore());

        await Assert.ThrowsAsync<HotelResearchResultUnavailableException>(() =>
            service.ResolveAsync(new HotelResearchResolveRequest
            {
                MTHotelCode = "MT100",
                Mode = HotelResearchResolveMode.ReuseOnly
            }));
    }

    [Fact]
    public async Task GetFactsAsync_PreservesConfirmedSitesAndSourceVersion()
    {
        var stored = CreateStoredResult();
        var store = new FakeHotelBusinessStore { StoredResult = JsonSerializer.SerializeToElement(stored) };
        var service = CreateService(
            new FakeSeedReader(CreateSeed()),
            new FakeAgentService(),
            store);

        var facts = await service.GetFactsAsync("MT100", 7);

        Assert.NotNull(facts);
        Assert.Equal(7, facts.SourceResultVersion);
        Assert.Equal("run-7", facts.SourceRunId);
        Assert.Single(facts.ConfirmedSites);
        Assert.Equal("Official website", facts.ConfirmedSites[0].SiteName);
        Assert.Single(facts.ConfirmedSites[0].Rooms);
    }

    static HotelResearchService CreateService(
        IHotelResearchSeedReader seedReader,
        IAgentService agentService,
        IHotelBusinessStore store)
        => new(seedReader, agentService, store, new HotelOperationConcurrencyGuard());

    static HotelResearchDatabaseHotel CreateSeed()
        => new()
        {
            HotelId = 100,
            MTHotelCode = "MT100",
            HotelName = "测试酒店",
            HotelNameEnglish = "Test Hotel",
            AddressEnglish = "Test Address"
        };

    static HotelResearchResult CreateStoredResult()
        => new()
        {
            MTHotelCode = "MT100",
            RunId = "run-7",
            ResultVersion = 7,
            ConversationId = "conversation-7",
            StartedAt = DateTimeOffset.Parse("2026-08-21T10:00:00Z"),
            ResearchedAt = DateTimeOffset.Parse("2026-08-21T10:01:00Z"),
            Processor = new HotelProcessorDescriptor
            {
                Name = "hotel-research",
                ProcessorVersion = "1.3.1",
                PromptVersion = "3.1.0",
                ResultSchemaVersion = 2,
                AgentName = "hotel-research",
                Provider = "openai-responses",
                Model = "gpt-5.6-sol",
                WebSearchEnabled = true
            },
            DatabaseHotel = CreateSeed(),
            Sites =
            [
                new HotelResearchSite
                {
                    SiteName = "Official website",
                    SiteUrl = "https://hotel.example/property",
                    Rooms = [new HotelResearchRoom { RoomNameEnglish = "Deluxe King Room" }],
                    IdentityVerification = new HotelResearchIdentityVerification
                    {
                        Status = "confirmed",
                        Summary = "确认是同一家酒店。"
                    }
                }
            ]
        };

    sealed class FakeSeedReader(HotelResearchDatabaseHotel? seed) : IHotelResearchSeedReader
    {
        public Task<HotelResearchDatabaseHotel?> FindAsync(
            string mtHotelCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult(seed);
    }

    sealed class FakeAgentService(AgentResponse? response = null) : IAgentService
    {
        public AgentRequest? LastRequest { get; private set; }

        public Task<AgentResponse> RunAsync(
            AgentRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(response ?? throw new InvalidOperationException("Agent should not be called."));
        }

        public async IAsyncEnumerable<AgentStreamEvent> RunStreamingAsync(
            AgentRequest request,
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    sealed class FakeHotelBusinessStore : IHotelBusinessStore
    {
        public JsonElement? StoredResult { get; init; }

        public HotelOperationRunStatus? Status { get; private set; }

        public int CompletedCount { get; private set; }

        public Task<IReadOnlyList<HotelBusinessRecentItem>> GetRecentAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HotelBusinessRecentItem>>([]);

        public Task<HotelBusinessDetail?> GetAsync(
            string mtHotelCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult<HotelBusinessDetail?>(null);

        public Task<HotelOperationRunHandle> StartRunAsync(
            HotelOperationRunStart start,
            CancellationToken cancellationToken = default)
        {
            Status = HotelOperationRunStatus.Running;
            return Task.FromResult(new HotelOperationRunHandle
            {
                MTHotelCode = start.MTHotelCode,
                HotelName = start.HotelName ?? start.MTHotelCode,
                OperationType = start.OperationType,
                RunId = "run-1",
                ConversationId = start.ConversationId ?? "conversation-1",
                StartedAt = DateTimeOffset.Parse("2026-08-21T10:00:00Z"),
                StorageScope = new AgentConversationStorageScope
                {
                    BusinessType = "hotel",
                    BusinessKey = start.MTHotelCode,
                    OperationType = start.OperationType,
                    RunId = "run-1"
                }
            });
        }

        public Task<T> CompleteRunAsync<T>(
            HotelOperationRunHandle run,
            HotelProcessorDescriptor processor,
            Func<int, DateTimeOffset, T> resultFactory,
            CancellationToken cancellationToken = default)
        {
            CompletedCount++;
            Status = HotelOperationRunStatus.Completed;
            return Task.FromResult(resultFactory(
                CompletedCount,
                DateTimeOffset.Parse("2026-08-21T10:01:00Z")));
        }

        public Task FinishRunWithoutResultAsync(
            HotelOperationRunHandle run,
            HotelOperationRunStatus status,
            Exception? exception,
            CancellationToken cancellationToken = default)
        {
            Status = status;
            return Task.CompletedTask;
        }

        public Task<JsonElement?> GetResultAsync(
            string mtHotelCode,
            string operationType,
            int? resultVersion,
            CancellationToken cancellationToken = default)
            => Task.FromResult(StoredResult);

        public Task<HotelBusinessRunConversation?> GetRunConversationAsync(
            string mtHotelCode,
            string operationType,
            string runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<HotelBusinessRunConversation?>(null);
    }
}
