using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Combat-only presentation. Reads live models and existing pure calculators; never resolves an action.</summary>
public sealed class CombatDetailPanelUI : MonoBehaviour
{
    public static CombatDetailPanelUI Instance { get; private set; }
    public static bool IsCombatActive => Instance != null && Instance.InCombat || ContextualTooltipUI.Instance && DealbreakUIRuntime.CombatActive;
    public GameObject panel;
    public TMP_Text title, body;
    public ScrollRect scroll;
    public ActionButtonsUI actions;
    public RunStatusUI runStatus;
    public GearRailUI gearRail;
    [Tooltip("Kept for scene compatibility; the HP panel is placed by PlayerCornerLayout.")]
    public RectTransform playerStatePanel;
    CombatInspectUI _inspected;
    string _lastTitle, _lastBody;
    bool _wasCombat;
    float _leaveUntil;
    readonly StringBuilder _text = new();

    public bool InCombat => CombatManager.Instance != null && CombatManager.Instance.Enemies.Count > 0 &&
        CombatManager.Instance.currentState is GameState.PlayerTurn or GameState.EnemyAttacking;
    void OnEnable() => Instance = this;
    void OnDisable() { if (Instance == this) Instance = null; _inspected = null; if (panel) panel.SetActive(false); SetSecondaryVisible(true); }
    public void Inspect(CombatInspectUI source) { if (InCombat && !GameplayInputGate.IsBlocked) { _inspected = source; _leaveUntil = float.PositiveInfinity; } }
    public void Leave(CombatInspectUI source) { if (_inspected == source) _leaveUntil = Time.unscaledTime + 0.25f; }

    void SetSecondaryVisible(bool visible)
    {
        if (runStatus == null) return;
        if (runStatus.timingLabel) runStatus.timingLabel.gameObject.SetActive(false);
        if (runStatus.pileSummaryLabel) runStatus.pileSummaryLabel.enabled = visible;
        // Recover is automatic (ActionButtonsUI): its button never shows.
        if (actions && actions.recoverButton) actions.recoverButton.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        bool combat = InCombat;
        if (combat != _wasCombat)
        {
            SetSecondaryVisible(!combat); _wasCombat = combat;
            if (gearRail) gearRail.Refresh();
        }
        if (!combat || GameplayInputGate.IsBlocked)
        {
            _inspected = null;
            if (combat && panel) panel.SetActive(false);
            else if (_lastTitle != null && panel) panel.SetActive(false);
            _lastTitle = _lastBody = null;
            return;
        }
        var cards = CardManager.Instance;
        if (actions)
        {
            // The ATTACK label has one writer, ActionButtonsUI.RefreshAll (S13).
            if (actions.recoverButton && actions.recoverButton.gameObject.activeSelf) actions.recoverButton.gameObject.SetActive(false);
            // The HP panel's rect has one writer, PlayerCornerLayout via DealbreakUIRuntime (S10a).
        }
        // The selection preview has one writer, ActionButtonsUI.RefreshAll, and one builder,
        // SelectionSummary (S13); this panel only reads it back.
        bool overPanel = panel && panel.activeSelf && UnityEngine.InputSystem.Mouse.current != null &&
            RectTransformUtility.RectangleContainsScreenPoint((RectTransform)panel.transform,
                UnityEngine.InputSystem.Mouse.current.position.ReadValue(), null);
        if (_inspected && _inspected.isActiveAndEnabled &&
            (_inspected.IsInspected || Time.unscaledTime < _leaveUntil || overPanel) && RenderSource(_inspected.gameObject)) return;
        _inspected = null;
        // Selection pins detail when the pointer leaves; a transient inspect always wins.
        if (cards?.selectedCard != null && RenderSource(cards.selectedCard.gameObject)) return;
        if (gearRail)
            foreach (var slot in gearRail.GetComponentsInChildren<GearIconSlotUI>())
                if (slot.IsDetailSelected && RenderSource(slot.gameObject)) return;
        if (panel) panel.SetActive(false);
        _lastTitle = _lastBody = null;
    }

    /// <summary>
    /// S13: the one builder of the selection preview (attack, block and Recover). ActionButtonsUI.RefreshAll
    /// is its one writer. State and numbers only; it never changes state or consumes RNG.
    /// </summary>
    public static string SelectionSummary(CardManager cards)
    {
        var combat = CombatManager.Instance;
        if (cards == null || combat == null || cards.IsEnemyTargeting || cards.IsHandEnhancementTargeting) return string.Empty;
        var selected = cards.SelectedCards;
        switch (combat.currentState)
        {
            case GameState.PlayerTurn:
                if (cards.HandCount == 0) return $"Recover: take {combat.TotalEnemyAttack}, draw 1";
                return selected.Count == 0 ? string.Empty : AttackSummary(cards, combat, selected);
            case GameState.EnemyAttacking:
                if (selected.Count == 0) return string.Empty;
                return combat.TryPreviewDefense(selected, out int blocked, out int remaining)
                    ? $"Blocks {blocked} of {combat.PendingAttackCount} • {remaining} damage left"
                    : "Can't block with this selection";
            default:
                return string.Empty;
        }
    }

