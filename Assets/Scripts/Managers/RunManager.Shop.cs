using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// RunManager: Black Market goods, Services, Investments, sales and pricing.
public partial class RunManager
{
    // The sum of each vector is the number of Goods; every Goods slot rolls Gear or a Trick.
    // A run takes the counts from GameBalance; a loaded save keeps the counts it was saved with.
    Vector3Int? _midShopGoods, _preHeadlinerShopGoods;
    internal Vector3Int midShopGoods
    {
        get => _midShopGoods ?? GameBalance.Current.blackMarket.midGoods;
        set => _midShopGoods = value;
    }
    internal Vector3Int preHeadlinerShopGoods
    {
        get => _preHeadlinerShopGoods ?? GameBalance.Current.blackMarket.preHeadlinerGoods;
        set => _preHeadlinerShopGoods = value;
    }

    List<ShopOffer> _activeShopOffers = new();
    readonly Dictionary<(int map, int node), ShopCheckpointState> _shopStates = new();
    ShopCheckpointState _activeShopState;
    bool _shopTransaction;
    public int GoodsRefreshPrice => _activeShopState?.RefreshPrice ?? GameBalance.Current.blackMarket.refreshBasePrice;
    public int GoodsRefreshCount => _activeShopState?.RefreshCount ?? 0;
    HashSet<ShopOfferKind> _usedSpecialShopOffers = new();
    HashSet<string> _purchasedConsumablesInShop = new(StringComparer.Ordinal);
    readonly List<int> _pendingInvestmentStakes = new();
    int _paidItemPurchaseCount;
    int _merchantGiftRewardCount;
    bool _madeSuccessfulPurchaseInCurrentShop;
    public int PendingInvestmentCount => _pendingInvestmentStakes.Count;
    public int PaidItemPurchaseCount => _paidItemPurchaseCount;
    public bool CanSellItems => IsActiveShopForSale();
    public IReadOnlyList<ShopOffer> GetCurrentShopOffers() => _activeShopOffers;

    public bool SellGear(long gearInstanceId, int expectedAuthoredPrice)
    {
        if (!IsActiveShopForSale() || GameplayInputGate.IsBlocked) return false;
        var cards = CardManager.Instance;
        var instance = cards != null ? cards.GetGearInstanceById(gearInstanceId) : null;
        if (instance == null || instance.Definition == null || instance.Definition.price < 0 ||
            instance.Definition.price != expectedAuthoredPrice) return false;
        int payout = SalePayout(expectedAuthoredPrice);
        if (!cards.RemoveGearInstance(gearInstanceId)) return false;
        if (payout > 0) cards.AddCredits(payout);
        SaveGameService.Checkpoint(this);
        return true;
    }

    public bool SellConsumable(int slotIndex, ConsumableInstance expectedInstance, int expectedAuthoredPrice)
    {
        if (!IsActiveShopForSale() || GameplayInputGate.IsBlocked || expectedInstance == null) return false;
        var cards = CardManager.Instance;
        var definition = expectedInstance.Definition;
        if (cards == null || definition == null || definition.price < 0 ||
            definition.price != expectedAuthoredPrice) return false;
        int payout = SalePayout(expectedAuthoredPrice);
        if (!cards.RemoveConsumableInstanceAtSlot(slotIndex, expectedInstance)) return false;
        if (payout > 0) cards.AddCredits(payout);
        SaveGameService.Checkpoint(this);
        return true;
    }

    static int SalePayout(int authoredPrice) =>
        (int)((long)authoredPrice * GameBalance.Current.blackMarket.sellPercent / 100L);

    bool IsActiveShopForSale() => !_shopTransaction && _activeNode != null && !_activeNode.completed &&
        _activeNode.kind == MapNodeType.Shop && CardManager.Instance != null;

    public bool CanPurchaseShopOffer(string stableId) =>
        string.IsNullOrEmpty(GetShopOfferUnavailableReason(stableId));

