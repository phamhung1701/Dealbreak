using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays Mod and Gear activations (<see cref="EffectTriggerFeed"/>) one after another, as Balatro
/// plays its Jokers: the source card or Gear hops, a tag says what it did, and Credits or HP fly
/// between it and the HUD. During a played attack or block the activations before the hit play
/// first, and the committed cards fly once they have (<see cref="AttackCardPresentationUI"/>).
/// Presentation only: the gameplay values have already changed.
/// </summary>
public sealed class EffectTriggerPresenterUI : MonoBehaviour
{
    public static EffectTriggerPresenterUI Instance { get; private set; }

    const float StepTime = .3f, HealthCostStepTime = .5f, MinStepScale = .35f;
    const float TagTime = .8f, ChipTime = .42f, ChipStagger = .06f;
    const int MaxChips = 6;

    struct Step
    {
        public EffectTrigger Trigger;
        public float StartAt;
        public bool Combat;
    }

    readonly List<Step> _queue = new();
    readonly List<GameObject> _visuals = new();
    readonly Dictionary<CardInstance, RectTransform> _committed = new();
    readonly Dictionary<Transform, Coroutine> _pulses = new();
    readonly Dictionary<Transform, int> _tagsOnAnchor = new();
    RectTransform _layer;
    float _timelineEnd;
    bool _inCommit, _leadInFrozen;
    float _commitStart, _leadIn, _cloneMoveAt;
    int _creditsInFlight, _healthInFlight, _combatInFlight;
    GearRailUI _rail;
    ActionButtonsUI _actions;

    /// <summary>Credits announced by a trigger that have not yet landed in the coin.</summary>
    public static bool HoldsCredits => Instance && Instance._creditsInFlight > 0;
    /// <summary>Healing announced by a trigger that has not yet landed on the HP bar.</summary>
    public static bool HoldsHealth => Instance && Instance._healthInFlight > 0;
    /// <summary>Combat activations still playing: the board waits for them before moving on.</summary>
    public static bool CombatBusy => Instance && (Instance._combatInFlight > 0);