    static string AttackSummary(CardManager cards, CombatManager combat, System.Collections.Generic.IReadOnlyList<CardView> selected)
    {
        int count = selected.Count;
        var instances = new CardInstance[count];
        for (int i = 0; i < count; i++) instances[i] = selected[i].data;
        if (GameplayEffectResolver.IsRoyalFamilySelection(instances, cards)) return "Royal Flush • instant defeat";
        string power = AttackPower(combat, instances);
        if (count == 2 && GameplayEffectResolver.CanPlayAsAcePair(instances)) return $"Ace pair • {power}";
        if (count >= 2 && GameplayEffectResolver.IsHandsMultiCardAction(instances, cards)) return $"Same rank ×{count} • {power}";
        if (count >= 2 && GameplayEffectResolver.IsRoyalFamilyPartialSelection(selected, cards))
            return $"Royal Flush {count}/{AttackSelectionRules.RoyalFlushSize} • {power}";
        if (GameplayEffectResolver.IsNuclearOptionGroup(instances, cards)) return $"{count} cards • {power}";
        return $"{count} selected • {power}";
    }

    // The execution calculation: primed Gear bonuses, action damage, hit count and caps. The critical
    // roll and enemy defenses are not included.
    static string AttackPower(CombatManager combat, CardInstance[] instances)
    {
        var target = combat.currentEnemy;
        int hits = combat.PreviewHitCount(instances);
        int total = combat.PreviewActionTotalDamage(instances, target);
        if (hits <= 1) return $"Attack power {total}";
        int perHit = combat.PreviewActionDamage(instances, target);
        // A damage cap (Scalpel) limits all the hits together.
        return total == (long)perHit * hits ? $"Attack power {perHit} ×{hits} hits" : $"Attack power {total} over {hits} hits";
    }

    bool RenderSource(GameObject source)
    {
        _text.Clear();
        var combat = CombatManager.Instance;
        if (source.TryGetComponent<CardView>(out var card))
        {
            if (card.data == null || card.homeField == null) return false;
            var data = card.data;
            _text.Append($"ATTACK POWER  {combat.CalculateCardAttackDamage(data)}\nBLOCK VALUE  {combat.CalculateCardDefense(data)}\n\n");
            if (data.Enhancement) _text.Append("MOD • ").Append(ModSignature.Name(data.Enhancement)).Append('\n').Append(ModSignature.Description(data.Enhancement)).Append("\n\n");
            // S14: the selection preview reads on the status line only, and the card names itself (no heading).
            while (_text.Length > 0 && _text[_text.Length - 1] == '\n') _text.Length--;
            Show(string.Empty, _text.ToString());
        }
        else if (source.TryGetComponent<EnemyDisplayUI>(out var enemy))
        {
            if (enemy.DisplayedEnemy == null || enemy.DisplayedEnemy.IsDefeated) return false;
            var data = enemy.DisplayedEnemy;
            // Ability names and rules open from the ability tags under the enemy (owner 2026-10-29).
            // S14: Shield, Double Strike/charged and the Headliner reward read on the enemy itself.
            _text.Append(enemy.DetailGuidance);
            if (data.type.fleeAfterPlayerTurns > 0) _text.Append("\n\nFlees after ").Append(data.type.fleeAfterPlayerTurns).Append(" completed card actions. Completed: ").Append(data.PlayerTurnsCompleted);
            Show(data.DisplayName, _text.ToString().TrimStart('\n'));
        }
        else if (source.TryGetComponent<GearIconSlotUI>(out var slot))
        {
            if (!slot.Gear) return false;
            Show(slot.Gear.displayName, GearRules.OwnedDescription(CardManager.Instance, slot.Gear, slot.InstanceId));
        }
        else if (source.TryGetComponent<DrawDeckStackUI>(out _))
        {
            string detail = DrawDeckStackUI.DetailText(CardManager.Instance, RunManager.Instance);
            if (detail == null) return false;
            Show("DRAW PILE", detail);
        }
        else return false;
        return true;
    }

    void Show(string heading, string description)
    {
        if (!panel || !title || !body) return;
        if (!panel.activeSelf) panel.SetActive(true);
        if (_lastTitle == heading && _lastBody == description) return;
        bool changedSource = _lastTitle != heading;
        title.text = heading;
        body.text = description;
        // The content grows inside a bounded, scrollable viewport, never over the targets/hand.
        body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(1f, body.GetPreferredValues(description, body.rectTransform.rect.width, 0f).y + 20f));
        if (changedSource && scroll) scroll.verticalNormalizedPosition = 1f;
        _lastTitle = heading; _lastBody = description;
    }
}