    public string GetShopOfferUnavailableReason(string stableId) => ShopOfferReason(stableId, out _);

    /// <summary>
    /// Why an offer cannot be bought, leaving out a plain lack of Credits: the Black Market shows only
    /// these, and a short purse just disables the price button (owner, 2026-10-23).
    /// </summary>
    public string GetShopOfferBlockedReason(string stableId)
    {
        string reason = ShopOfferReason(stableId, out bool creditsOnly);
        return creditsOnly ? string.Empty : reason;
    }

    string ShopOfferReason(string stableId, out bool creditsOnly)
    {
        creditsOnly = false;
        if (_shopTransaction) return "Black Market transaction in progress";
        if (_activeNode == null || _activeNode.completed || _activeNode.kind != MapNodeType.Shop || string.IsNullOrEmpty(stableId))
            return "Offer unavailable";

        var offer = _activeShopOffers.FirstOrDefault(candidate => candidate.StableId == stableId);
        var cards = CardManager.Instance;
        if (offer == null || cards == null) return "Offer unavailable";
        if (offer.price < 0) return "Invalid price";
        // Supply is a repeatable service: each purchase is bounded only by Credits, Draw Pile and hand space.
        if (offer.purchased && offer.kind != ShopOfferKind.Supply) return "Already purchased at this Black Market";

        if (offer.kind == ShopOfferKind.Gear)
        {
            string reason = cards.GetStandaloneGearUnavailableReason(offer.gear);
            if (!string.IsNullOrEmpty(reason)) return reason;
        }
        else if (offer.kind == ShopOfferKind.Contract)
        {
            if (_activeContract != null || _pendingContractSelectionEvent != null)
                return "A Contract is already active";
            if (_usedSpecialShopOffers.Contains(offer.kind)) return "Already purchased during this Black Market visit";
        }
        else if (offer.kind == ShopOfferKind.Consumable)
        {
            if (offer.consumable == null || !offer.consumable.shopAvailable ||
                !cards.consumableCatalog.Contains(offer.consumable)) return "Trick unavailable";
            if (_purchasedConsumablesInShop.Contains(offer.consumable.id)) return "Already purchased during this Black Market visit";
            if (!cards.CanAddConsumable(offer.consumable)) return "Backpack Full";
        }
        else if (offer.kind == ShopOfferKind.Supply)
        {
            int maximum = GetSupplyMaximumQuantity();
            if (maximum <= 0)
            {
                if (cards.deck.Count == 0) return "Draw Pile empty";
                if (cards.HandCount >= cards.HandCapacity) return "Hand full";
                creditsOnly = true;
                return "Supply needs Credits";
            }
            if (offer.supplyQuantity <= 0 || offer.supplyQuantity > maximum) return $"Choose 1–{maximum} cards";
        }
        else if (offer.kind == ShopOfferKind.Investment || offer.kind == ShopOfferKind.Guidance)
        {
            if (_usedSpecialShopOffers.Contains(offer.kind)) return "Already purchased during this Black Market visit";
            if (offer.kind == ShopOfferKind.Investment && (offer.investmentStake <= 0 || offer.investmentStake > cards.credits))
            {
                creditsOnly = cards.credits <= 0;
                return "Stake must be between 1 and your current Credits";
            }
            if (offer.kind == ShopOfferKind.Guidance && !CanRevealEligibleHiddenNode())
                return "No eligible hidden node to reveal";
        }
        else return "Offer unavailable";

        int requiredCredits = offer.kind == ShopOfferKind.Investment ? offer.investmentStake : offer.price;
        if (cards.CanAfford(requiredCredits)) return string.Empty;
        creditsOnly = true;
        return $"Need {requiredCredits} Credits (have {cards.credits} Credits)";
    }

