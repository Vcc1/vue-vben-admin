namespace ExpediaChaseRoomApp;

public interface IExpediaTaapPromoLabelProvider
{
    IReadOnlyDictionary<string, decimal> GetPromoLabels(int accountId);

    void Replace(IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> promoLabelsByAccount);
}
