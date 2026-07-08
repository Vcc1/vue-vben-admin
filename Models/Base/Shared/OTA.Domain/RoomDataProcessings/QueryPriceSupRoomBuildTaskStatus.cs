namespace OTADomain.RoomDataProcessings;

public enum QueryPriceSupRoomBuildTaskStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    RetryWaiting = 3,
    Failed = 4
}

public static class QueryPriceSupRoomBuildTaskStatusExtensions
{
    public static bool IsCompleted(this QueryPriceSupRoomBuildTaskStatus status)
    {
        return status == QueryPriceSupRoomBuildTaskStatus.Completed;
    }

    public static bool CanProcess(this QueryPriceSupRoomBuildTaskStatus status)
    {
        return status is QueryPriceSupRoomBuildTaskStatus.Pending
            or QueryPriceSupRoomBuildTaskStatus.RetryWaiting;
    }
}
