using System;

/// <summary>Finite, deterministic stat/reward scaling for maps after the authored 12-map run.</summary>
public static class InfiniteRunScaling
{
    public const int FirstInfiniteMapIndex = RunLength.Districts;
    public const int MaximumValue = int.MaxValue;

    public static int ScaleStat(int map12Value, int mapIndex, decimal multiplier)
    {
        if (map12Value <= 0) return 0;
        if (mapIndex < FirstInfiniteMapIndex) return map12Value;
        if (multiplier < 1m)
            throw new ArgumentOutOfRangeException(nameof(multiplier));

        decimal value = Math.Min(map12Value, MaximumValue);
        int steps = mapIndex - FirstInfiniteMapIndex + 1;
        for (int i = 0; i < steps && value < MaximumValue; i++)
            value = Math.Min((decimal)MaximumValue, decimal.Floor(value * multiplier));
        return (int)value;
    }

    public static (int hp, int attack) Scale(int baseHp, int baseAttack, int mapIndex)
    {
        var baseline = EnemyMapScaling.Scale(baseHp, baseAttack,
            Math.Min(RunLength.LastDistrictIndex, Math.Max(0, mapIndex)));
        if (mapIndex < FirstInfiniteMapIndex) return baseline;
        var balance = GameBalance.Current.districtScaling;
        return (ScaleStat(baseline.hp, mapIndex, balance.infiniteHpPercent / 100m),
            ScaleStat(baseline.attack, mapIndex, balance.infiniteAttackPercent / 100m));
    }

    public static int ScaleReward(int baseReward, int mapIndex) =>
        Math.Clamp(baseReward, 0, MaximumValue);
}