    public static EffectTriggerPresenterUI Install(Canvas root)
    {
        if (!root) return null;
        var existing = root.GetComponentInChildren<EffectTriggerPresenterUI>(true);
        if (existing) return existing;
        var layer = new GameObject("Effect Trigger Layer", typeof(RectTransform), typeof(Canvas), typeof(EffectTriggerPresenterUI));
        var rect = (RectTransform)layer.transform;
        rect.SetParent(root.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        // Above the flying attack cards (60) and hit numbers (66), below details (90) and menus (100).
        var canvas = layer.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 70;
        var presenter = layer.GetComponent<EffectTriggerPresenterUI>();
        presenter._layer = rect;
        return presenter;
    }

    void Awake()
    {
        if (!_layer) _layer = (RectTransform)transform;
    }

    void OnEnable()
    {
        Instance = this;
        EffectTriggerFeed.Triggered += HandleTriggered;
        if (RunManager.Instance != null) RunManager.Instance.OnRunStarted += HandleRunStarted;
    }

    void OnDisable()
    {
        EffectTriggerFeed.Triggered -= HandleTriggered;
        if (RunManager.Instance != null) RunManager.Instance.OnRunStarted -= HandleRunStarted;
        CancelAll();
        if (Instance == this) Instance = null;
    }

    void HandleRunStarted(string _) => CancelAll();

    // ---- Committed cards ------------------------------------------------------------------

    /// <summary>The cards about to be played (or blocked with) and their flying copies, so their
    /// activations hop the copies instead of the hand.</summary>
    public void BeginCommit(IReadOnlyList<CardInstance> cards, IReadOnlyList<RectTransform> clones)
    {
        _committed.Clear();
        var gone = new List<Transform>();
        foreach (var target in _restScales.Keys) if (!target) gone.Add(target);
        foreach (var target in gone) { _restScales.Remove(target); _restAngles.Remove(target); }
        for (int i = 0; cards != null && clones != null && i < cards.Count && i < clones.Count; i++)
            if (cards[i] != null && clones[i]) _committed[cards[i]] = clones[i];
        _inCommit = true;
        _leadInFrozen = false;
        _commitStart = Time.time;
        _leadIn = 0f;
    }

    /// <summary>
    /// Fixes how long the committed cards wait for the activations reported so far, and makes later
    /// activations wait until the cards have landed (<paramref name="travelTime"/> after the wait).
    /// Called when the first hit is timed, and at the latest when the commit ends.
    /// </summary>
    public float FreezeLeadIn(float travelTime)
    {
        if (!_inCommit) return 0f;
        if (_leadInFrozen) return _leadIn;
        _leadInFrozen = true;
        _leadIn = Mathf.Max(0f, _timelineEnd - _commitStart);
        _cloneMoveAt = _commitStart + _leadIn;
        _timelineEnd = Mathf.Max(_timelineEnd, _cloneMoveAt + travelTime);
        return _leadIn;
    }

    public float EndCommit(float travelTime)
    {
        float leadIn = FreezeLeadIn(travelTime);
        _inCommit = false;
        return leadIn;
    }

    // ---- Queue ----------------------------------------------------------------------------

    void HandleTriggered(EffectTrigger trigger)
    {
        if (!isActiveAndEnabled || !_layer) return;
        var combat = CombatManager.Instance;
        bool inCombat = combat != null && combat.currentState is GameState.PlayerTurn or GameState.EnemyAttacking;
        float now = Time.time;
        // A long backlog plays faster, so spamming actions never leaves a long tail.
        float scale = Mathf.Max(MinStepScale, 1f - .15f * _queue.Count);
        float start = Mathf.Max(now, _timelineEnd);
        _timelineEnd = start + StepDuration(trigger.Cue) * scale;
        _queue.Add(new Step { Trigger = trigger, StartAt = start, Combat = inCombat });
        if (trigger.Cue == EffectCue.Credits) _creditsInFlight++;
        if (trigger.Cue is EffectCue.Heal) _healthInFlight++;
        if (inCombat)
        {
            _combatInFlight++;
            AttackCardPresentationUI.Instance?.MarkExternalBusy();
        }
    }

    static float StepDuration(EffectCue cue) => cue == EffectCue.HealthCost ? HealthCostStepTime : StepTime;

    void Update()
    {
        if (_queue.Count == 0) return;
        float now = Time.time;
        for (int i = 0; i < _queue.Count;)
        {
            if (_queue[i].StartAt > now) { i++; continue; }
            var step = _queue[i];
            _queue.RemoveAt(i);
            StartCoroutine(Play(step));
        }
    }

    public void CancelAll()
    {
        StopAllCoroutines();
        foreach (var visual in _visuals)
            if (visual) Destroy(visual);
        _visuals.Clear();
        foreach (var pulse in _pulses.Keys)
            if (pulse)
            {
                pulse.localScale = _restScales.TryGetValue(pulse, out var rest) ? rest : Vector3.one;
                pulse.localRotation = Quaternion.Euler(0f, 0f, _restAngles.TryGetValue(pulse, out var angle) ? angle : 0f);
            }
        _pulses.Clear();
        _restScales.Clear();
        _restAngles.Clear();
        _tagsOnAnchor.Clear();
        _queue.Clear();
        _committed.Clear();
        _inCommit = _leadInFrozen = false;
        _timelineEnd = 0f;
        bool wasCombat = _combatInFlight > 0;
        _creditsInFlight = _healthInFlight = _combatInFlight = 0;
        if (wasCombat) AttackCardPresentationUI.Instance?.NotifyExternalIdle();
    }

    IEnumerator Play(Step step)
    {
        var trigger = step.Trigger;
        var anchor = ResolveAnchor(trigger, out var handCard, out bool canHop);
        float duration = StepDuration(trigger.Cue);
        if (anchor)
        {
            if (trigger.Cue == EffectCue.HealthCost)
                yield return PlayHealthCost(trigger, anchor, handCard, canHop);
            else
            {
                Hop(anchor, handCard, canHop);
                ShowTag(anchor, TagText(trigger), Style(trigger.Cue), trigger.Cue == EffectCue.Credits);
                bool sends = trigger.Cue is EffectCue.Credits or EffectCue.Heal;
                if (trigger.Cue == EffectCue.Credits) StartCoroutine(SendCredits(anchor, trigger.Amount));
                else if (trigger.Cue == EffectCue.Heal) StartCoroutine(SendHealth(anchor, trigger.Amount));
                // The board waits for what this step sends to land.
                yield return Wait(sends ? ChipTime + (MaxChips - 1) * ChipStagger : duration * .5f);
            }
        }
        else
        {
            // Nothing on screen to animate (an unseen Gear or a card already gone): just settle.
            if (trigger.Cue == EffectCue.Credits) _creditsInFlight--;
            if (trigger.Cue == EffectCue.Heal) _healthInFlight--;
        }
        if (step.Combat) FinishCombatStep();
    }

    void FinishCombatStep()
    {
        _combatInFlight = Mathf.Max(0, _combatInFlight - 1);
        if (_combatInFlight == 0) AttackCardPresentationUI.Instance?.NotifyExternalIdle();
    }

    // ---- Anchors --------------------------------------------------------------------------

    RectTransform ResolveAnchor(EffectTrigger trigger, out CardView handCard, out bool canHop)
    {
        handCard = null;
        canHop = true;
        if (trigger.Gear != null)
        {
            if (!_rail) _rail = FindFirstObjectByType<GearRailUI>();
            var slot = _rail ? _rail.SlotFor(trigger.Gear.Id) : null;
            return slot && slot.gameObject.activeInHierarchy ? (RectTransform)slot.transform : null;
        }
        if (trigger.Card == null) return null;
        if (_committed.TryGetValue(trigger.Card, out var clone) && clone && clone.gameObject.activeInHierarchy)
        {
            // Once the copy is flying its own animation owns it: show the tag where it is, no hop.
            canHop = !_leadInFrozen || Time.time < _cloneMoveAt - .02f;
            return clone;
        }
        var hand = CardManager.Instance != null ? CardManager.Instance.handField : null;
        if (hand && hand.cardsHolder)
            foreach (var view in hand.cardsHolder.GetComponentsInChildren<CardView>())
                if (view && ReferenceEquals(view.data, trigger.Card) && view.gameObject.activeInHierarchy)
                {
                    handCard = view;
                    return view.visualRoot ? view.visualRoot : (RectTransform)view.transform;
                }
        return null;
    }

    RectTransform HealthTarget()
    {
        if (!_actions) _actions = FindFirstObjectByType<ActionButtonsUI>();
        var label = _actions ? _actions.playerHealthLabel : null;
        return label && label.gameObject.activeInHierarchy ? label.rectTransform : null;
    }

    static RectTransform CreditsTarget() => CreditsHudUI.Instance ? CreditsHudUI.Instance.Coin : null;

    Vector2 LocalPoint(RectTransform target)
    {
        var canvas = target.GetComponentInParent<Canvas>();
        var camera = CameraFor(canvas);
        var screen = RectTransformUtility.WorldToScreenPoint(camera, target.TransformPoint(target.rect.center));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen,
            CameraFor(_layer.GetComponentInParent<Canvas>()), out var local);
        return local;
    }

