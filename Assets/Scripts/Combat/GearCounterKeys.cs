/// <summary>
/// Named GameplayEffectState keys for Gear special rules. Values are persisted in version-1 saves
/// (EffectStateSaveData) and must never change. They are negative so they cannot collide with the
/// non-negative effect/rule indices that typed effects use as keys.
/// </summary>
public static class GearCounterKeys
{
    // Persistent counters
    public const int DiamondEpicSuitCount = -108;
    public const int RetaliationEpicGrowth = -111;
    public const int HuntersLedgerEliteKills = -119;
    public const int WanderersBootsRewardedMap = -133;
    public const int HiddenTrailRerolledMap = -135;
    public const int TrophyRackEliteKills = -137;
    public const int HammerLastRewardNode = -140;
    public const int HeartEpicStoredOverheal = -1050;
    public const int BalancersScalePreviousGroup = -1181;
    public const int BalancersScaleBonus = -1182;
    public const int WarpathBannerWinStreak = -1340;

    // Encounter counters
    public const int AceReturnUsed = -114;
    public const int SpadeEpicOverblocks = -1071;
    public const int KingslayerRankMask = -1381;
    public const int KingslayerCharges = -1382;
}