    // No purchase needs a target: Mods are sold as Mod Tricks.
    public bool PurchaseShopOffer(string stableId)
    {
        if (GameplayInputGate.IsBlocked || !CanPurchaseShopOffer(stableId)) return false;
        var offer = _activeShopOffers.First(candidate => candidate.StableId == stableId);
        if (offer.kind == ShopOfferKind.Supply) return PurchaseSupply(stableId, offer.supplyQuantity);
        _shopTransaction = true;
        try
        {
            bool success = PurchaseShopOfferCore(offer);
            if (success && offer.kind != ShopOfferKind.Contract) MarkShopPurchase(offer);
            return success;
        }
        finally { _shopTransaction = false; OnShopOffersChanged?.Invoke(); SaveGameService.Checkpoint(this); }
    }

    void MarkShopPurchase(ShopOffer offer)
    {
        offer.purchased = true;
        if (!offer.IsService) _activeShopState?.PurchasedGoods.Add(GoodsIdentity(offer));
        if (_activeShopState != null) _activeShopState.MadePricedPurchase = _madeSuccessfulPurchaseInCurrentShop;
    }

    static string GoodsIdentity(ShopOffer offer) => offer.kind switch
    {
        ShopOfferKind.Gear => "artifact:" + offer.gear?.id,
        ShopOfferKind.Enhancement => "enhancement:" + offer.enhancement?.id,
        ShopOfferKind.Consumable => "consumable:" + offer.consumable?.id,
        _ => string.Empty
    };

    bool PurchaseShopOfferCore(ShopOffer offer)
    {
        var cards = CardManager.Instance;
        var purchaseModifiers = GearRules.CapturePurchaseModifiers(cards);
        int creditsBefore = cards.credits;
        bool success;
        if (offer.kind == ShopOfferKind.Gear)
            success = cards.BuyStandaloneGear(offer.gear, offer.price);
        else if (offer.kind == ShopOfferKind.Contract)
        {
            success = ShowContractSelection(offer.price);
        }
        else if (offer.kind == ShopOfferKind.Consumable)
        {
            success = cards.BuyConsumable(offer.consumable, offer.price);
            if (success) _purchasedConsumablesInShop.Add(offer.consumable.id);
        }
        else if (offer.kind == ShopOfferKind.Investment)
        {
            success = cards.SpendCredits(offer.investmentStake);
            if (success)
            {
                _pendingInvestmentStakes.Add(offer.investmentStake);
                _usedSpecialShopOffers.Add(offer.kind);
            }
        }
        else if (offer.kind == ShopOfferKind.Guidance)
        {
            if (!CanRevealEligibleHiddenNode() || !cards.SpendCredits(offer.price)) return false;
            if (!TryRevealHiddenNodeFromEvent())
            {
                cards.AddCredits(offer.price);
                return false;
            }
            _usedSpecialShopOffers.Add(offer.kind);
            success = true;
        }
        else return false;
        if (!success) return false;
        if (offer.kind != ShopOfferKind.Contract && offer.price > 0)
            _madeSuccessfulPurchaseInCurrentShop = true;
        if (IsEligiblePaidItem(offer.kind))
            CompletePaidItemPurchase(creditsBefore - cards.credits, purchaseModifiers);
        RepriceActiveShopOffers();
        return true;
    }

    public int GetSupplyMaximumQuantity()
    {
        var cards = CardManager.Instance;
        if (cards == null || !IsActiveShopForSale()) return 0;
        int affordable = cards.credits / GameBalance.Current.blackMarket.supplyPricePerCard;
        return Math.Max(0, Math.Min(affordable, Math.Min(cards.deck.Count, cards.HandCapacity - cards.HandCount)));
    }

    public bool SetSupplyQuantity(string stableId, int quantity)
    {
        if (GameplayInputGate.IsBlocked || _shopTransaction || !IsActiveShopForSale()) return false;
        var offer = _activeShopOffers.FirstOrDefault(value => value.StableId == stableId);
        if (offer == null || offer.kind != ShopOfferKind.Supply ||
            quantity <= 0 || quantity > GetSupplyMaximumQuantity()) return false;
        offer.supplyQuantity = quantity;
        return true;
    }

