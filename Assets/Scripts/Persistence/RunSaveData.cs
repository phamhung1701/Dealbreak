using System;
using System.Collections.Generic;

// Versioned wire contracts contain values and stable IDs only. No Unity objects, delegates or UI state.
// Legacy gold/artifact/boss keys are the version-1 JSON contract. Runtime code uses Credits/Gear/Headliner;
// Unity migration attributes do not migrate this custom persistence format. Keep these keys and enum numbers.
[Serializable] public sealed class RunSaveData
{
    public int version = RunSaveValidation.Version;
    public int contentVersion = 1;
    public string checkpoint = "safe-node";
    public string seed;
    public int bossIndex, completedNodeCount, activeNodeId = -1;
    public bool infiniteMode;
    public string activeEventId;
    public List<NodeSaveData> nodes = new();
    public List<BossSaveData> bosses = new();
    public CardManagerSaveData cards = new();
    public PlayerSaveData player = new();
    public uint criticalRandom, duelistRandom;
    public int eliteKillCount;
    public long nextActionId;
    public float criticalChance;
    public List<ShopSaveData> shops = new();
    public List<OfferSaveData> upgradeOffers = new();
    public List<int> investmentStakes = new();
    public int paidItemPurchases, combatWins, merchantGiftRewards, satchelRewards, visitedMapIndex;
    public List<int> visitedNodeTypes = new();
    public List<int> consumableDropContexts = new();
    public List<string> resolvedRewardContexts = new();
    public List<AlchemistSaveData> alchemistChoices = new();
    public bool hasContract;
    public ContractSaveData contract = new();
    public TimingSaveData timing = new();
    public int[] midShopGoods = { 2, 2, 1 }, preBossShopGoods = { 1, 1, 3 };
}
[Serializable] public sealed class CounterSaveData { public int key, value; }
[Serializable] public sealed class EffectStateSaveData
{
    public List<CounterSaveData> persistent = new(), encounter = new();
}
[Serializable] public sealed class CardSaveData
{
    public int id, rank, definitionSuit, effectiveSuit, permanentAttack;
    public string enhancementId;
    public EffectStateSaveData effects = new();
}
[Serializable] public sealed class ArtifactSaveData
{
    public long id;
    public string contentId;
    public int preparedCardId, pendingCardId;
    public EffectStateSaveData effects = new();
}
[Serializable] public sealed class ConsumableSlotSaveData
{
    public string contentId;
    public List<int> charges = new();
}
[Serializable] public sealed class CardManagerSaveData
{
    public int gold, nextCardId;
    public long artifactIdWatermark;
    public uint random;
    public List<CardSaveData> owned = new();
    public List<int> deck = new(), hand = new(), discard = new();
    public List<ArtifactSaveData> artifacts = new();
    public List<ConsumableSlotSaveData> backpack = new();
}
[Serializable] public sealed class PlayerSaveData { public int configuredMaxHealth, maxHealth, health, shield, block; }
[Serializable] public sealed class NodeSaveData
{
    public int id, kind, mapIndex;
    public string type, contentId;
    public float col, row;
    public bool completed, accessible, revealed, hidden, shopCheckpoint, scouted;
    public List<int> next = new();
}
[Serializable] public sealed class AbilitySaveData
{
    public string id, displayName, description;
    public int effect, amount;
}
[Serializable] public sealed class BossSaveData
{
    public string name;
    public int rank, suit, hp, attack, gold, encounterCount, fleeTurns;
    public List<AbilitySaveData> abilities = new();
}
[Serializable] public sealed class OfferSaveData
{
    public int kind, slot, basePrice, price, investmentStake, supplyQuantity, generation;
    public string artifactId, enhancementId, consumableId;
    public bool purchased;
}
[Serializable] public sealed class ShopSaveData
{
    public int mapIndex, nodeId, refreshCount;
    public bool madePricedPurchase, completed;
    public List<OfferSaveData> offers = new();
    public List<int> usedServices = new();
    public List<string> purchasedConsumables = new(), purchasedGoods = new();
}
[Serializable] public sealed class AlchemistSaveData { public int mapIndex; public List<string> choices = new(); }
[Serializable] public sealed class ContractSaveData
{
    public int objective, startedMap, startedNode, deadlineMap, encounterWins;
    public string stableId;
}
[Serializable] public sealed class SplitSaveData { public int mapIndex; public double elapsed; }
[Serializable] public sealed class TimingSaveData
{
    public bool active, rolling;
    public int mapIndex;
    public double totalElapsed, mapElapsed;
    public List<SplitSaveData> splits = new();
}
