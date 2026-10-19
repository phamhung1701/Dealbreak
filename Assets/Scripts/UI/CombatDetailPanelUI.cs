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
        if (actions && actions.combatLogLabel) actions.combatLogLabel.gameObject.SetActive(visible);
        if (visible && actions)
        {
            if (actions.recoverButton) actions.recoverButton.gameObject.SetActive(true);
        }
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
            var attackLabel = actions.playButton ? actions.playButton.GetComponentInChildren<TMP_Text>() : null;
            if (attackLabel) attackLabel.text = cards != null && cards.IsEnemyTargeting ? "CANCEL" : "ATTACK";
            bool defense = CombatManager.Instance.currentState == GameState.EnemyAttacking;
            if (actions.recoverButton) actions.recoverButton.gameObject.SetActive(
                CombatManager.Instance.currentState == GameState.PlayerTurn && cards != null && cards.HandCount == 0);
            if (playerStatePanel)
            {
                playerStatePanel.anchorMax = new Vector2(playerStatePanel.anchorMax.x, defense ? .55f : .21f);
                actions.playerHealthLabel.rectTransform.anchorMin = new Vector2(.055f, defense ? .67f : .1f);
            }
        }
        if (actions && actions.selectionLabel && cards != null)
        {
            if (cards.SelectedCards.Count == 0 && !cards.IsEnemyTargeting && !cards.IsHandEnhancementTargeting)
                actions.selectionLabel.text = cards.HandCount == 0 ? actions.selectionLabel.text : string.Empty;
            else if (cards.SelectedCards.Count > 0 && !cards.IsEnemyTargeting && !cards.IsHandEnhancementTargeting &&
                     CombatManager.Instance.currentState == GameState.PlayerTurn)
                actions.selectionLabel.text = SelectionSummary(cards);
        }
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

    public static string SelectionSummary(CardManager cards)
    {
        var combat = CombatManager.Instance;
        if (cards == null || combat == null || cards.SelectedCards.Count == 0) return string.Empty;
        var instances = new CardInstance[cards.SelectedCards.Count];
        for (int i = 0; i < instances.Length; i++) instances[i] = cards.SelectedCards[i].data;
        if (GameplayEffectResolver.IsRoyalFamilySelection(instances, cards)) return "ROYAL FAMILY • Instant defeat • Choose a target";
        long power = 0;
        for (int i = 0; i < instances.Length; i++) power += combat.CalculateCardAttackDamage(instances[i], instances, i == 0);
        return $"{instances.Length} selected • Attack power {Math.Min(int.MaxValue, power)} • Choose a target";
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
            if (data.Enhancement) _text.Append("MOD • ").Append(data.Enhancement.displayName).Append('\n').Append(data.Enhancement.description).Append("\n\n");
            else _text.Append("No Mod attached.\n\n");
            if (CardManager.Instance?.SelectedCards.Count > 0)
                _text.Append(actions && actions.selectionLabel ? actions.selectionLabel.text : SelectionSummary(CardManager.Instance)).Append("\n\n");
            _text.Append("Attack power is before action bonuses, target reactions and chance effects. Block preview uses the pending attacks.\n\nSelect to pin • Drag to a target or use the action bar.");
            Show(data.DisplayName, _text.ToString());
        }
        else if (source.TryGetComponent<EnemyDisplayUI>(out var enemy))
        {
            if (enemy.DisplayedEnemy == null || enemy.DisplayedEnemy.IsDefeated) return false;
            var data = enemy.DisplayedEnemy;
            _text.Append(enemy.DetailGuidance);
            if (data.type.abilities != null)
                foreach (var ability in data.type.abilities)
                    if (ability) _text.Append("\n\n").Append(ability.displayName).Append('\n').Append(ability.description);
            if (data.type.sourceCard) _text.Append("\n\nHeadliner reward • ").Append(data.type.sourceCard.DisplayName);
            if (data.type.fleeAfterPlayerTurns > 0) _text.Append("\n\nFlees after ").Append(data.type.fleeAfterPlayerTurns).Append(" completed card actions. Completed: ").Append(data.PlayerTurnsCompleted);
            Show(data.DisplayName, _text.ToString());
        }
        else if (source.TryGetComponent<GearIconSlotUI>(out var slot))
        {
            if (!slot.Gear) return false;
            Show(slot.Gear.displayName, slot.Gear.description);
        }
        else if (source.TryGetComponent<DrawDeckStackUI>(out _))
        {
            var cards = CardManager.Instance;
            if (cards == null) return false;
            Show("DRAW PILE", $"Draw {cards.deck.Count}\nDiscard {cards.discardPile.Count}\nHand {cards.HandCount}/{cards.HandCapacity}\n\nSelect the pile to inspect its cards.\n\n{runStatus?.pileSummaryLabel?.text}\n{runStatus?.timingLabel?.text}");
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
