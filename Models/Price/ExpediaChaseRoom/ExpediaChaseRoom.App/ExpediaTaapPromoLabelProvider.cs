namespace ExpediaChaseRoomApp;

public sealed class ExpediaTaapPromoLabelProvider : IExpediaTaapPromoLabelProvider
{
    private static readonly IReadOnlyDictionary<string, decimal> EmptyPromoLabels =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

    private readonly Lock _syncRoot = new();
    private IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> _promoLabelsByAccount =
        new Dictionary<int, IReadOnlyDictionary<string, decimal>>();

    public IReadOnlyDictionary<string, decimal> GetPromoLabels(int accountId)
    {
        lock (_syncRoot)
        {
            return _promoLabelsByAccount.TryGetValue(accountId, out var promoLabels)
                ? promoLabels
                : EmptyPromoLabels;
        }
    }

    public void Replace(IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> promoLabelsByAccount)
    {
        ArgumentNullException.ThrowIfNull(promoLabelsByAccount);

        lock (_syncRoot)
        {
            _promoLabelsByAccount = promoLabelsByAccount;
        }
    }
}
