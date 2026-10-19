using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "ActionButtonsUI")]
public class ActionButtonsUI : MonoBehaviour
{
    enum EnemyTargetAction { None, Attack, Consumable }

    [Header("References")]
    public Button playButton;
    public Button passButton;
    public Button blockButton;
    public Button rankSortButton;
    public Button suitSortButton;
    public Field handField;
    public Button recoverButton;
    public Button[] backpackSlotButtons = new Button[CardManager.BACKPACK_CAPACITY];
    public TMP_Text playerHealthLabel;
    public TMP_Text turnStateLabel;
    public TMP_Text pendingDamageLabel;
    public TMP_Text selectionLabel;
    public TMP_Text combatLogLabel;

    TMP_Text _primaryActionLabel;
    readonly DisplayedNumber _shownHealth = new();
    Color _healthLabelColor;
    bool _healthLabelColorCaptured;
    HealthBarBacking _healthBar;
    UnityAction[] _backpackSlotListeners;
    ConsumableSlotDragUI[] _backpackSlotDragInputs;
    EnhancementTargetUI _enhancementTargetUI;
    EnemyTargetAction _enemyTargetAction;
    readonly List<CardView> _attackTargetCards = new();
    readonly List<EnemyRuntime> _validEnemyTargets = new();
    int _targetConsumableSlot = -1;
    ConsumableInstance _targetConsumableInstance;
    int _selectedConsumableSlot = -1;
    ConsumableInstance _selectedConsumable;
    GameObject _consumableContextPanel;
    TMP_Text _consumableContextText;
    Button _consumableUseButton;
    Button _consumableSellButton;
    Button _consumableDiscardButton;
    bool _discardConfirmationArmed;
    RectTransform _consumableContextRect;
    EnemyGroupUI _enemyGroup;
    readonly Vector3[] _consumableSlotCorners = new Vector3[4];
    readonly List<CardView> _contextHandViews = new();

