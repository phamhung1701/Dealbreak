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
    enum EnemyTargetAction { None, Attack }

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
    // A slot's authored glyph alignment; a Trick sprite moves the stack/charge count to the top-right corner.
    readonly Dictionary<TMP_Text, TextAlignmentOptions> _backpackLabelAlignment = new();
    ConsumableSlotDragUI[] _backpackSlotDragInputs;
    EnemyTargetAction _enemyTargetAction;
    readonly List<CardView> _attackTargetCards = new();
    readonly List<EnemyRuntime> _validEnemyTargets = new();
    EnemyGroupUI _enemyGroup;

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
            CombatManager.Instance.OnCommandCompleted += RefreshAll;
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
        _enemyGroup = FindFirstObjectByType<EnemyGroupUI>();

        if (playButton) { playButton.onClick.AddListener(OnPlayClicked); KeyButtonBorderStyle.Apply(playButton); }
        if (passButton) { passButton.onClick.AddListener(OnPassClicked); KeyButtonBorderStyle.Apply(passButton); }
        if (blockButton) { blockButton.onClick.AddListener(OnBlockClicked); KeyButtonBorderStyle.Apply(blockButton); }
        if (rankSortButton) rankSortButton.onClick.AddListener(OnRankSortClicked);
        if (suitSortButton) suitSortButton.onClick.AddListener(OnSuitSortClicked);
        if (recoverButton) { recoverButton.onClick.AddListener(OnRecoverClicked); KeyButtonBorderStyle.Apply(recoverButton); }
        BindBackpackSlots();

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
            CombatManager.Instance.OnCommandCompleted -= RefreshAll;
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
        ItemDragUI.Get()?.Cancel();
        CancelEnemyTargeting();
        RefreshAll();
    }

    void HandleContextEnded()
    {
        CancelEnemyTargeting();
        RefreshAll();
    }

    void HandleShopContextStarted() => RefreshAll();

    void HandleEnemyTargetingChanged(bool active)
    {
        if (!active && _enemyTargetAction != EnemyTargetAction.None)
        {
            _enemyTargetAction = EnemyTargetAction.None;
            _attackTargetCards.Clear();
            _validEnemyTargets.Clear();
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
    void HandleSelectionChanged(CardView _) => RefreshAll();
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
        if (_healthBar == null) _healthBar = HealthBarBacking.Attach(playerHealthLabel, true, HealthBarBacking.Look.Underline);
        if (_healthBar != null)
            _healthBar.SetFraction(combat.HasInfiniteHealthForDev || combat.player.maxHealth <= 0 ? 1f
                : _shownHealth.Value / combat.player.maxHealth);
    }

    /// <summary>The player's HP panel, the drop target for healing Tricks (the bar itself is a thin underline).</summary>
    public RectTransform PlayerHealthArea => playerHealthLabel ? (RectTransform)playerHealthLabel.transform.parent : null;

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
            var theme = DealbreakUIStyle.Theme;
            if (theme && pendingDamageLabel.color != theme.brick)
            {
                DealbreakUIStyle.Text(pendingDamageLabel, theme.headingFont, 30, theme.brick);
                pendingDamageLabel.alignment = TextAlignmentOptions.Center;
            }
            DealbreakUIStyle.Outline(pendingDamageLabel, .12f);
            bool showDamage = combat.currentState == GameState.EnemyAttacking;
            pendingDamageLabel.gameObject.SetActive(showDamage);
            pendingDamageLabel.text = showDamage
                ? combat.pendingDamage > 0
                    ? $"INCOMING {combat.IncomingDamageAfterModifiers}"
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
        if (!GameplayEffectResolver.HasDoubleStrikeEffect(selected)) return $"{damage} damage";
        // A damage cap (Scalpel) limits both hits together.
        int total = combat.PreviewActionTotalDamage(selected, combat.currentEnemy);
        return total == damage * 2 ? $"{damage} damage ×2 hits" : $"{total} damage over 2 hits";
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
        return _enemyTargetAction == EnemyTargetAction.Attack && combat.CanPlayCards(_attackTargetCards, target);
    }

    void HandleEnemyTargetChosen(EnemyRuntime target)
    {
        if (!IsCurrentEnemyTargetValid(target)) return;
        var action = _enemyTargetAction;
        var combat = CombatManager.Instance;
        var attackCards = _attackTargetCards.ToArray();
        CancelEnemyTargeting();
        bool resolved = false;
        if (action == EnemyTargetAction.Attack && combat != null)
        {
            combat.SelectEnemyTarget(target);
            resolved = ExecuteAttack(attackCards, target, combat);
        }
        if (!resolved) RefreshAll();
    }

    bool ExecuteAttack(IReadOnlyList<CardView> selection, EnemyRuntime target, CombatManager combat) =>
        CombatCommands.Attack(selection, target);

    void CancelEnemyTargeting()
    {
        _enemyTargetAction = EnemyTargetAction.None;
        _attackTargetCards.Clear();
        _validEnemyTargets.Clear();
        CardManager.Instance?.EndEnemyTargeting();
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
        if (!TutorialGate.Allows(TutorialCommand.Pass)) return;
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
                PinBackpackSlotDetail(slotIndex);
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

    // Clicking a Trick keeps its detail open; it is used, sold or moved by dragging it.
    void PinBackpackSlotDetail(int slotIndex)
    {
        var cards = CardManager.Instance;
        if (GameplayInputGate.IsBlocked || cards == null || cards.GetConsumableInstanceAtSlot(slotIndex) == null) return;
        var detail = backpackSlotButtons[slotIndex] ? backpackSlotButtons[slotIndex].GetComponent<ContextualInfoTrigger>() : null;
        if (detail && ContextualTooltipUI.Instance) ContextualTooltipUI.Instance.Pin(detail);
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
            if (button.targetGraphic) button.targetGraphic.color = consumable == null
                ? new Color(0.14f, 0.22f, 0.23f, 0.3f)
                : new Color(0.25f, 0.34f, 0.30f, 0.85f);
            if (label)
            {
                int stackCount = cards.GetConsumableStackCountAtSlot(i);
                var activeItem = cards.GetConsumableInstanceAtSlot(i);
                bool hasSprite = ItemIconUI.Show(button.transform, consumable != null ? consumable.iconSprite : null);
                string compactIcon = consumable != null && !hasSprite ? consumable.icon ?? string.Empty : string.Empty;
                if (DealbreakUIStyle.Theme && compactIcon.Length > 2) compactIcon = compactIcon.Substring(0, 2);
                label.text = consumable == null
                    ? string.Empty
                    : ((DealbreakUIStyle.Theme?compactIcon:$"{consumable.icon} {consumable.displayName}") +
                        (stackCount > 1 ? $" ×{stackCount}" : string.Empty) +
                        (consumable.uses > 1 && activeItem != null
                            ? DealbreakUIStyle.Theme?$" {activeItem.RemainingCharges}/{consumable.uses}":$" ({activeItem.RemainingCharges}/{consumable.uses} first)" : string.Empty)).Trim();
                if (!_backpackLabelAlignment.ContainsKey(label)) _backpackLabelAlignment[label] = label.alignment;
                label.alignment = hasSprite ? TextAlignmentOptions.TopRight : _backpackLabelAlignment[label];
                if (hasSprite && label.text.Length > 0)
                {
                    // Over the sprite the count prints on an ink tab so it stays legible.
                    label.text = $"<mark=#0E1113EE padding=6,6,2,2>{label.text}</mark>";
                    label.transform.SetAsLastSibling();
                }
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
    }
}
