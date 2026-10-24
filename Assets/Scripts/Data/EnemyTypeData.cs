using UnityEngine;

[CreateAssetMenu(fileName = "Enemy", menuName = "Game/Enemy Type")]
public class EnemyTypeData : ScriptableObject
{
    public string enemyName;
    public int maxHp;
    public int baseAttack;
    [UnityEngine.Serialization.FormerlySerializedAs("goldReward")]
    public int creditsReward;
    [Min(1)] public int encounterCount = 1;
    [Min(0)] public int fleeAfterPlayerTurns;
    public CardData sourceCard;
    [Tooltip("Optional authored portrait. Headliner face-card identity continues to use sourceCard.")]
    public Sprite portraitSprite;
    public EnemyAbility[] abilities;

    // Gameplay rules key on these authored fields and asset references, never on enemyName,
    // which is player-facing copy owned by the terminology workbook.
    [Header("Encounter Composition")]
    [Tooltip("Zero-based District index from which route generation may pick this enemy.")]
    [Min(0)] public int minMapIndex;
    [Tooltip("Whether route generation may use this enemy as the base of a generic Enforcer.")]
    public bool eligibleAsEliteBase = true;
    [Tooltip("Allies that join this enemy in a Street Fight, or in an authored Enforcer encounter.")]
    public EncounterAllySpec[] allies = System.Array.Empty<EncounterAllySpec>();

    public static EnemyTypeData Create(string name, int hp, int atk, int credits)
    {
        var data = CreateInstance<EnemyTypeData>();
        data.enemyName = name;
        data.maxHp = hp;
        data.baseAttack = atk;
        data.creditsReward = credits;
        data.encounterCount = 1;
        return data;
    }
}