    void OnEnable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnStateChanged += HandleStateChanged;
            CombatManager.Instance.OnPendingDamageChanged += HandlePendingDamage;
            CombatManager.Instance.OnPlayerHealthChanged += HandlePlayerHealthChanged;
            CombatManager.Instance.OnCombatLog += HandleCombatLog;
            CombatManager.Instance.OnEnemiesChanged += HandleEnemiesChanged;
            CombatManager.Instance.OnEncounterResult += HandleEncounterResult;
        }
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnCardSelected += HandleSelectionChanged;
            CardManager.Instance.OnDeckChanged += HandleDeckChanged;
            CardManager.Instance.OnConsumablesChanged += HandleDeckChanged;
            CardManager.Instance.OnEnemyTargetingChanged += HandleEnemyTargetingChanged;
        }
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += HandleRunStarted;
            RunManager.Instance.OnHideShop += HandleContextEnded;
            RunManager.Instance.OnShowShop += HandleShopContextStarted;
            RunManager.Instance.OnShowPathScreen += HandleContextEnded;
            RunManager.Instance.OnHidePathScreen += HandleContextEnded;
        }

        _primaryActionLabel = playButton != null ? playButton.GetComponentInChildren<TMP_Text>(true) : null;
        _enhancementTargetUI = GetComponent<EnhancementTargetUI>();
        _enemyGroup = FindFirstObjectByType<EnemyGroupUI>();

        if (playButton) { playButton.onClick.AddListener(OnPlayClicked); KeyButtonBorderStyle.Apply(playButton); }
        if (passButton) { passButton.onClick.AddListener(OnPassClicked); KeyButtonBorderStyle.Apply(passButton); }
        if (blockButton) { blockButton.onClick.AddListener(OnBlockClicked); KeyButtonBorderStyle.Apply(blockButton); }
        if (rankSortButton) rankSortButton.onClick.AddListener(OnRankSortClicked);
        if (suitSortButton) suitSortButton.onClick.AddListener(OnSuitSortClicked);
        if (recoverButton) { recoverButton.onClick.AddListener(OnRecoverClicked); KeyButtonBorderStyle.Apply(recoverButton); }
        BindBackpackSlots();
        EnsureConsumableContextUI();

        RefreshAll();
    }

    void OnDisable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnStateChanged -= HandleStateChanged;
            CombatManager.Instance.OnPendingDamageChanged -= HandlePendingDamage;
            CombatManager.Instance.OnPlayerHealthChanged -= HandlePlayerHealthChanged;
            CombatManager.Instance.OnCombatLog -= HandleCombatLog;
            CombatManager.Instance.OnEnemiesChanged -= HandleEnemiesChanged;
            CombatManager.Instance.OnEncounterResult -= HandleEncounterResult;
        }
        var run = RunManager.Instance;
        if (run != null)
        {
            run.OnRunStarted -= HandleRunStarted;
            run.OnHideShop -= HandleContextEnded;
            run.OnShowShop -= HandleShopContextStarted;
            run.OnShowPathScreen -= HandleContextEnded;
            run.OnHidePathScreen -= HandleContextEnded;
        }
        var cards = CardManager.Instance;
        if (cards != null)
        {
            cards.OnCardSelected -= HandleSelectionChanged;
            cards.OnDeckChanged -= HandleDeckChanged;
            cards.OnConsumablesChanged -= HandleDeckChanged;
            cards.OnEnemyTargetingChanged -= HandleEnemyTargetingChanged;
        }

        if (playButton) playButton.onClick.RemoveListener(OnPlayClicked);
        if (passButton) passButton.onClick.RemoveListener(OnPassClicked);
        if (blockButton) blockButton.onClick.RemoveListener(OnBlockClicked);
        if (rankSortButton) rankSortButton.onClick.RemoveListener(OnRankSortClicked);
        if (suitSortButton) suitSortButton.onClick.RemoveListener(OnSuitSortClicked);
        if (recoverButton) recoverButton.onClick.RemoveListener(OnRecoverClicked);
        UnbindBackpackSlots();
        CancelEnemyTargeting();
        ClearSelectedConsumable();
    }

    void Update()
    {
        if (CardManager.Instance?.IsEnemyTargeting == true)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                CancelEnemyTargeting();
            RefreshEnemyTargetPresentation();
        }
        RefreshBoardGuidance();
        if (_shownHealth.Tick(Time.unscaledDeltaTime)) RefreshPlayerHealthLabel(CombatManager.Instance);
    }

    void LateUpdate()
    {
        // Style passes repaint labels; the HP label must stay readable on its bar.
        if (_healthLabelColorCaptured && playerHealthLabel)
            playerHealthLabel.color = _shownHealth.Tint(_healthLabelColor);
        if (_consumableContextPanel && _consumableContextPanel.activeSelf) PositionConsumableContext();
    }

    // These labels support encounters, not the Shop/Event objects occupying the same surface.
    void RefreshBoardGuidance()
    {
        var node = RunManager.Instance?.ActiveNode;
        bool encounter = node != null && (node.kind == MapNodeType.Combat ||
            node.kind == MapNodeType.Elite || node.kind == MapNodeType.Headliner);
        if (turnStateLabel && turnStateLabel.enabled != encounter) turnStateLabel.enabled = encounter;
        if (combatLogLabel && combatLogLabel.gameObject.activeSelf) combatLogLabel.gameObject.SetActive(false);
        bool showSelection = node == null || encounter;
        if (selectionLabel && selectionLabel.enabled != showSelection) selectionLabel.enabled = showSelection;
    }

    void HandleRunStarted(string _)
    {
        _enhancementTargetUI?.Hide();
        CancelEnemyTargeting();
        ClearSelectedConsumable();
        RefreshAll();
    }

    void HandleContextEnded()
    {
        CancelEnemyTargeting();
        ClearSelectedConsumable();
        RefreshAll();
    }

    void HandleShopContextStarted() => RefreshAll();

    void HandleEnemyTargetingChanged(bool active)
    {
        if (!active && _enemyTargetAction != EnemyTargetAction.None)
        {
            bool clearConsumable = _enemyTargetAction == EnemyTargetAction.Consumable;
            _enemyTargetAction = EnemyTargetAction.None;
            _attackTargetCards.Clear();
            _validEnemyTargets.Clear();
            _targetConsumableSlot = -1;
            _targetConsumableInstance = null;
            if (clearConsumable) ClearSelectedConsumable();
        }
        RefreshEnemyTargetPresentation();
        RefreshAll();
    }

    void HandleEnemiesChanged()
    {
        if (_enemyTargetAction == EnemyTargetAction.None) return;
        _validEnemyTargets.Clear();
        var combat = CombatManager.Instance;
        if (combat != null && _enemyTargetAction == EnemyTargetAction.Attack)
        {
            foreach (var enemy in combat.Enemies)
                if (enemy != null && combat.CanPlayCards(_attackTargetCards, enemy)) _validEnemyTargets.Add(enemy);
        }
        else if (_enemyTargetAction == EnemyTargetAction.Consumable)
        {
            var cards = CardManager.Instance;
            if (cards != null && cards.GetConsumableInstanceAtSlot(_targetConsumableSlot) == _targetConsumableInstance)
                _validEnemyTargets.AddRange(cards.GetValidEnemyTargetsForConsumableAtSlot(_targetConsumableSlot));
        }
        if (_validEnemyTargets.Count == 0) CancelEnemyTargeting();
        else RefreshEnemyTargetPresentation();
    }

    void HandleEncounterResult(EncounterResult _) => HandleContextEnded();
    void HandleStateChanged(GameState _)
    {
        var combat = CombatManager.Instance;
        if (_enemyTargetAction != EnemyTargetAction.None && combat != null &&
            combat.currentState is not (GameState.PlayerTurn or GameState.EnemyAttacking))
            CancelEnemyTargeting();
        RefreshAll();
    }
    void HandlePendingDamage(int _) => RefreshAll();
    void HandleSelectionChanged(CardView view)
    {
        if (view != null) ClearSelectedConsumable();
        RefreshAll();
    }
    void HandleDeckChanged()
    {
        // Source removal/reordering and hand mutations invalidate the captured action, not just its label.
        if (_enemyTargetAction != EnemyTargetAction.None) HandleEnemiesChanged();
        RefreshAll();
    }
    void HandlePlayerHealthChanged(int _, int __) => RefreshAll();

    // The combat log is kept for the Console and telemetry; the board shows state, not narration.
    void HandleCombatLog(string message)
    {
        if (combatLogLabel && combatLogLabel.gameObject.activeSelf) combatLogLabel.gameObject.SetActive(false);
        RefreshAll();
    }

    // HP changes count over a few frames with a loss/gain tint in play mode; the value itself is
    // always read from the player.
    void RefreshPlayerHealthLabel(CombatManager combat)
    {
        if (!playerHealthLabel || combat == null) return;
        if (!_healthLabelColorCaptured)
        {
            // The label sits on the HP bar, so it reads in paper over brick and ink.
            var theme = DealbreakUIStyle.Theme;
            _healthLabelColor = theme ? theme.paper : Color.white;
            _healthLabelColorCaptured = true;
        }
        _shownHealth.SetTarget(combat.player.currentHealth, Application.isPlaying && isActiveAndEnabled);
        string block = combat.player.EncounterBlock > 0 ? $"  •  BLOCK {combat.player.EncounterBlock}" : string.Empty;
        string shield = combat.player.ShieldCharges > 0 ? $"  •  SHIELD {combat.player.ShieldCharges}" : string.Empty;
        playerHealthLabel.text = combat.HasInfiniteHealthForDev
            ? $"HP  ∞{block}{shield}"
            : $"HP  {_shownHealth.Shown}/{combat.player.maxHealth}{block}{shield}";
        playerHealthLabel.color = _shownHealth.Tint(_healthLabelColor);
        if (_healthBar == null) _healthBar = HealthBarBacking.Attach(playerHealthLabel, true);
        if (_healthBar != null)
            _healthBar.SetFraction(combat.HasInfiniteHealthForDev || combat.player.maxHealth <= 0 ? 1f
                : _shownHealth.Value / combat.player.maxHealth);
    }

    /// <summary>The player's HP bar, a drop target for healing Tricks.</summary>
    public RectTransform PlayerHealthBar => _healthBar != null ? _healthBar.Bar : null;

    void RefreshAll()
    {
        var combat = CombatManager.Instance;
        var cards = CardManager.Instance;
        if (combat == null || cards == null) return;

        int selectedCount = cards.SelectedCards.Count;
        bool hasSelected = selectedCount > 0;
        bool canPlay = GetValidAttackTargets(cards, combat).Count > 0;
        bool canDefend = combat.CanDefendWithCards(cards.SelectedCards);
        bool isDefense = combat.currentState == GameState.EnemyAttacking;
        bool canTakeDamage = isDefense && combat.pendingDamage > 0;
        bool canRecover = combat.currentState == GameState.PlayerTurn && cards.HandCount == 0;

        bool targeting = cards.IsHandEnhancementTargeting || cards.IsEnemyTargeting;
        if (playButton) playButton.interactable = !GameplayInputGate.IsBlocked &&
            (cards.IsEnemyTargeting || !targeting && combat.currentState == GameState.PlayerTurn && canPlay);
        if (passButton)
        {
            bool combatPhase = combat.currentState is GameState.PlayerTurn or GameState.EnemyAttacking;
            if (passButton.gameObject.activeSelf != combatPhase) passButton.gameObject.SetActive(combatPhase);
            passButton.interactable = !GameplayInputGate.IsBlocked && !targeting &&
                (isDefense ? canTakeDamage : combat.CanPassPlayerTurn);
        }
        if (blockButton) blockButton.interactable = !targeting && isDefense && canDefend;
        if (rankSortButton) rankSortButton.interactable = cards.HandCount > 1;
        if (suitSortButton) suitSortButton.interactable = cards.HandCount > 1;
        // Emphasis follows the combat state; command availability remains authoritative above.
        if (playButton && playButton.targetGraphic)
            playButton.targetGraphic.color = combat.currentState == GameState.PlayerTurn
                ? new Color(0.26f, 0.48f, 0.38f, 1f) : new Color(0.14f, 0.22f, 0.23f, 0.3f);
        if (blockButton && blockButton.targetGraphic)
            blockButton.targetGraphic.color = isDefense
                ? new Color(0.32f, 0.48f, 0.64f, 1f) : new Color(0.14f, 0.22f, 0.23f, 0.3f);
        if (_primaryActionLabel)
        {
            _primaryActionLabel.text = cards.IsEnemyTargeting ? "Cancel" : combat.currentState switch
            {
                GameState.PlayerTurn when GameplayEffectResolver.IsRoyalFamilySelection(cards.SelectedCards.Select(view => view.data).ToArray(), cards) => "ATTACK ROYAL FAMILY",
                GameState.PlayerTurn when selectedCount == 2 && GameplayEffectResolver.CanPlayAsAcePair(cards.SelectedCards) => "ATTACK ACE PAIR",
                GameState.PlayerTurn when selectedCount >= 2 && GameplayEffectResolver.IsSameRankMultiCardAction(cards.SelectedCards, cards) => $"ATTACK SAME-RANK {selectedCount}",
                GameState.PlayerTurn => "ATTACK",
                GameState.EnemyAttacking => "CONFIRM BLOCK",
                _ => "ACTION"
            };
        }
        if (recoverButton) recoverButton.interactable = !targeting && canRecover;

        RefreshBackpackSlots(cards, combat);

        RefreshPlayerHealthLabel(combat);

        if (turnStateLabel)
        {
            turnStateLabel.text = combat.currentState switch
            {
                GameState.PlayerTurn => combat.IsBonusPlayerAction ? "PLAYER TURN  •  BONUS ACTION" : "PLAYER TURN",
                GameState.EnemyAttacking => "DEFENSE",
                GameState.GameWon => "ENCOUNTER WON",
                GameState.GameOver => "RUN DEFEAT",
                _ => "CHOOSE A ROUTE"
            };
        }

        if (pendingDamageLabel)
        {
            bool showDamage = combat.currentState == GameState.EnemyAttacking;
            pendingDamageLabel.gameObject.SetActive(showDamage);
            pendingDamageLabel.text = showDamage
                ? combat.pendingDamage > 0
                    ? $"INCOMING {combat.pendingDamage}"
                    : "ALL BLOCKED"
                : string.Empty;
        }

        if (selectionLabel)
        {
        // Only what the current selection will do; how to play is left to the tutorial.
        if (cards.IsEnemyTargeting || cards.IsHandEnhancementTargeting) selectionLabel.text = string.Empty;
        else selectionLabel.text = combat.currentState switch
            {
                GameState.PlayerTurn when cards.HandCount == 0 =>
                    $"Recover: take {combat.TotalEnemyAttack}, draw 1",
                GameState.PlayerTurn when GameplayEffectResolver.IsRoyalFamilySelection(cards.SelectedCards.Select(view => view.data).ToArray(), cards) =>
                    "Royal Family • instant defeat",
                GameState.PlayerTurn when GameplayEffectResolver.IsRoyalFamilyPartialSelection(cards.SelectedCards, cards) =>
                    $"Royal Family {selectedCount}/5",
                GameState.PlayerTurn when selectedCount == 2 && GameplayEffectResolver.CanPlayAsAcePair(cards.SelectedCards) =>
                    $"Ace pair • {DamagePreview(cards, combat)}",
                GameState.PlayerTurn when selectedCount >= 2 && GameplayEffectResolver.IsSameRankMultiCardAction(cards.SelectedCards, cards) =>
                    $"Same rank ×{selectedCount} • {DamagePreview(cards, combat)}",
                GameState.PlayerTurn when hasSelected => DamagePreview(cards, combat),
                GameState.EnemyAttacking when hasSelected && combat.TryPreviewDefense(cards.SelectedCards, out int blocked, out int remaining) =>
                    $"Blocks {blocked} of {combat.PendingAttackCount} • {remaining} damage left",
                GameState.EnemyAttacking when hasSelected => "Can't block with this selection",
                _ => string.Empty
            };
        }
    }

    // Shares the execution calculation; the critical roll and enemy defenses are not included.
    static string DamagePreview(CardManager cards, CombatManager combat)
    {
        var selected = cards.SelectedCards.Select(view => view.data).ToArray();
        int damage = combat.PreviewActionDamage(selected, combat.currentEnemy);
        return GameplayEffectResolver.HasDoubleStrikeEffect(selected)
            ? $"{damage} damage ×2 hits" : $"{damage} damage";
    }

    void OnPrimaryActionClicked()
    {
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        if (cards == null || combat == null) return;

        var selection = cards.GetSelectedCardsSnapshot();
        if (combat.currentState == GameState.PlayerTurn)
            StartSelectedAttack(cards, combat);
        else if (combat.currentState == GameState.EnemyAttacking)
            CombatCommands.Block(selection, combat.currentEnemy);
    }

    void OnPlayClicked()
    {
        if (GameplayInputGate.IsBlocked) return;
        if (CardManager.Instance?.IsEnemyTargeting == true)
        {
            CancelEnemyTargeting();
            return;
        }
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        if (cards != null && combat != null && combat.currentState == GameState.PlayerTurn)
            StartSelectedAttack(cards, combat);
    }

    List<EnemyRuntime> GetValidAttackTargets(CardManager cards, CombatManager combat)
    {
        var targets = new List<EnemyRuntime>();
        if (cards == null || combat == null || cards.SelectedCards.Count == 0 ||
            combat.currentState != GameState.PlayerTurn) return targets;
        foreach (var enemy in combat.Enemies)
            if (enemy != null && combat.CanPlayCards(cards.SelectedCards, enemy)) targets.Add(enemy);
        return targets;
    }

    void StartSelectedAttack(CardManager cards, CombatManager combat)
    {
        if (GameplayInputGate.IsBlocked || cards.IsHandEnhancementTargeting || cards.IsEnemyTargeting) return;
        ClearSelectedConsumable();
        var selection = cards.GetSelectedCardsSnapshot();
        var targets = GetValidAttackTargets(cards, combat);
        if (targets.Count == 1)
        {
            combat.SelectEnemyTarget(targets[0]);
            ExecuteAttack(selection, targets[0], combat);
            return;
        }
        if (targets.Count <= 1) return;
        _enemyTargetAction = EnemyTargetAction.Attack;
        _validEnemyTargets.Clear();
        _validEnemyTargets.AddRange(targets);
        _attackTargetCards.Clear();
        _attackTargetCards.AddRange(selection);
        cards.BeginEnemyTargeting(HandleEnemyTargetChosen, IsCurrentEnemyTargetValid);
        RefreshEnemyTargetPresentation();
        RefreshAll();
    }

    bool IsCurrentEnemyTargetValid(EnemyRuntime target)
    {
        if (target == null || !_validEnemyTargets.Contains(target)) return false;
        var combat = CombatManager.Instance;
        if (combat == null) return false;
        if (_enemyTargetAction == EnemyTargetAction.Attack)
            return combat.CanPlayCards(_attackTargetCards, target);
        if (_enemyTargetAction == EnemyTargetAction.Consumable)
        {
            var cards = CardManager.Instance;
            return cards != null && cards.GetConsumableInstanceAtSlot(_targetConsumableSlot) == _targetConsumableInstance &&
                cards.GetValidEnemyTargetsForConsumableAtSlot(_targetConsumableSlot).Contains(target);
        }
        return false;
    }

    void HandleEnemyTargetChosen(EnemyRuntime target)
    {
        if (!IsCurrentEnemyTargetValid(target)) return;
        var action = _enemyTargetAction;
        var combat = CombatManager.Instance;
        var cards = CardManager.Instance;
        var attackCards = _attackTargetCards.ToArray();
        int consumableSlot = _targetConsumableSlot;
        var consumableInstance = _targetConsumableInstance;
        CancelEnemyTargeting();
        bool resolved = false;
        if (action == EnemyTargetAction.Attack && combat != null)
        {
            combat.SelectEnemyTarget(target);
            resolved = ExecuteAttack(attackCards, target, combat);
        }
        else if (action == EnemyTargetAction.Consumable && cards != null && combat != null &&
            cards.GetConsumableInstanceAtSlot(consumableSlot) == consumableInstance)
        {
            combat.SelectEnemyTarget(target);
            resolved = cards.UseConsumableAtSlot(consumableSlot, combat, target);
        }
        if (action == EnemyTargetAction.Consumable) ClearSelectedConsumable();
        if (!resolved) RefreshAll();
    }

    bool ExecuteAttack(IReadOnlyList<CardView> selection, EnemyRuntime target, CombatManager combat) =>
        CombatCommands.Attack(selection, target);

    void CancelEnemyTargeting()
    {
        bool clearConsumable = _enemyTargetAction == EnemyTargetAction.Consumable;
        _enemyTargetAction = EnemyTargetAction.None;
        _attackTargetCards.Clear();
        _validEnemyTargets.Clear();
        _targetConsumableSlot = -1;
        _targetConsumableInstance = null;
        CardManager.Instance?.EndEnemyTargeting();
        if (clearConsumable) ClearSelectedConsumable();
        RefreshEnemyTargetPresentation();
        RefreshAll();
    }

    void RefreshEnemyTargetPresentation()
    {
        if (_enemyGroup == null) _enemyGroup = FindFirstObjectByType<EnemyGroupUI>();
        if (_enemyGroup == null) return;
        bool targeting = CardManager.Instance?.IsEnemyTargeting == true;
        foreach (var view in _enemyGroup.ActiveViews)
        {
            if (!view) continue;
            var enemy = view.DisplayedEnemy;
            view.SetEnemyTargeting(targeting, targeting && enemy != null && _validEnemyTargets.Contains(enemy));
        }
    }

    void OnBlockClicked()
    {
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        if (cards != null && combat != null && combat.currentState == GameState.EnemyAttacking)
        {
            CombatCommands.Block(cards.GetSelectedCardsSnapshot(), combat.currentEnemy);
        }
    }

    void OnRankSortClicked() => (handField != null ? handField : CardManager.Instance?.handField)?.SortByRank();
    void OnSuitSortClicked() => (handField != null ? handField : CardManager.Instance?.handField)?.SortBySuit();

    // Kept for existing serialized UnityEvent bindings and interaction characterization.
    void OnDiscardClicked() => OnBlockClicked();

    void OnPassClicked()
    {
        var combat = CombatManager.Instance;
        var cards = CardManager.Instance;
        if (combat == null || GameplayInputGate.IsBlocked ||
            cards != null && (cards.IsEnemyTargeting || cards.IsHandEnhancementTargeting)) return;
        if (combat.currentState == GameState.EnemyAttacking) combat.TakeRemainingDamage();
        else combat.PassPlayerTurn();
        RefreshAll();
    }

    void OnRecoverClicked() => CombatManager.Instance?.Recover();

    void BindBackpackSlots()
    {
        UnbindBackpackSlots();
        if (backpackSlotButtons == null) return;
        _backpackSlotListeners = new UnityAction[backpackSlotButtons.Length];
        _backpackSlotDragInputs = new ConsumableSlotDragUI[backpackSlotButtons.Length];
        for (int i = 0; i < backpackSlotButtons.Length; i++)
        {
            if (!backpackSlotButtons[i]) continue;
            int slotIndex = i;
            var dragInput = backpackSlotButtons[i].GetComponent<ConsumableSlotDragUI>();
            if (!dragInput) dragInput = backpackSlotButtons[i].gameObject.AddComponent<ConsumableSlotDragUI>();
            dragInput.Configure(this, slotIndex);
            _backpackSlotDragInputs[i] = dragInput;
            _backpackSlotListeners[i] = () =>
            {
                if (_backpackSlotDragInputs[slotIndex] != null &&
                    _backpackSlotDragInputs[slotIndex].ConsumeClickSuppression()) return;
                SelectBackpackSlot(slotIndex);
            };
            backpackSlotButtons[i].onClick.AddListener(_backpackSlotListeners[i]);
        }
    }

    void UnbindBackpackSlots()
    {
        if (_backpackSlotListeners == null || backpackSlotButtons == null) return;
        int count = Mathf.Min(_backpackSlotListeners.Length, backpackSlotButtons.Length);
        for (int i = 0; i < count; i++)
            if (backpackSlotButtons[i] && _backpackSlotListeners[i] != null)
                backpackSlotButtons[i].onClick.RemoveListener(_backpackSlotListeners[i]);
        _backpackSlotListeners = null;
        _backpackSlotDragInputs = null;
    }

    public void HandleConsumableSlotDrop(int sourceSlot, PointerEventData eventData)
    {
        if (GameplayInputGate.IsBlocked || CardManager.Instance == null || EventSystem.current == null ||
            CardManager.Instance.IsHandEnhancementTargeting) return;
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        CardActionDropTarget enemyTarget = null;
        int destinationSlot = -1;
        for (int i = 0; i < results.Count; i++)
        {
            var hit = results[i].gameObject;
            var targetCard = hit.GetComponentInParent<CardView>();
            if (targetCard != null)
            {
                var cards = CardManager.Instance;
                if (cards.GetConsumableAtSlot(sourceSlot)?.effectType == ConsumableEffectType.ApplyEnhancement)
                    return; // Enhancement Tricks require card-first selection followed by explicit Use confirmation.
                if (cards.GetConsumableTargetMaximum(sourceSlot) > 0 &&
                    targetCard.data != null && cards.CanTargetConsumableAtSlot(sourceSlot, targetCard.data.Id))
                {
                    _enhancementTargetUI?.OpenConsumableTarget(sourceSlot);
                    if (_enhancementTargetUI != null && _enhancementTargetUI.IsTargeting)
                        cards.HandleCardClick(targetCard);
                }
                return;
            }
            var dropTarget = hit.GetComponentInParent<CardActionDropTarget>();
            if (dropTarget != null)
            {
                enemyTarget = dropTarget;
                break;
            }
            for (int slot = 0; slot < backpackSlotButtons.Length; slot++)
                if (backpackSlotButtons[slot] && (hit == backpackSlotButtons[slot].gameObject ||
                    hit.transform.IsChildOf(backpackSlotButtons[slot].transform)))
                {
                    destinationSlot = slot;
                    break;
                }
            if (destinationSlot >= 0) break;
        }
        if (enemyTarget != null)
            enemyTarget.TryUseConsumableAtSlot(sourceSlot);
        else if (destinationSlot >= 0)
            CardManager.Instance.MoveConsumableSlot(sourceSlot, destinationSlot);
    }

    void SelectBackpackSlot(int slotIndex)
    {
        if (GameplayInputGate.IsBlocked) return;
        var cards = CardManager.Instance;
        var item = cards?.GetConsumableInstanceAtSlot(slotIndex);
        if (cards == null || item == null || cards.IsHandEnhancementTargeting || cards.IsEnemyTargeting) return;
        if (cards.GetConsumableAtSlot(slotIndex)?.effectType != ConsumableEffectType.ApplyEnhancement)
            cards.ClearSelection();
        FindFirstObjectByType<GearRailUI>()?.ClearObjectSelection();
        if (_selectedConsumableSlot == slotIndex && ReferenceEquals(_selectedConsumable, item))
            ClearSelectedConsumable();
        else
        {
            _discardConfirmationArmed = false;
            _selectedConsumableSlot = slotIndex;
            _selectedConsumable = item;
            RefreshSelectedConsumableContext();
        }
        RefreshBackpackSlots(cards, CombatManager.Instance);
    }

    void EnsureConsumableContextUI()
    {
        if (_consumableContextPanel || backpackSlotButtons == null || backpackSlotButtons.Length == 0 ||
            !backpackSlotButtons[0]) return;
        var parent = backpackSlotButtons[0].GetComponentInParent<Canvas>()?.transform ?? backpackSlotButtons[0].transform.parent;
        if (!(parent is RectTransform)) return;
        _consumableContextPanel = new GameObject("Selected Consumable Actions",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
        _consumableContextPanel.transform.SetParent(parent, false);
        var contextCanvas = _consumableContextPanel.GetComponent<Canvas>();
        contextCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        contextCanvas.overrideSorting = true;
        contextCanvas.sortingOrder = 65;
        _consumableContextRect = (RectTransform)_consumableContextPanel.transform;
        _consumableContextRect.anchorMin = _consumableContextRect.anchorMax = new Vector2(0.5f, 0.5f);
        _consumableContextRect.pivot = new Vector2(0.5f, 0.5f);
        _consumableContextRect.sizeDelta = new Vector2(270f, 150f);
        ContextualTooltipUI.RegisterObstacle(_consumableContextRect);
        var background = _consumableContextPanel.GetComponent<Image>();
        background.color = new Color(0.08f, 0.12f, 0.16f, 0.96f);
        background.raycastTarget = true;

        var textObject = new GameObject("Consumable Details", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(_consumableContextPanel.transform, false);
        var textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 64f);
        textRect.offsetMax = new Vector2(-12f, -10f);
        _consumableContextText = textObject.GetComponent<TMP_Text>();
        var styleSource = selectionLabel != null ? selectionLabel : backpackSlotButtons[0].GetComponentInChildren<TMP_Text>(true);
        if (styleSource != null) _consumableContextText.font = styleSource.font;
        _consumableContextText.fontSize = 18f;
        _consumableContextText.enableAutoSizing = false;
        _consumableContextText.fontSizeMin = 14f;
        _consumableContextText.fontSizeMax = 19f;
        _consumableContextText.alignment = TextAlignmentOptions.TopLeft;
        _consumableContextText.raycastTarget = false;

        _consumableUseButton = CreateConsumableActionButton("Use Trick", "USE", new Vector2(-88f, 10f));
        _consumableSellButton = CreateConsumableActionButton("Sell Trick", "SELL", Vector2.zero);
        _consumableDiscardButton = CreateConsumableActionButton("Discard Trick", "DISCARD", new Vector2(88f, 10f));
        _consumableUseButton.onClick.AddListener(UseSelectedConsumable);
        _consumableSellButton.onClick.AddListener(SellOrCancelSelectedConsumable);
        _consumableDiscardButton.onClick.AddListener(DiscardSelectedConsumable);
        DealbreakUIStyle.Frame(_consumableContextPanel.transform);
        _consumableContextPanel.SetActive(false);
    }

    Button CreateConsumableActionButton(string objectName, string label, Vector2 position)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(_consumableContextPanel.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(82f, 44f);
        var image = go.GetComponent<Image>();
        image.color = new Color(0.22f, 0.43f, 0.34f, 1f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        if (objectName == "Use Trick") KeyButtonBorderStyle.Apply(button);
        button.colors = new ColorBlock { normalColor = Color.white, highlightedColor = new Color(1.1f, 1.1f, 1.1f),
            pressedColor = new Color(0.8f, 0.8f, 0.8f), selectedColor = Color.white,
            disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.48f), colorMultiplier = 1f, fadeDuration = 0.1f };
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(rect, false);
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(4f, 2f);
        labelRect.offsetMax = new Vector2(-4f, -2f);
        var text = labelObject.GetComponent<TMP_Text>();
        text.font = _consumableContextText.font;
        text.text = label;
        text.fontSize = 16f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return button;
    }

    static void SetConsumableActionLabel(Button button, string value)
    {
        if (button != null && button.GetComponentInChildren<TMP_Text>(true) is TMP_Text label)
            label.text = value;
    }

    void RefreshSelectedConsumableContext()
    {
        var cards = CardManager.Instance;
        var run = RunManager.Instance;
        bool selected = cards != null && _selectedConsumable != null &&
            cards.GetConsumableInstanceAtSlot(_selectedConsumableSlot) == _selectedConsumable &&
            !cards.IsEnemyTargeting && !cards.IsHandEnhancementTargeting;
        if (!selected)
        {
            _selectedConsumableSlot = -1;
            _selectedConsumable = null;
            _discardConfirmationArmed = false;
            if (_consumableContextPanel) _consumableContextPanel.SetActive(false);
            return;
        }
        if (!_consumableContextPanel) EnsureConsumableContextUI();
        if (!_consumableContextPanel) return;
        var definition = _selectedConsumable.Definition;
        var selectedCards = cards.SelectedCards;
        CardInstance selectedEnhancementTarget = definition.effectType == ConsumableEffectType.ApplyEnhancement &&
            selectedCards.Count == 1 ? selectedCards[0]?.data : null;
        bool validEnhancementTarget = selectedEnhancementTarget != null &&
            cards.CanTargetConsumableAtSlot(_selectedConsumableSlot, selectedEnhancementTarget.Id);
        if (_consumableContextText)
        {
            int stackCount = cards.GetConsumableStackCountAtSlot(_selectedConsumableSlot);
            string details = $"<size=115%><b>{definition.displayName}</b></size>" +
                (stackCount > 1 ? $" — {stackCount} copies" : string.Empty) + (ContextualTooltipUI.Instance?string.Empty:$"\n{definition.description}");
            if (definition.effectType == ConsumableEffectType.ApplyEnhancement)
                details += selectedCards.Count == 1
                    ? validEnhancementTarget
                        ? $"\n<size=90%><color=#E8C66D>Selected target: {selectedEnhancementTarget.DisplayName}</color></size>"
                        : $"\n<size=85%><color=#DFA0A0>Selected card is unavailable (enhanced or outside the allowed zone).</color></size>"
                    : string.Empty;
            if (_discardConfirmationArmed)
                _consumableContextText.text = $"Discard 1 of {stackCount} copies of {definition.displayName}? This removes this copy and all {_selectedConsumable.RemainingCharges} remaining charge(s). No reward.";
            else
                _consumableContextText.text = details +
                    (run != null && run.CanSellItems ? string.Empty : "\n<size=85%><color=#B7C0C7>Sell in Black Market</color></size>");
            float availableWidth = Mathf.Max(80f, _consumableContextRect.rect.width - 24f);
            float textHeight = _consumableContextText.GetPreferredValues(_consumableContextText.text, availableWidth, 0f).y;
            _consumableContextRect.sizeDelta = new Vector2(270f, Mathf.Max(150f, Mathf.Ceil(textHeight + 74f)));
        }
        if (_consumableUseButton)
        {
            bool canUse = definition.effectType == ConsumableEffectType.ApplyEnhancement
                ? validEnhancementTarget || selectedCards.Count!=1&&ContextualTooltipUI.Instance&&_enhancementTargetUI&&_enhancementTargetUI.CanOpenConsumableTarget&&cards.CanUseConsumableAtSlot(_selectedConsumableSlot,CombatManager.Instance)
                : cards.CanUseConsumableAtSlot(_selectedConsumableSlot, CombatManager.Instance);
            _consumableUseButton.interactable = !_discardConfirmationArmed && canUse && !GameplayInputGate.IsBlocked;
        }
        if (_consumableSellButton)
        {
            SetConsumableActionLabel(_consumableSellButton, _discardConfirmationArmed ? "CANCEL" : "SELL");
            _consumableSellButton.interactable = _discardConfirmationArmed || (run != null && run.CanSellItems);
        }
        if (_consumableDiscardButton)
        {
            SetConsumableActionLabel(_consumableDiscardButton, _discardConfirmationArmed ? "CONFIRM" : "DISCARD");
            _consumableDiscardButton.interactable = !GameplayInputGate.IsBlocked;
        }
        PositionConsumableContext();
        _consumableContextPanel.SetActive(true);
    }

    void PositionConsumableContext()
    {
        if (!_consumableContextRect || _selectedConsumableSlot < 0 || backpackSlotButtons == null ||
            _selectedConsumableSlot >= backpackSlotButtons.Length || !backpackSlotButtons[_selectedConsumableSlot]) return;
        var buttonRect = backpackSlotButtons[_selectedConsumableSlot].transform as RectTransform;
        var parentRect = _consumableContextRect.parent as RectTransform;
        if (!buttonRect || !parentRect) return;
        var canvas = _consumableContextRect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector3[] corners = _consumableSlotCorners;
        buttonRect.GetWorldCorners(corners);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corners[3]);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, camera, out var local)) return;
        float halfWidth = _consumableContextRect.rect.width * 0.5f;
        float halfHeight = _consumableContextRect.rect.height * 0.5f;
        local.x += halfWidth + 12f;
        if (local.x + halfWidth > parentRect.rect.xMax)
            local.x -= _consumableContextRect.rect.width + buttonRect.rect.width + 24f;
        local.x = Mathf.Clamp(local.x, parentRect.rect.xMin + halfWidth, parentRect.rect.xMax - halfWidth);
        local.y += buttonRect.rect.height * 0.5f;
        // Keep the action cluster clear of every Trick and Gear slot, not only its source slot.
        float controlsLeft=float.PositiveInfinity,controlsRight=float.NegativeInfinity;
        void IncludeControl(RectTransform control)
        {
            if(!control)return;
            control.GetWorldCorners(corners);
            for(int i=0;i<corners.Length;i++)
            {
                float x=parentRect.InverseTransformPoint(corners[i]).x;
                controlsLeft=Mathf.Min(controlsLeft,x);controlsRight=Mathf.Max(controlsRight,x);
            }
        }
        if(backpackSlotButtons!=null)foreach(var slot in backpackSlotButtons)if(slot&&slot.gameObject.activeInHierarchy)IncludeControl(slot.transform as RectTransform);
        var gearRail=FindFirstObjectByType<GearRailUI>();
        if(gearRail)foreach(var gearSlot in gearRail.GetComponentsInChildren<GearIconSlotUI>(true))if(gearSlot.gameObject.activeInHierarchy)IncludeControl(gearSlot.transform as RectTransform);
        if(!float.IsInfinity(controlsRight)&&_consumableContextRect.rect.width>0)
        {
            if(local.x-halfWidth<controlsRight+12f)local.x=controlsRight+12f+halfWidth;
            if(local.x+halfWidth>parentRect.rect.xMax-12f&&!float.IsInfinity(controlsLeft))
                local.x=controlsLeft-halfWidth-12f;
        }
        local.x = Mathf.Clamp(local.x, parentRect.rect.xMin + halfWidth, parentRect.rect.xMax - halfWidth);
        // On narrower canvases the fan can reach into the side context. Keep details above
        // actual visible cards rather than covering a target or shrinking the text.
        var holder = (handField != null ? handField : CardManager.Instance?.handField)?.cardsHolder;
        if (holder != null)
        {
            float handLeft = float.PositiveInfinity, handRight = float.NegativeInfinity;
            float handBottom = float.PositiveInfinity, handTop = float.NegativeInfinity;
            holder.GetComponentsInChildren(false, _contextHandViews);
            foreach (var view in _contextHandViews)
            {
                if (view.data == null) continue;
                var cardRect = view.visualRoot != null ? view.visualRoot : view.transform as RectTransform;
                if (cardRect == null) continue;
                cardRect.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 point = parentRect.InverseTransformPoint(corner);
                    handLeft = Mathf.Min(handLeft, point.x);
                    handRight = Mathf.Max(handRight, point.x);
                    handBottom = Mathf.Min(handBottom, point.y);
                    handTop = Mathf.Max(handTop, point.y);
                }
            }
            if (local.x + halfWidth > handLeft && local.x - halfWidth < handRight &&
                local.y - halfHeight < handTop && local.y + halfHeight > handBottom)
                local.y = handTop + halfHeight + 12f;
        }
        local.y = Mathf.Clamp(local.y, parentRect.rect.yMin + halfHeight, parentRect.rect.yMax - halfHeight);
        _consumableContextRect.anchoredPosition = local;
    }

    void UseSelectedConsumable()
    {
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        if (GameplayInputGate.IsBlocked || cards == null || combat == null || _selectedConsumable == null ||
            cards.GetConsumableInstanceAtSlot(_selectedConsumableSlot) != _selectedConsumable ||
            cards.IsEnemyTargeting || cards.IsHandEnhancementTargeting) return;

        var definition = _selectedConsumable.Definition;
        if (definition.effectType == ConsumableEffectType.ApplyEnhancement)
        {
            if (cards.SelectedCards.Count != 1)
            {
                if(ContextualTooltipUI.Instance&&_enhancementTargetUI&&_enhancementTargetUI.CanOpenConsumableTarget&&cards.CanUseConsumableAtSlot(_selectedConsumableSlot,combat))
                {int slot=_selectedConsumableSlot;ClearSelectedConsumable();_enhancementTargetUI.OpenConsumableTarget(slot);}
                else RefreshAll();
                return;
            }
            var target = cards.SelectedCards[0]?.data;
            if (target == null || !ReferenceEquals(cards.FindOwnedCard(target.Id), target) ||
                !cards.CanTargetConsumableAtSlot(_selectedConsumableSlot, target.Id))
            {
                RefreshAll();
                return;
            }
            if (cards.UseEnhancementConsumableAtSlot(_selectedConsumableSlot, target.Id))
                ClearSelectedConsumable();
            RefreshAll();
            return;
        }

        if (!cards.CanUseConsumableAtSlot(_selectedConsumableSlot, combat)) return;
        switch (cards.GetConsumableTargetTypeAtSlot(_selectedConsumableSlot))
        {
            case ConsumableTargetType.Card:
                int slot = _selectedConsumableSlot;
                ClearSelectedConsumable();
                RefreshAll();
                _enhancementTargetUI?.OpenConsumableTarget(slot);
                break;
            case ConsumableTargetType.Enemy:
                var targets = cards.GetValidEnemyTargetsForConsumableAtSlot(_selectedConsumableSlot);
                int targetCount = cards.GetConsumableEffectTargetMaximumAtSlot(_selectedConsumableSlot);
                if (targets.Count == targetCount)
                {
                    combat.SelectEnemyTarget(targets[0]);
                    bool used = cards.UseConsumableAtSlot(_selectedConsumableSlot, combat, targets[0]);
                    if (used) ClearSelectedConsumable();
                    RefreshAll();
                }
                else if (targets.Count > targetCount)
                {
                    _enemyTargetAction = EnemyTargetAction.Consumable;
                    _targetConsumableSlot = _selectedConsumableSlot;
                    _targetConsumableInstance = _selectedConsumable;
                    _validEnemyTargets.Clear();
                    _validEnemyTargets.AddRange(targets);
                    cards.BeginEnemyTargeting(HandleEnemyTargetChosen, IsCurrentEnemyTargetValid);
                    RefreshEnemyTargetPresentation();
                    RefreshAll();
                }
                break;
            default:
                if (cards.UseConsumableAtSlot(_selectedConsumableSlot, combat)) ClearSelectedConsumable();
                RefreshAll();
                break;
        }
    }

    void SellOrCancelSelectedConsumable()
    {
        if (_discardConfirmationArmed)
        {
            _discardConfirmationArmed = false;
            RefreshAll();
            return;
        }
        var dialog=Object.FindFirstObjectByType<ConfirmationDialogUI>();
        var coordinator=Object.FindFirstObjectByType<GameplayMenuCoordinator>();
        if(ContextualTooltipUI.Instance&&dialog&&coordinator&&RunManager.Instance?.CanSellItems==true&&_selectedConsumable!=null&&!GameplayInputGate.IsBlocked)
        {
            int slot=_selectedConsumableSlot;var item=_selectedConsumable;int price=item.Definition.price;
            dialog.Open(coordinator,"Sell Trick?",$"Sell {item.Definition.displayName} for {price/2} Credits? This removes this copy and its remaining charges.","SELL",()=>
            {
                if(CardManager.Instance.GetConsumableInstanceAtSlot(slot)==item&&item.Definition.price==price&&
                    RunManager.Instance.SellConsumable(slot,item,price)){ClearSelectedConsumable();RefreshAll();}
            });
        }
        else SellSelectedConsumable();
    }

    void DiscardSelectedConsumable()
    {
        var cards = CardManager.Instance;
        if (GameplayInputGate.IsBlocked || cards == null || _selectedConsumable == null ||
            cards.GetConsumableInstanceAtSlot(_selectedConsumableSlot) != _selectedConsumable) return;
        if (!_discardConfirmationArmed)
        {
            _discardConfirmationArmed = true;
            RefreshAll();
            return;
        }
        if (cards.RemoveConsumableInstanceAtSlot(_selectedConsumableSlot, _selectedConsumable))
            ClearSelectedConsumable();
        else
            _discardConfirmationArmed = false;
        RefreshAll();
    }

    void SellSelectedConsumable()
    {
        var cards = CardManager.Instance;
        var run = RunManager.Instance;
        if (cards == null || run == null || !run.CanSellItems ||
            cards.GetConsumableInstanceAtSlot(_selectedConsumableSlot) != _selectedConsumable ||
            _selectedConsumable?.Definition == null) return;
        if (run.SellConsumable(_selectedConsumableSlot, _selectedConsumable,
            _selectedConsumable.Definition.price)) ClearSelectedConsumable();
        RefreshAll();
    }

    public void ClearSelectedConsumable()
    {
        _selectedConsumableSlot = -1;
        _selectedConsumable = null;
        _discardConfirmationArmed = false;
        _contextHandViews.Clear();
        if (_consumableContextPanel) _consumableContextPanel.SetActive(false);
        var cards = CardManager.Instance;
        if (cards == null || backpackSlotButtons == null) return;
        for (int i = 0; i < backpackSlotButtons.Length; i++)
        {
            var button = backpackSlotButtons[i];
            if (!button || !button.targetGraphic) continue;
            button.targetGraphic.color = cards.GetConsumableAtSlot(i) == null
                ? new Color(0.14f, 0.22f, 0.23f, 0.3f)
                : new Color(0.25f, 0.34f, 0.30f, 0.85f);
        }
    }

    void RefreshBackpackSlots(CardManager cards, CombatManager combat)
    {
        if (backpackSlotButtons == null) return;
        for (int i = 0; i < backpackSlotButtons.Length; i++)
        {
            var button = backpackSlotButtons[i];
            if (!button) continue;
            bool visible = i < Mathf.Max(cards.BackpackCapacity, cards.BackpackSlotsUsed);
            button.gameObject.SetActive(visible);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (!visible)
            {
                if (label) label.gameObject.SetActive(false);
                continue;
            }
            var consumable = cards.GetConsumableAtSlot(i);
            bool isSelected = _selectedConsumableSlot == i && ReferenceEquals(_selectedConsumable, cards.GetConsumableInstanceAtSlot(i)) &&
                !cards.IsEnemyTargeting && !cards.IsHandEnhancementTargeting;
            var style=button.GetComponent<EditorialButton>();if(style)style.selected=isSelected;
            if (button.targetGraphic) button.targetGraphic.color = consumable == null
                ? new Color(0.14f, 0.22f, 0.23f, 0.3f)
                : isSelected ? new Color(0.32f, 0.58f, 0.78f, 1f) : new Color(0.25f, 0.34f, 0.30f, 0.85f);
            if (label)
            {
                int stackCount = cards.GetConsumableStackCountAtSlot(i);
                var activeItem = cards.GetConsumableInstanceAtSlot(i);
                string compactIcon = consumable != null ? consumable.icon ?? string.Empty : string.Empty;
                if (DealbreakUIStyle.Theme && compactIcon.Length > 2) compactIcon = compactIcon.Substring(0, 2);
                label.text = consumable == null
                    ? string.Empty
                    : (DealbreakUIStyle.Theme?compactIcon:$"{consumable.icon} {consumable.displayName}") +
                        (stackCount > 1 ? $" ×{stackCount}" : string.Empty) +
                        (consumable.uses > 1 && activeItem != null
                            ? DealbreakUIStyle.Theme?$" {activeItem.RemainingCharges}/{consumable.uses}":$" ({activeItem.RemainingCharges}/{consumable.uses} first)" : string.Empty);
                label.gameObject.SetActive(consumable != null);
                if (DealbreakUIStyle.Theme)
                {
                    label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                    label.overflowMode = TMPro.TextOverflowModes.Truncate;
                }
            }
            // Keep slots raycastable for drag/reorder even when use is currently unavailable;
            // CardManager revalidates every click/drop against current combat state.
            button.interactable = true;
        }
        RefreshSelectedConsumableContext();
    }
}
