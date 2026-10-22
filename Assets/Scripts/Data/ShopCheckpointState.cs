using System.Collections.Generic;

// Run-local checkpoint state. Never stored on shared content definitions.
public sealed class ShopCheckpointState
{
    public readonly List<ShopOffer> Offers = new();
    public readonly HashSet<ShopOfferKind> UsedServices = new();
    public readonly HashSet<string> PurchasedConsumables = new();
    public readonly HashSet<string> PurchasedGoods = new();
    public int RefreshCount;
    public bool MadePricedPurchase;
    public bool Completed;
    public int RefreshPrice => (int)System.Math.Min(int.MaxValue,
        GameBalance.Current.blackMarket.refreshBasePrice + (long)RefreshCount * (RefreshCount + 3L) / 2L);
}