    public bool PurchaseSupply(string stableId, int quantity)
    {
        if (GameplayInputGate.IsBlocked || _shopTransaction || !IsActiveShopForSale()) return false;
        var offer = _activeShopOffers.FirstOrDefault(value => value.StableId == stableId);
        if (offer == null || offer.kind != ShopOfferKind.Supply ||
            quantity <= 0 || quantity > GetSupplyMaximumQuantity()) return false;
        _shopTransaction = true;
        try
        {
            // CardManager commits zones and payment before publishing either notification.
            int drawn = CardManager.Instance.PurchaseExistingDraw(quantity, GameBalance.Current.blackMarket.supplyPricePerCard);
            if (drawn <= 0) return false;
            _madeSuccessfulPurchaseInCurrentShop = true;
            // The offer stays available; the next quantity defaults to what is still affordable.
            offer.purchased = false;
            offer.supplyQuantity = Math.Max(1, Math.Min(offer.supplyQuantity, GetSupplyMaximumQuantity()));
            if (_activeShopState != null) _activeShopState.MadePricedPurchase = true;
            return true;
        }
        finally { _shopTransaction = false; OnShopOffersChanged?.Invoke(); SaveGameService.Checkpoint(this); }
    }

    static bool IsEligiblePaidItem(ShopOfferKind kind) => kind is
        ShopOfferKind.Gear or ShopOfferKind.Consumable;

    void CompletePaidItemPurchase(int actualCreditsSpent, GearRules.PurchaseModifiers modifiers)
    {
        actualCreditsSpent = Math.Max(0, actualCreditsSpent);
        if (actualCreditsSpent <= 0) return;
        var cards = CardManager.Instance;
        int refund = GearRules.CashbackRefund(modifiers, actualCreditsSpent);
        if (refund > 0 && cards != null)
        {
            GearRules.ReportCashback(cards, refund);
            cards.AddCredits(refund);
        }
        if (!modifiers.MerchantsGift) return;
        _paidItemPurchaseCount = IncrementToMaximum(_paidItemPurchaseCount);
        if (!GearRules.LoyaltyGiftRewardDue(_paidItemPurchaseCount)) return;
        _merchantGiftRewardCount = IncrementToMaximum(_merchantGiftRewardCount);
        QueueRandomNonEnhancementReward($"merchant-gift:{_merchantGiftRewardCount}",
            GearRules.OwnedGearName(cards, GearSpecialRule.MerchantsGift), "merchant-gift", _merchantGiftRewardCount);
    }

    int CalculateEffectiveShopPrice(ShopOfferKind kind, int basePrice)
    {
        basePrice = Math.Max(0, basePrice);
        var cards = CardManager.Instance;
        return IsEligiblePaidItem(kind) ? GearRules.DiscountedItemPrice(cards, basePrice) : basePrice;
    }

    void RepriceActiveShopOffers()
    {
        bool changed = false;
        for (int i = 0; i < _activeShopOffers.Count; i++)
        {
            var offer = _activeShopOffers[i];
            if (offer == null) continue;
            if (offer.basePrice == 0 && offer.price > 0) offer.basePrice = offer.price;
            int effective = CalculateEffectiveShopPrice(offer.kind, offer.basePrice);
            if (offer.price == effective) continue;
            offer.price = effective;
            changed = true;
        }
        if (changed) OnShopOffersChanged?.Invoke();
    }

    public bool SetInvestmentStake(string stableId, int stake)
    {
        if (GameplayInputGate.IsBlocked || _shopTransaction || !IsActiveShopForSale()) return false;
        var offer = _activeShopOffers.FirstOrDefault(candidate => candidate != null && candidate.StableId == stableId);
        var cards = CardManager.Instance;
        if (offer == null || offer.kind != ShopOfferKind.Investment || cards == null || stake <= 0 || stake > cards.credits)
            return false;
        offer.investmentStake = stake;
        return true;
    }

