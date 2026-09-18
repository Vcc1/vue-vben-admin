using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.App.Models;

namespace HotelMatchingPipeline.App;

public sealed class HotelMatchVetoService
{
    static readonly TimeSpan StatusPollInterval = TimeSpan.FromMilliseconds(250);

    readonly IHotelMatchVetoRepository _repository;
    readonly HotelMatchVetoSyncSignal _syncSignal;

    public HotelMatchVetoService(
        IHotelMatchVetoRepository repository,
        HotelMatchVetoSyncSignal syncSignal)
    {
        _repository = repository;
        _syncSignal = syncSignal;
    }

    public async Task<HotelMatchVetoResult> VetoAsync(
        HotelMatchVetoRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var normalized = request with
        {
            SHotelCode = request.SHotelCode.Trim(),
            MTHotelCode = request.MTHotelCode.Trim(),
            Operator = request.Operator.Trim(),
            Reason = request.Reason.Trim()
        };
        var creation = await _repository.CreateAsync(normalized, cancellationToken);
        if (creation.VetoId is null)
        {
            return new HotelMatchVetoResult(
                false,
                Status(creation.Status),
                normalized.SupId,
                normalized.SHotelCode,
                normalized.MTHotelCode,
                null,
                null,
                creation.CurrentMTHotelCode);
        }

        _syncSignal.Pulse();
        while (true)
        {
            var state = await _repository.GetAsync(creation.VetoId.Value, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Hotel match veto {creation.VetoId.Value} disappeared after creation.");
            if (state.RedisSyncedAt.HasValue)
            {
                return new HotelMatchVetoResult(
                    true,
                    "vetoed",
                    state.SupId,
                    state.SHotelCode,
                    state.MTHotelCode,
                    state.DatabaseUpdatedAt,
                    state.RedisSyncedAt);
            }

            await Task.Delay(StatusPollInterval, cancellationToken);
        }
    }

    static void Validate(HotelMatchVetoRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SupId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.SupId), "SupId must be greater than zero.");
        }

        ValidateRequired(request.SHotelCode, nameof(request.SHotelCode), 50);
        ValidateRequired(request.MTHotelCode, nameof(request.MTHotelCode), 100);
        ValidateRequired(request.Operator, nameof(request.Operator), 100);
        ValidateRequired(request.Reason, nameof(request.Reason), 500);
    }

    static void ValidateRequired(string value, string parameterName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Trim().Length > maximumLength)
        {
            throw new ArgumentException(
                $"{parameterName} must be at most {maximumLength} characters.",
                parameterName);
        }
    }

    static string Status(HotelMatchVetoCreateStatus status)
        => status switch
        {
            HotelMatchVetoCreateStatus.SupplierNotFound => "supplierNotFound",
            HotelMatchVetoCreateStatus.MatchNotFound => "matchNotFound",
            HotelMatchVetoCreateStatus.CurrentMatchChanged => "currentMatchChanged",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
}
