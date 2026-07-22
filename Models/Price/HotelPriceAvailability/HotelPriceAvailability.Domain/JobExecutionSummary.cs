namespace HotelPriceAvailability.Domain;

public sealed record JobExecutionSummary(
    int TotalTasks,
    int QueryableTasks,
    int PricedTasks,
    int UnpricedTasks,
    int InvalidTasks,
    long TotalRequests,
    long SuccessfulRequests,
    long FailedRequests,
    TimeSpan Elapsed)
{
    public bool HasErrors => InvalidTasks > 0 || FailedRequests > 0;
}