    void ResolvePendingInvestments(PathNode shopNode)
    {
        if (_pendingInvestmentStakes.Count == 0 || shopNode == null || CardManager.Instance == null) return;
        if (_randomContext == null) _randomContext = new RunRandomContext(runSeed);
        int context = unchecked(shopNode.mapIndex * 7919 ^ shopNode.id);
        var random = _randomContext.CreateStream("shop-investment-payout", context);
        long payout = 0;
        long totalStake = 0;
        int wins = 0;
        foreach (int stake in _pendingInvestmentStakes)
        {
            totalStake += stake;
            if (random.NextInt(0, 2) != 0) continue;
            wins++;
            payout = Math.Min(int.MaxValue, payout + (long)stake * GameBalance.Current.blackMarket.stakePayoutMultiplier);
        }
        int losses = _pendingInvestmentStakes.Count - wins;
        _pendingInvestmentStakes.Clear();
        if (payout > 0) CardManager.Instance.AddCredits((int)payout);
        OnInvestmentResult?.Invoke(
            $"Stake results: {wins} success(es), {losses} loss(es); payout {payout} Credits, stakes at risk {totalStake} Credits.");
    }

    void PrepareShopOffers(int maximumOffers = 4)
    {
        var cards = CardManager.Instance;
        if (_activeNode == null || _activeNode.kind != MapNodeType.Shop || cards == null) return;
        if (_activeShopState != null) _activeShopState.MadePricedPurchase = _madeSuccessfulPurchaseInCurrentShop;
        var key = (_activeNode.mapIndex, _activeNode.id);
        bool cached = _shopStates.TryGetValue(key, out var state);
        if (!cached) _shopStates.Add(key, state = new ShopCheckpointState());
        _activeShopState = state;
        _activeShopOffers = state.Offers;
        _usedSpecialShopOffers = state.UsedServices;
        _purchasedConsumablesInShop = state.PurchasedConsumables;
        _madeSuccessfulPurchaseInCurrentShop = state.MadePricedPurchase;
        if (cached) return;
        ResolvePendingInvestments(_activeNode);
        if (IsTutorialRun)
        {
            PrepareTutorialShopOffers();
            return;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevModeRuntime.UnlimitedShopOffers)
        {
            PrepareAllDevShopOffers(cards);
            AddSpecialShopOffers(unchecked(_activeNode.mapIndex * 397 ^ _activeNode.id));
            return;
        }
#endif
        int context = unchecked(_activeNode.mapIndex * 397 ^ _activeNode.id);
        if (maximumOffers > 0) _activeShopOffers.AddRange(GenerateShopGoods(0));
        AddSpecialShopOffers(context);
    }

    // Each Goods slot is Gear or a Trick (GameBalance: 75% Gear). Gear rolls its rarity by weight
    // (6:3:1 Common, Rare, Epic), then a Gear of that rarity; Legendary Gear is never sold. Owned Gear
    // and Gear tiers are not considered: every Gear is sold as a standalone item.

