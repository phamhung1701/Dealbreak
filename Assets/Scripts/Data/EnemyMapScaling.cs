using System;

/// <summary>District 1–12 scaling of authored enemy HP and ATK, from GameBalance. Map indices are zero-based.</summary>
public static class EnemyMapScaling
{
    public static (int hp, int attack) Scale(int baseHp, int baseAttack, int mapIndex)
    {
        int index = Math.Min(RunLength.LastDistrictIndex, Math.Max(0, mapIndex));
        var balance = GameBalance.Current.districtScaling;
        int hp = ScaleValue(baseHp, 1m + balance.hpGrowthPercent / 100m * index);
        int attack = ScaleValue(baseAttack, 1m + balance.attackGrowthPercent / 100m * index);
        return (hp, attack);
    }

    static int ScaleValue(int baseValue, decimal multiplier)
    {
        if (baseValue <= 0) return 0;
        decimal value = baseValue * multiplier;
        if (value >= int.MaxValue) return int.MaxValue;
        return (int)decimal.Round(value, 0, MidpointRounding.AwayFromZero);
    }
}
