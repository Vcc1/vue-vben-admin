namespace SupplierStaticDataApp;

public sealed class SupplierStaticDataResult<T>
{
    public bool Success { get; init; }

    public string RequestedCode { get; init; } = string.Empty;

    public string? ResolvedSupplierHotelCode { get; init; }

    public T? Data { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public bool FromCache { get; init; }

    public bool Refreshed { get; init; }

    public string Source { get; init; } = string.Empty;

    public SupplierStaticDataSourceDetails? SourceDetails { get; init; }

    public SupplierStaticDataRawInfo? RawInfo { get; init; }

    public static SupplierStaticDataResult<T> SuccessResult(
        string requestedCode,
        T data,
        string? resolvedSupplierHotelCode,
        bool fromCache,
        bool refreshed,
        SupplierStaticDataRawInfo? rawInfo = null,
        string? source = null,
        SupplierStaticDataSourceDetails? sourceDetails = null)
    {
        return new SupplierStaticDataResult<T>
        {
            Success = true,
            RequestedCode = requestedCode,
            ResolvedSupplierHotelCode = resolvedSupplierHotelCode,
            Data = data,
            FromCache = fromCache,
            Refreshed = refreshed,
            Source = source ?? (fromCache ? "cache" : "direct"),
            SourceDetails = sourceDetails,
            RawInfo = rawInfo
        };
    }

    public static SupplierStaticDataResult<T> Failure(
        string requestedCode,
        string errorCode,
        string errorMessage,
        string? resolvedSupplierHotelCode = null,
        SupplierStaticDataRawInfo? rawInfo = null,
        string? source = null,
        SupplierStaticDataSourceDetails? sourceDetails = null)
    {
        return new SupplierStaticDataResult<T>
        {
            RequestedCode = requestedCode,
            ResolvedSupplierHotelCode = resolvedSupplierHotelCode,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Source = source ?? rawInfo?.Source ?? string.Empty,
            SourceDetails = sourceDetails,
            RawInfo = rawInfo
        };
    }
}