    List<ShopOffer> GenerateShopGoods(int generation)
    {
        var cards = CardManager.Instance;
        var result = new List<ShopOffer>();
        int context = unchecked((_activeNode.mapIndex * 397 ^ _activeNode.id) + generation * 104729);
        var slots = IsPreHeadlinerBlackMarket(_activeNode) ? preHeadlinerShopGoods : midShopGoods;
        int goodsCount = Mathf.Clamp(slots.x + slots.y + slots.z, 1, 20);
        bool WasBought(ShopOfferKind kind, string id) => _activeShopState != null &&
            _activeShopState.PurchasedGoods.Contains(ShopOffer.PersistentGoodsPrefix(kind) + ":" + id);
        var gearPool = cards.gearCatalog.Where(value => value != null && !string.IsNullOrWhiteSpace(value.id) &&
            !WasBought(ShopOfferKind.Gear, value.id))
            .OrderBy(value => value.id, StringComparer.Ordinal).ToList();
        var trickPool = cards.consumableCatalog.Where(value => value != null && value.shopAvailable &&
            !WasBought(ShopOfferKind.Consumable, value.id) && !_purchasedConsumablesInShop.Contains(value.id))
            .OrderBy(value => value.id, StringComparer.Ordinal).ToList();
        var kindRandom = _randomContext.CreateStream("shop-goods-kind", context);
        var rarityRandom = _randomContext.CreateStream("shop-gear-rarity", context);
        var gearRandom = _randomContext.CreateStream("shop-artifacts", context);
        var trickRandom = _randomContext.CreateStream("shop-consumables", context);
        for (int slot = 0; slot < goodsCount; slot++)
        {
            bool gearSlot = kindRandom.NextInt(0, 100) < GameBalance.Current.blackMarket.gearSlotPercent;
            var gear = gearSlot ? TakeShopGear(gearPool, rarityRandom, gearRandom) : null;
            if (gear != null)
            {
                result.Add(new ShopOffer { kind = ShopOfferKind.Gear, gear = gear,
                    basePrice = gear.price, price = CalculateEffectiveShopPrice(ShopOfferKind.Gear, gear.price), generation = generation });
                continue;
            }
            // A Trick slot, or a Gear slot with no Gear left to sell.
            if (trickPool.Count == 0)
            {
                if (!gearSlot && (gear = TakeShopGear(gearPool, rarityRandom, gearRandom)) != null)
                    result.Add(new ShopOffer { kind = ShopOfferKind.Gear, gear = gear,
                        basePrice = gear.price, price = CalculateEffectiveShopPrice(ShopOfferKind.Gear, gear.price), generation = generation });
                continue;
            }
            int index = trickRandom.NextInt(0, trickPool.Count);
            var consumable = trickPool[index];
            trickPool.RemoveAt(index);
            result.Add(new ShopOffer { kind = ShopOfferKind.Consumable, consumable = consumable,
                basePrice = consumable.price, price = CalculateEffectiveShopPrice(ShopOfferKind.Consumable, consumable.price), generation = generation });
        }
        return result;
    }

