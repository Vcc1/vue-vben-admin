using SupRoomInfoIngestion.Contracts;

namespace SupRoomInfoIngestion;

public sealed class SupRoomInfoSourceCoordinatorStep
{
    readonly SupRoomInfoListSourceStep _listSourceStep;
    readonly SupRoomInfoSetSourceStep _setSourceStep;
    readonly SupRoomInfoIngestionState _state;
    readonly SupRoomInfoIngestionMetrics _metrics;

    public SupRoomInfoSourceCoordinatorStep(
        SupRoomInfoListSourceStep listSourceStep,
        SupRoomInfoSetSourceStep setSourceStep,
        SupRoomInfoIngestionState state,
        SupRoomInfoIngestionMetrics metrics)
    {
        _listSourceStep = listSourceStep;
        _setSourceStep = setSourceStep;
        _state = state;
        _metrics = metrics;
    }

    public async Task<IReadOnlyList<TempSupRoomInfo2>> ReadNextAsync(CancellationToken cancellationToken)
    {
        using var scope = _metrics.MeasureDuration("source_coordinate");
        var listPending = await _listSourceStep.GetPendingCountAsync(cancellationToken);
        _state.SetListPending(listPending);

        var listItems = await _listSourceStep.ReadAsync(cancellationToken);
        if (listItems.Count > 0)
        {
            _state.SetListPending(Math.Max(0, listPending - listItems.Count));
            return listItems;
        }

        var setPending = await _setSourceStep.GetPendingCountAsync(cancellationToken);
        _state.SetSetPending(setPending);
        var setItems = await _setSourceStep.ReadAsync(cancellationToken);
        if (setItems.Count > 0)
        {
            _state.SetSetPending(Math.Max(0, setPending - setItems.Count));
        }

        return setItems;
    }
}
