using AgentPlatform.Contracts;
using Infrastructure.OTARoomBuilder;
using Infrastructure.Sql.MengTuSup.Entitys;
using OTARoomBuilder.AI;
using OTARoomBuilder.Contracts;
using Xunit;

namespace OTARoomBuilder.App.Tests;

public sealed class AiRoomBranchTests
{
    [Fact]
    public void AiSupplierQuery_LoadsAllSuppliersAndRoomAttributes()
    {
        var sql = OtaRoomBuilderRepository.BuildSupplierRoomsQuery(
            supplierFilter: string.Empty,
            lastDaysOffset: null);

        Assert.DoesNotContain("a.SupId IN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.BedInfo", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.AcreageFrom", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.AcreageTo", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.SmokeType", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.MaxAdt", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SupplierRoomMapping_PreservesBedAreaSmokingAndOccupancy()
    {
        var room = OtaRoomBuilderRepository.MapSupplierRoom(new OtaRoomBuilderRepository.SupplierRoomRow
        {
            Id = 1001,
            SupId = 99,
            SHotelCode = "H-99",
            SRoomCode = "R-1001",
            MTHotelCode = "MT-100",
            SRoomNameCN = "豪华大床房",
            SRoomNameEN = "Deluxe King Room",
            BedInfo = "1 king bed",
            AcreageFrom = 35.5m,
            AcreageTo = 42m,
            SmokeType = 1,
            MaxAdt = 3
        });

        Assert.Equal(99, room.SupId);
        Assert.Equal("1 king bed", room.BedInfo);
        Assert.Equal(35.5m, room.AcreageFrom);
        Assert.Equal(42m, room.AcreageTo);
        Assert.Equal(1, room.SmokeType);
        Assert.Equal(3, room.MaxAdt);
    }

    [Fact]
    public void CandidateSelector_DoesNotApplySupplierPriorityBlocking()
    {
        var rooms = new[]
        {
            CreateRoom(1, 51, "Room 51"),
            CreateRoom(2, 32, "Room 32"),
            CreateRoom(3, 1, "Room 1"),
            CreateRoom(4, 99, "Room 99"),
            CreateRoom(5, 88, string.Empty)
        };

        var selected = AiRoomCandidateSelector
            .Select(rooms, new HashSet<int> { 1 })
            .Select(room => room.SupId)
            .ToArray();

        Assert.Equal([32, 1, 99], selected);
    }

    [Fact]
    public void HotelContext_KeepsLegacyAndAiSupplierInputsSeparate()
    {
        var context = new HotelRoomBuildContext
        {
            MTHotelCode = "MT-100",
            SupplierRooms = [CreateRoom(1, 51, "Legacy room")],
            AiSupplierRooms =
            [
                CreateRoom(1, 51, "Legacy room"),
                CreateRoom(2, 32, "Room 32"),
                CreateRoom(3, 1, "Room 1"),
                CreateRoom(4, 99, "Room 99")
            ]
        };

        Assert.Single(context.SupplierRooms);
        Assert.Equal([51, 32, 1, 99], context.AiSupplierRooms.Select(room => room.SupId));
    }

    [Fact]
    public async Task MafBuilder_SendsEveryCandidateInOneNonBlockingRequest()
    {
        var agent = new CapturingAgentService();
        var builder = new MafAiRoomBuilder(agent);
        var candidates = new[]
        {
            CreateCandidate(101, 51),
            CreateCandidate(102, 32),
            CreateCandidate(103, 1),
            CreateCandidate(104, 99)
        };

        await builder.ProcessAsync("MT-100", candidates, facts: null);

        Assert.NotNull(agent.Request);
        Assert.False(agent.Request.EnableWebSearch);
        Assert.Contains("不能按该优先级阻断本批房型，必须逐条判断所有输入", agent.Request.Message);
        foreach (var candidate in candidates)
        {
            Assert.Contains($"\"supplierRoomId\": {candidate.SupplierRoomId}", agent.Request.Message);
            Assert.Contains($"\"supId\": {candidate.SupId}", agent.Request.Message);
        }
    }

    static SupRoomInfo CreateRoom(int id, int supId, string name) => new()
    {
        Id = id,
        SupId = supId,
        SHotelCode = $"H-{supId}",
        SRoomCode = $"R-{id}",
        MTHotelCode = "MT-100",
        SRoomNameCN = string.Empty,
        SRoomNameEN = name,
        BedInfo = string.Empty,
        BedInfoKey = string.Empty
    };

    static AiRoomCandidate CreateCandidate(int id, int supId) => new()
    {
        SupplierRoomId = id,
        SupId = supId,
        SHotelCode = $"H-{supId}",
        SRoomCode = $"R-{id}",
        MTHotelCode = "MT-100",
        RoomNameEnglish = $"Room {supId}"
    };

    sealed class CapturingAgentService : IAgentService
    {
        public AgentRequest? Request { get; private set; }

        public Task<AgentResponse> RunAsync(
            AgentRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new AgentResponse
            {
                ConversationId = "test-conversation",
                AgentName = request.AgentName,
                Text = "{\"decisions\":[]}",
                Provider = "test",
                Model = "test"
            });
        }

        public async IAsyncEnumerable<AgentStreamEvent> RunStreamingAsync(
            AgentRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