    // Rolls a rarity, then takes one Gear of it out of the pool. A rarity with none left falls
    // back to the next rarity in Common, Rare, Epic order, wrapping round.
    static GearData TakeShopGear(List<GearData> pool, IRandomSource rarityRandom, IRandomSource gearRandom)
    {
        var weights = GameBalance.Current.blackMarket.gearRarityWeights;
        int totalWeight = weights.Sum(entry => Math.Max(0, entry.weight));
        if (totalWeight <= 0) return null;
        int roll = rarityRandom.NextInt(0, totalWeight);
        int rolled = 0;
        while (roll >= Math.Max(0, weights[rolled].weight)) roll -= Math.Max(0, weights[rolled++].weight);
        for (int offset = 0; offset < weights.Length; offset++)
        {
            string rarity = weights[(rolled + offset) % weights.Length].rarity;
            var candidates = pool.Where(value => string.Equals(value.rarity, rarity, StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidates.Count == 0) continue;
            var gear = candidates[gearRandom.NextInt(0, candidates.Count)];
            pool.Remove(gear);
            return gear;
        }
        return null;
    }

    public bool CanRefreshGoods => !GameplayInputGate.IsBlocked && !_shopTransaction && IsActiveShopForSale() &&
        _activeShopState != null && CardManager.Instance != null && CardManager.Instance.CanAfford(GoodsRefreshPrice) &&
        _activeShopState.RefreshCount < int.MaxValue;

    public bool RefreshGoods(int expectedRefreshCount)
    {
        if (!CanRefreshGoods || expectedRefreshCount != GoodsRefreshCount) return false;
        _shopTransaction = true;
        try
        {
            int next = GoodsRefreshCount + 1;
            var replacement = GenerateShopGoods(next);
            if (!CardManager.Instance.SpendCredits(GoodsRefreshPrice)) return false;
            _activeShopState.RefreshCount = next;
            _activeShopOffers.RemoveAll(offer => !offer.IsService);
            _activeShopOffers.InsertRange(0, replacement);
            return true;
        }
        finally { _shopTransaction = false; OnShopOffersChanged?.Invoke(); SaveGameService.Checkpoint(this); }
    }

    void AddSpecialShopOffers(int context)
    {
        var services = new List<ShopOfferKind> { ShopOfferKind.Guidance, ShopOfferKind.Contract,
            ShopOfferKind.Investment, ShopOfferKind.Supply };
        services.Shuffle(_randomContext.CreateStream("shop-services", context));
        // Intel scouts the route ahead; the last Black Market before the Headliner has none left.
        bool preHeadliner = IsPreHeadlinerBlackMarket(_activeNode);
        if (preHeadliner) services.Remove(ShopOfferKind.Guidance);
        bool supplyAtMid = _randomContext.CreateStream("shop-supply-checkpoint", _activeNode.mapIndex).NextInt(0, 2) == 0;
        bool isMid = !preHeadliner;
        if (_activeNode.isShopCheckpoint && supplyAtMid == isMid && services.IndexOf(ShopOfferKind.Supply) >= 2)
        {
            services.Remove(ShopOfferKind.Supply);
            services.Insert(0, ShopOfferKind.Supply);
        }
        int offered = Math.Min(services.Count, GameBalance.Current.blackMarket.servicesOffered);
        for (int i = 0; i < offered; i++) _activeShopOffers.Add(ServiceOffer(services[i]));
    }

    // The Black Market checkpoint that leads straight to the Headliner, whatever the route's width.
    bool IsPreHeadlinerBlackMarket(PathNode node) => node != null && node.isShopCheckpoint &&
        node.next.Any(id => FindNode(id)?.kind == MapNodeType.Headliner);

    static ShopOffer ServiceOffer(ShopOfferKind kind)
    {
        var balance = GameBalance.Current.blackMarket;
        int price = kind switch
        {
            ShopOfferKind.Contract => balance.contractPrice,
            ShopOfferKind.Supply => balance.supplyPricePerCard,
            ShopOfferKind.Investment => balance.stakePrice,
            _ => balance.intelPrice
        };
        return new ShopOffer { kind = kind, basePrice = price, price = price };
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void PrepareAllDevShopOffers(CardManager cards)
    {
        foreach (var gear in cards.gearCatalog
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.id))
            .OrderBy(value => value.id, StringComparer.Ordinal))
        {
            _activeShopOffers.Add(new ShopOffer
            {
                kind = ShopOfferKind.Gear,
                gear = gear,
                basePrice = gear.price,
                price = CalculateEffectiveShopPrice(ShopOfferKind.Gear, gear.price)
            });
        }

        foreach (var consumable in cards.consumableCatalog
            .Where(value => value != null && value.shopAvailable)
            .OrderBy(value => value.id, StringComparer.Ordinal))
        {
            _activeShopOffers.Add(new ShopOffer
            {
                kind = ShopOfferKind.Consumable,
                consumable = consumable,
                basePrice = consumable.price,
                price = CalculateEffectiveShopPrice(ShopOfferKind.Consumable, consumable.price)
            });
        }
    }
#endif

    public void OnShopDone()
    {
        if (GameplayInputGate.IsBlocked) return;
        if (_activeNode == null || _activeNode.completed || _activeNode.kind != MapNodeType.Shop || _shopTransaction) return;
        _shopTransaction = true;
        try
        {
            if (_activeShopState != null)
            {
                _activeShopState.MadePricedPurchase = _madeSuccessfulPurchaseInCurrentShop;
                _activeShopState.Completed = true;
            }
            OnHideShop?.Invoke();
            _activeShopOffers = new List<ShopOffer>();
            _activeShopState = null;
            CompleteActiveNode();
        }
        finally { _shopTransaction = false; }
        ShowPathAfterNode();
    }
}
