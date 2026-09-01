using Infrastructure.Sql.MengTuSup.Entitys;

namespace OTARoomBuilder.AI;

internal static class AiRoomCandidateSelector
{
    public static IEnumerable<SupRoomInfo> Select(
        IEnumerable<SupRoomInfo> supplierRooms,
        IReadOnlySet<int> successfullyCreatedSupplierRoomIds)
    {
        return supplierRooms.Where(room =>
            !successfullyCreatedSupplierRoomIds.Contains(room.Id)
            && (!string.IsNullOrWhiteSpace(room.SRoomNameCN)
                || !string.IsNullOrWhiteSpace(room.SRoomNameEN)));
    }
}