    Vector2 LocalTop(RectTransform target) => LocalEdge(target, 1f);

    /// <summary>The middle of the target's bottom (0) or top (1) edge, in the layer's space.</summary>
    Vector2 LocalEdge(RectTransform target, float edge)
    {
        var canvas = target.GetComponentInParent<Canvas>();
        var camera = CameraFor(canvas);
        var screen = RectTransformUtility.WorldToScreenPoint(camera,
            target.TransformPoint(new Vector3(target.rect.center.x, Mathf.Lerp(target.rect.yMin, target.rect.yMax, edge))));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen,
            CameraFor(_layer.GetComponentInParent<Canvas>()), out var local);
        return local;
    }

    static Camera CameraFor(Canvas canvas)
    {
        var root = canvas ? canvas.rootCanvas : null;
        return root && root.renderMode != RenderMode.ScreenSpaceOverlay
            ? root.worldCamera ? root.worldCamera : Camera.main : null;
    }

    // ---- Motion ---------------------------------------------------------------------------

    void Hop(RectTransform anchor, CardView handCard, bool canHop)
    {
        if (handCard) { handCard.PlayTriggerPulse(); return; }
        if (!canHop) return;
        // A Cut Sheet slot hops its own face; its root is the untilted (often pinned) hit area.
        if (anchor && anchor.TryGetComponent(out CutSheetSlotUI sticker)) { sticker.Hop(); return; }
        if (_pulses.TryGetValue(anchor, out var running) && running != null) StopCoroutine(running);
        _pulses[anchor] = StartCoroutine(Pulse(anchor));
    }

    // Scale and a wiggle only: slots sit in layout groups and card copies own their position.
    IEnumerator Pulse(RectTransform target)
    {
        Vector3 rest = RestScale(target);
        float restAngle = _restAngles[target];
        float elapsed = 0f;
        while (elapsed < CardView.TriggerPulseTime && target)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / CardView.TriggerPulseTime);
            float hop = Mathf.Sin(t * Mathf.PI);
            target.localScale = rest * (1f + hop * .2f);
            target.localRotation = Quaternion.Euler(0f, 0f, restAngle + Mathf.Sin(t * Mathf.PI * 3f) * 9f * (1f - t));
            yield return null;
        }
        if (target)
        {
            target.localScale = rest;
            target.localRotation = Quaternion.Euler(0f, 0f, restAngle);
        }
        _pulses.Remove(target);
    }

    // The pose a target rests in, captured the first time it pulses: Gear slots and the coin at
    // scale one, card copies at their captured screen scale and fan angle.
    readonly Dictionary<Transform, Vector3> _restScales = new();
    readonly Dictionary<Transform, float> _restAngles = new();
    Vector3 RestScale(Transform target)
    {
        if (!_restScales.TryGetValue(target, out var rest))
        {
            _restScales[target] = rest = target.localScale;
            _restAngles[target] = target.localEulerAngles.z;
        }
        return rest;
    }

    IEnumerator PlayHealthCost(EffectTrigger trigger, RectTransform anchor, CardView handCard, bool canHop)
    {
        var health = HealthTarget();
        int amount = Mathf.Max(0, trigger.Amount);
        if (health)
        {
            ShowTag(health, $"−{amount} HP", TagStyle.Cost, false, below: false);
            StartCoroutine(PulseHealth(health));
            int chips = Mathf.Clamp(amount, 1, MaxChips);
            for (int i = 0; i < chips; i++)
                StartCoroutine(FlyChip(health, anchor, null, Signal, i * ChipStagger, null));
            yield return Wait(ChipTime + (chips - 1) * ChipStagger);
        }
        if (anchor)
        {
            Hop(anchor, handCard, canHop);
            ShowTag(anchor, $"+{amount} DMG", TagStyle.Attack, false);
        }
        yield return Wait(.12f);
    }

    IEnumerator PulseHealth(RectTransform health)
    {
        var panel = health.parent as RectTransform;
        if (!panel) yield break;
        float elapsed = 0f;
        while (elapsed < .3f && panel)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / .3f);
            panel.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * .07f);
            yield return null;
        }
        if (panel) panel.localScale = Vector3.one;
    }

    IEnumerator SendCredits(RectTransform from, int amount)
    {
        var coin = CreditsTarget();
        if (!coin)
        {
            _creditsInFlight--;
            yield break;
        }
        var theme = DealbreakUIStyle.Theme;
        int chips = Mathf.Clamp(amount, 1, MaxChips);
        int landed = 0;
        for (int i = 0; i < chips; i++)
            StartCoroutine(FlyChip(from, coin, theme ? theme.creditsIcon : null, Mustard, i * ChipStagger,
                () => { landed++; PopCoin(coin); }));
        float elapsed = 0f;
        while (landed < chips && elapsed < 2f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        _creditsInFlight = Mathf.Max(0, _creditsInFlight - 1);
    }

    IEnumerator SendHealth(RectTransform from, int amount)
    {
        var health = HealthTarget();
        if (!health)
        {
            _healthInFlight--;
            yield break;
        }
        int chips = Mathf.Clamp(amount, 1, MaxChips);
        int landed = 0;
        for (int i = 0; i < chips; i++)
            StartCoroutine(FlyChip(from, health, null, Lime, i * ChipStagger, () => landed++));
        float elapsed = 0f;
        while (landed < chips && elapsed < 2f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        _healthInFlight = Mathf.Max(0, _healthInFlight - 1);
        StartCoroutine(PulseHealth(health));
    }

    void PopCoin(RectTransform coin)
    {
        if (!coin) return;
        if (_pulses.TryGetValue(coin, out var running) && running != null) StopCoroutine(running);
        _pulses[coin] = StartCoroutine(Pulse(coin));
    }

    // A chip rises off its source, then arcs into the target, re-aiming every frame.
    IEnumerator FlyChip(RectTransform from, RectTransform to, Sprite sprite, Color color, float delay,
        System.Action landed)
    {
        if (delay > 0f) yield return Wait(delay);
        if (!from || !to) { landed?.Invoke(); yield break; }
        var chip = new GameObject("Trigger Chip", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)chip.transform;
        rect.SetParent(_layer, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = sprite ? new Vector2(38f, 38f) : new Vector2(16f, 16f);
        var image = chip.GetComponent<Image>();
        image.raycastTarget = false;
        if (sprite) { image.sprite = sprite; image.preserveAspect = true; image.color = Color.white; }
        else image.color = color;
        _visuals.Add(chip);
        Vector2 start = LocalPoint(from) + new Vector2(Jitter(-28f, 28f), Jitter(-10f, 22f));
        float elapsed = 0f;
        while (elapsed < ChipTime && rect && to)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / ChipTime);
            Vector2 end = LocalPoint(to);
            Vector2 control = new((start.x + end.x) * .5f, Mathf.Max(start.y, end.y) + 110f);
            float eased = t * t;
            rect.anchoredPosition = (1 - eased) * (1 - eased) * start + 2 * (1 - eased) * eased * control + eased * eased * end;
            rect.localScale = Vector3.one * Mathf.Lerp(1.15f, .7f, t);
            rect.localRotation = Quaternion.Euler(0f, 0f, t * 180f);
            yield return null;
        }
        _visuals.Remove(chip);
        if (chip) Destroy(chip);
        landed?.Invoke();
    }

    // ---- Tags -----------------------------------------------------------------------------

    internal enum TagStyle { Gain, Credits, Attack, Cost, Utility }

    static Color Lime => DealbreakUIStyle.Theme ? DealbreakUIStyle.Theme.ochre : new Color(.78f, .94f, .19f);
    static Color Signal => DealbreakUIStyle.Theme ? DealbreakUIStyle.Theme.brick : new Color(.91f, .2f, .17f);
    static Color Paper => DealbreakUIStyle.Theme ? DealbreakUIStyle.Theme.paper : new Color(.93f, .92f, .89f);
    static Color Ink => DealbreakUIStyle.Theme ? DealbreakUIStyle.Theme.ink : new Color(.04f, .04f, .05f);
    static Color InkPrint => DealbreakUIStyle.Theme ? DealbreakUIStyle.Theme.inkPrint : new Color(.07f, .07f, .07f);
    static Color Mustard => DealbreakUIStyle.Theme ? DealbreakUIStyle.Theme.mustard : new Color(.9f, .67f, .33f);

    internal static TagStyle Style(EffectCue cue) => cue switch
    {
        EffectCue.Credits => TagStyle.Credits,
        EffectCue.Heal or EffectCue.Draw or EffectCue.Block or EffectCue.Shield or
            EffectCue.BlockBonus or EffectCue.BlockMultiplier or EffectCue.DamageReduction or
            EffectCue.MaxHealth => TagStyle.Gain,
        EffectCue.AttackBonus or EffectCue.AttackMultiplier or EffectCue.Counter or EffectCue.Critical or
            EffectCue.DoubleStrike or EffectCue.AreaDamage or EffectCue.Carry or EffectCue.CoverFire => TagStyle.Attack,
        EffectCue.HealthCost or EffectCue.Destroy => TagStyle.Cost,
        _ => TagStyle.Utility
    };

    // Credits are mustard everywhere; other gains keep the selection accent until their zone is restyled.
    internal static Color Fill(TagStyle style) => style switch
    {
        TagStyle.Gain => Lime,
        TagStyle.Credits => Mustard,
        TagStyle.Attack => Signal,
        TagStyle.Cost => Ink,
        _ => Paper
    };

    public static string TagText(EffectTrigger trigger)
    {
        int n = trigger.Amount;
        return trigger.Cue switch
        {
            EffectCue.Credits => $"+{n}",
            EffectCue.Heal => $"+{n} HP",
            EffectCue.HealthCost => $"−{n} HP",
            EffectCue.Draw => n == 1 ? "DRAW" : $"DRAW {n}",
            EffectCue.AttackBonus => $"+{n} DMG",
            EffectCue.AttackMultiplier => $"×{n} DMG",
            EffectCue.BlockBonus => $"+{n} BLOCK",
            EffectCue.BlockMultiplier => $"×{n} BLOCK",
            EffectCue.DamageReduction => $"−{n} DMG",
            EffectCue.Counter => $"COUNTER {n}",
            EffectCue.Critical => "CRIT",
            EffectCue.DoubleStrike => $"×{Mathf.Max(2, n)} HITS",
            EffectCue.AreaDamage => $"SPLASH {n}",
            EffectCue.Growth => $"UPGRADE +{n}",
            EffectCue.Weaken => $"−{n} ATK",
            EffectCue.Recycle => $"RECYCLE {n}",
            EffectCue.Destroy => "DESTROYED",
            EffectCue.Shield => $"+{n} SHIELD",
            EffectCue.Block => $"+{n} BLOCK",
            EffectCue.Retrigger => "AGAIN",
            EffectCue.Save => "SAVED",
            EffectCue.ReturnToHand => "BACK TO HAND",
            EffectCue.MaxHealth => $"+{n} MAX HP",
            EffectCue.Carry => $"CARRY {n}",
            EffectCue.CoverFire => $"COVER {n}",
            EffectCue.StaysInHand => "STAYS IN HAND",
            _ => string.Empty
        };
    }

    void ShowTag(RectTransform anchor, string text, TagStyle style, bool creditIcon, bool below = false)
    {
        if (string.IsNullOrEmpty(text) || !anchor) return;
        var tag = new GameObject("Trigger Tag", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)tag.transform;
        rect.SetParent(_layer, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, 0f);
        var background = tag.GetComponent<Image>();
        background.raycastTarget = false;
        background.color = Fill(style);
        var labelObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.SetParent(rect, false);
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        var theme = DealbreakUIStyle.Theme;
        if (theme && theme.headingFont) label.font = theme.headingFont;
        label.fontSize = 30f;
        label.text = text;
        label.color = style switch
        {
            TagStyle.Attack => Paper,
            TagStyle.Cost => Signal,
            _ => InkPrint
        };
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        float iconSize = creditIcon && theme && theme.creditsIcon ? 30f : 0f;
        Vector2 textSize = label.GetPreferredValues(text);
        rect.sizeDelta = new Vector2(textSize.x + 22f + (iconSize > 0f ? iconSize + 6f : 0f), 42f);
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(.5f, .5f);
        labelRect.sizeDelta = new Vector2(textSize.x + 4f, 42f);
        labelRect.anchoredPosition = new Vector2(iconSize > 0f ? (iconSize + 6f) * .5f : 0f, 1f);
        if (iconSize > 0f)
        {
            var icon = new GameObject("Credit", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)icon.transform;
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, .5f);
            iconRect.pivot = new Vector2(0f, .5f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = new Vector2(10f, 0f);
            var iconImage = icon.GetComponent<Image>();
            iconImage.sprite = theme.creditsIcon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
        }
        _tagsOnAnchor.TryGetValue(anchor, out int stacked);
        _tagsOnAnchor[anchor] = stacked + 1;
        Vector2 at = (below ? LocalPoint(anchor) : LocalTop(anchor)) + new Vector2(0f, 12f + stacked * 48f);
        // Header slots have no room above: their tags hang under the slot instead.
        if (at.y + 70f > _layer.rect.yMax)
            at = LocalEdge(anchor, 0f) - new Vector2(0f, 54f + stacked * 48f);
        rect.anchoredPosition = at;
        _visuals.Add(tag);
        StartCoroutine(AnimateTag(tag, rect, background, label, at, anchor));
    }

    IEnumerator AnimateTag(GameObject tag, RectTransform rect, Image background, TMP_Text label, Vector2 at,
        RectTransform anchor)
    {
        var group = tag.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        float elapsed = 0f;
        float tilt = Jitter(-5f, 5f);
        while (elapsed < TagTime && rect)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / TagTime);
            float pop = Mathf.Clamp01(elapsed / .14f);
            rect.localScale = Vector3.one * Mathf.LerpUnclamped(.3f, 1f, EaseOutBack(pop));
            rect.localRotation = Quaternion.Euler(0f, 0f, tilt * (1f - pop * .6f));
            rect.anchoredPosition = at + Vector2.up * (24f * EaseOutCubic(t));
            group.alpha = 1f - Mathf.InverseLerp(.7f, 1f, t);
            yield return null;
        }
        _visuals.Remove(tag);
        if (tag) Destroy(tag);
        if (anchor && _tagsOnAnchor.TryGetValue(anchor, out int stacked))
        {
            if (stacked <= 1) _tagsOnAnchor.Remove(anchor);
            else _tagsOnAnchor[anchor] = stacked - 1;
        }
    }

    // Presentation jitter only, from its own generator: gameplay random streams are never touched.
    readonly System.Random _jitter = new();
    float Jitter(float min, float max) => min + (float)_jitter.NextDouble() * (max - min);

    static float EaseOutCubic(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    static float EaseOutBack(float t)
    {
        const float overshoot = 1.7f;
        float u = t - 1f;
        return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
    }

    static IEnumerator Wait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
