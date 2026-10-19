using UnityEngine;

[CreateAssetMenu(fileName = "RunContentCatalog", menuName = "Game/Run Content Catalog")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "RunContentCatalog")]
public class RunContentCatalog : ScriptableObject
{
    [Header("Encounters")]
    public EnemyTypeData[] normalEnemies;
    [Tooltip("Authored elite bases; do not apply the legacy generic elite bonuses.")]
    public EnemyTypeData[] eliteEnemies;
    public EnemyAbility eliteAbility;

    [Header("Route Content")]
    public RunEventDefinition[] events;
    public RunEventDefinition[] upgrades;
    public RunEventDefinition[] risks;

    public RunEventDefinition[] GetEvents(MapNodeType category) => category switch
    {
        MapNodeType.Upgrade => upgrades,
        MapNodeType.Risk => risks,
        _ => events
    };
}
