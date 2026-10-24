using UnityEngine;

// Managers and scene references must exist before UI OnEnable subscriptions.
[DefaultExecutionOrder(-50)]
public class GameController : MonoBehaviour
{
    [Header("References")]
    public Field handField;
    public Canvas dragCanvas;
    public CardView cardPrefab;

    [Header("Content")]
    [Tooltip("The single content registry for play and saves. Defaults to Resources/RunSaveContent.")]
    public SaveContentCatalog contentRegistry;

    [Header("Player Prototype Balance")]
    [Min(1)] public int playerMaxHealth = 30;

    [Header("Enemy Definitions")]
    public EnemyTypeData thiefType;
    public EnemyTypeData goblinType;
    public EnemyTypeData knightType;

    bool _configured;

    void Awake()
    {
        if (handField == null || handField.cardsHolder == null ||
            dragCanvas == null || cardPrefab == null || playerMaxHealth <= 0 ||
            thiefType == null || goblinType == null || knightType == null)
        {
            Debug.LogError("GameController: assign hand, card, canvas, positive player health, and all enemy references before starting a run.", this);
            return;
        }

        // Play and saves read one registry, so content cannot be playable but unloadable.
        var registry = contentRegistry != null ? contentRegistry : SaveContentCatalog.Load();
        if (registry == null)
        {
            Debug.LogError("GameController: the Resources/RunSaveContent content registry is missing.", this);
            return;
        }

        var cards = GetOrCreate<CardManager>();
        var combat = GetOrCreate<CombatManager>();
        var run = GetOrCreate<RunManager>();
        cards.Configure(handField, dragCanvas, cardPrefab, registry.gearItems, registry.enhancements, registry.consumables);
        combat.ConfigurePlayer(playerMaxHealth);
        run.thiefType = thiefType;
        run.goblinType = goblinType;
        run.knightType = knightType;
        run.contentCatalog = registry.runContent;
        // The tutorial runs without the autosave, so it can neither write nor delete the run save.
        if (!FrontendLaunchContext.HasPendingTutorial) SaveGameService.Attach(run);
        _configured = true;
    }

    void Start()
    {
        if (!_configured) return;
        if (FrontendLaunchContext.TryConsumeTutorial())
        {
            TutorialDirector.Begin(RunManager.Instance, TutorialRunDefinition.Load());
            return;
        }
        if (FrontendLaunchContext.TryConsumeContinue(out var saved))
        {
            RestoreSavedRun(saved);
        }
        else if (FrontendLaunchContext.TryConsumeRunSeed(out string seed, out string characterId))
            RunManager.Instance.StartRunWithSeed(seed, SaveContentCatalog.Load()?.Character(characterId));
        else if (!DevModeRuntime.Enabled && SaveGameService.TryRead(out saved, out _))
            RestoreSavedRun(saved);
        else if (!DevModeRuntime.Enabled && SaveGameService.Store.HasActiveFiles)
        {
            SaveGameService.TryRead(out _, out string error);
            GameFlowService.Instance?.ShowMainMenu();
            SavePromptUI.ShowError(error);
        }
        else
            RunManager.Instance.StartRun();
    }

    void RestoreSavedRun(RunSaveData saved)
    {
        if (SaveGameService.Restore(RunManager.Instance, saved, out string error)) return;
        // Never replace a failed Continue with a fresh run or overwrite its recovery files.
        GameFlowService.Instance?.ShowMainMenu();
        SavePromptUI.ShowError(error);
    }

    static T GetOrCreate<T>() where T : Singleton<T>
    {
        var manager = Singleton<T>.Instance;
        return manager != null ? manager : new GameObject($"[{typeof(T).Name}]").AddComponent<T>();
    }
}
