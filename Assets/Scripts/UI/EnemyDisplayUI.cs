using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EnemyDisplayUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("References")]
    public TMP_Text nameText;
    public TMP_Text hpText;
    public TMP_Text atkText;
    public TMP_Text statusText;
    public Image portrait;
    public Slider healthBar;
    public CombatPortraitCatalog combatPortraits;
    public string DetailGuidance { get; private set; }

    EnemyRuntime _boundEnemy;
    bool _hasExplicitBinding;
    bool _isDisplayingHeadlinerCard;
    bool _isDisplayingAuthoredPortrait;
    Sprite _placeholderSprite;
    Color _placeholderColor;
    Image.Type _placeholderImageType;
    bool _placeholderPreserveAspect;
    TMP_Text _portraitGlyph;
    Vector2 _portraitAnchorMin;
    Vector2 _portraitAnchorMax;
    Vector2 _portraitAnchoredPosition;
    Vector2 _portraitSizeDelta;
    Coroutine _defeatCue;
    Coroutine _perfectBlockCue;
    TMP_Text _perfectBlockCueText;
    int _perfectBlockCueFrame = -1;
    int _perfectBlockCueCount;
    Vector3 _preDefeatScale;
    Color _preDefeatPortraitColor;
    CanvasGroup _defeatCanvasGroup;
    bool _enemyTargeting;
    bool _enemyTargetEligible;
    bool _enemyPointerHovered;
    CanvasGroup _targetCanvasGroup;
    Vector3 _baseTargetScale;

    // Presentation-only motion: HP drain, hit shake/flash, entrance and target emphasis.
    // Gameplay values are read from EnemyRuntime; these only ease how they are shown.
    const float HpSmoothTime = 0.14f;
    // Cut Sheet hit shake (visual-design 3b): 120ms, 6px, only with shake on and Reduce Motion off.
    const float ShakeTime = 0.12f;
    const float ShakeStrength = 6f;
    const float HitFlashTime = 0.2f;
    const float EntranceTime = 0.3f;
    const float EmphasisSmoothTime = 0.07f;
    float _shownHp = -1f;
    float _hpVelocity;
    float _hpHold;
    float _shakeTime;
    float _shakeStrength;
    float _flashTime;
    Color _flashColor;
    Vector2 _portraitRestPosition;
    Vector2 _portraitShownOffset;
    float _entranceDelay;
    float _entranceElapsed = EntranceTime;
    float _targetAlpha = 1f;
    float _targetScaleFactor = 1f;
    float _scaleFactor = 1f;
    float _scaleVelocity;

    public EnemyRuntime DisplayedEnemy => _hasExplicitBinding
        ? _boundEnemy
        : CombatManager.Instance != null ? CombatManager.Instance.currentEnemy : null;

    void Awake()
    {
        EnemyAbilityCalloutUI.Attach(this);
        _baseTargetScale = transform.localScale;
        if (!portrait) return;

        _placeholderSprite = portrait.sprite;
        _placeholderColor = portrait.color;
        _placeholderImageType = portrait.type;
        _placeholderPreserveAspect = portrait.preserveAspect;
        _portraitGlyph = portrait.GetComponentInChildren<TMP_Text>(true);

        var rect = portrait.rectTransform;
        _portraitAnchorMin = rect.anchorMin;
        _portraitAnchorMax = rect.anchorMax;
        _portraitAnchoredPosition = rect.anchoredPosition;
        _portraitSizeDelta = rect.sizeDelta;
    }

    public void SetEnemyTargeting(bool targeting, bool eligible)
    {
        bool wasTargeting = _enemyTargeting;
        _enemyTargeting = targeting;
        _enemyTargetEligible = targeting && eligible;
        if (wasTargeting && !targeting) RefreshStateGuidance();
        if (_enemyTargeting && _targetCanvasGroup == null)
            _targetCanvasGroup = TryGetComponent(out CanvasGroup existingCanvasGroup) ? existingCanvasGroup : gameObject.AddComponent<CanvasGroup>();
        RefreshEnemyTargetPresentation();
        if (!wasTargeting && targeting && _marks != null) _marks.Hop();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _enemyPointerHovered = true;
        RefreshEnemyTargetPresentation();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _enemyPointerHovered = false;
        RefreshEnemyTargetPresentation();
    }

    // Targeting reads through the crop marks (and dims an enemy the pending action cannot reach);
    // no frame, outline or VALID/INVALID wording.
    void RefreshEnemyTargetPresentation()
    {
        _targetAlpha = _enemyTargeting && !_enemyTargetEligible ? 0.38f : 1f;
        if (_targetCanvasGroup != null && !IsEntering) _targetCanvasGroup.alpha = _targetAlpha;
        SetEmphasis(1f);
        RefreshTargetMarks();
    }

    // Target emphasis eases in play mode; outside it (tests, editor) it applies at once.
    void SetEmphasis(float factor)
    {
        _targetScaleFactor = factor;
        if (Application.isPlaying || _baseTargetScale == Vector3.zero) return;
        _scaleFactor = factor;
        transform.localScale = _baseTargetScale * factor;
    }

    bool IsEntering => _entranceElapsed < EntranceTime;

    public bool IsBoundTo(EnemyRuntime enemy) => _hasExplicitBinding && ReferenceEquals(_boundEnemy, enemy);

    public void Bind(EnemyRuntime enemy)
    {
        if (!IsBoundTo(enemy))
        {
            // A recycled view must not drain or shake from the previous enemy's state.
            _shownHp = -1f;
            _hpHold = _shakeTime = _flashTime = 0f;
        }
        _hasExplicitBinding = true;
        _boundEnemy = enemy;
        RefreshDisplay();
        RefreshStateGuidance();
    }

    /// <summary>Fades and grows the panel in after the delay; presentation only.</summary>
    public void PlayEntrance(float delay)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || _baseTargetScale == Vector3.zero) return;
        if (_targetCanvasGroup == null && !TryGetComponent(out _targetCanvasGroup))
            _targetCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        _entranceDelay = Mathf.Max(0f, delay);
        _entranceElapsed = 0f;
        _targetCanvasGroup.alpha = 0f;
        transform.localScale = _baseTargetScale * _scaleFactor * 0.88f;
    }

    /// <summary>
    /// Shakes and tints the portrait when a hit lands, and holds the HP drain until then.
    /// The gameplay result has already been applied; only its presentation waits.
    /// </summary>
    public void PlayHitCue(bool blocked, float delay)
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        _hpHold = _hpHold > 0f ? Mathf.Min(_hpHold, delay) : delay;
        StartCoroutine(HitCueAfter(blocked, delay));
    }

    System.Collections.IEnumerator HitCueAfter(bool blocked, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        StartShake(ShakeTime);
        _flashTime = HitFlashTime;
        _flashColor = blocked ? new Color(0.55f, 0.9f, 1f, 1f) : new Color(1f, 0.42f, 0.36f, 1f);
    }

    void StartShake(float seconds)
    {
        if (!CutSheetMotion.ShakeEnabled || CutSheetMotion.ReduceMotion) return;
        _shakeTime = _shakeDuration = seconds;
        _shakeStrength = ShakeStrength;
    }

    float _shakeDuration = ShakeTime;

    void Update()
    {
        if (!Application.isPlaying) return;
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;
        TickHp(deltaTime);
        TickHitCue(deltaTime);
        TickEmphasis(deltaTime);
    }

    void TickHp(float deltaTime)
    {
        var enemy = DisplayedEnemy;
        if (enemy == null || _shownHp < 0f) return;
        if (_hpHold > 0f)
        {
            _hpHold = Mathf.Max(0f, _hpHold - deltaTime);
            return;
        }
        if (Mathf.Approximately(_shownHp, enemy.currentHp)) return;
        _shownHp = Mathf.SmoothDamp(_shownHp, enemy.currentHp, ref _hpVelocity, HpSmoothTime, Mathf.Infinity, deltaTime);
        if (Mathf.Abs(_shownHp - enemy.currentHp) < 0.05f) _shownHp = enemy.currentHp;
        ApplyHp(enemy);
    }

    void ApplyHp(EnemyRuntime enemy)
    {
        if (hpText) hpText.SetText("HP {0}/{1}", Mathf.RoundToInt(_shownHp), enemy.maxHp);
        ApplyHpBar(enemy.maxHp > 0 ? _shownHp / enemy.maxHp : 0f, enemy.ShieldCharges);
    }

    // ---- Cut Sheet dressing (visual-design 3b, hud.md section C) ----
    // The portrait is a sticker on the board with a 6px ink shadow and no frame; the name, "HP n/max"
    // and the abilities sit on the board in paper type; the HP meter is an ink frame, paper track and
    // ink fill with a 2px paper gap; ATTACK is a red tag; Shield greys the fill under a hatch with one
    // glyph per charge. Teal crop marks outside the portrait show valid (thin) and targeted (thick,
    // plus a strip under the meter) enemies, only while two or more stand. Every decoration ignores
    // raycasts; the view root keeps the hit area and every flight/drop target transform.
    public const string MeterName = "HP Meter", StripName = "Target Strip", AttackTagName = "Attack Tag",
        PortraitShadowName = "Portrait Shadow", FocusRingName = "Focus Ring", ShieldRowName = "Shield Glyphs";
    const int MaxShieldGlyphs = 4;
    const float ShieldGlyphSize = 24f, MeterGap = 2f, GlyphPopSeconds = .12f, GlyphPopScale = .3f, FocusRingOutset = 4f;
    // Pixel rows from the view's bottom edge (1080p reference px), under the portrait window.
    // The meter is 18px: 2px ink frame, 2px cream gap, a 10px ink fill (full HP reads as a solid bar).
    public const float MeterHeight = 18f;
    const float StatusTop = 38f, TagBottom = 42f, TagTop = 72f, StripBottom = 74f, StripTop = 80f,
        HpBottom = 81f, HpTop = 109f, MeterBottom = 86f, MeterTop = MeterBottom + MeterHeight,
        NameBottom = 110f, NameTop = 136f, PortraitBottom = 140f;
    static readonly string[] PortraitParts = { "PortraitWindow", "PortraitMount" };

    bool _dressed, _undressable;
    EditorialFrame _viewFrame;
    RectTransform _marksHolder, _marksBounds;
    public RectTransform HpMeter => _meter;
    RectTransform _meter, _meterFill, _shieldRow, _portraitShadowHolder;
    Image _meterFillImage, _meterHatch, _focusRing, _strip;
    CutSheetShadow _portraitShadow;
    CropMarksUI _marks;
    TMP_Text _shieldCount;
    CombatInspectUI _inspect;
    bool _inspectLooked, _focused;
    int _dressedShields = -1;
    float _hatchStart = -1f, _glyphPopStart = -1f;
    readonly System.Collections.Generic.List<Image> _shieldGlyphs = new();
    readonly System.Collections.Generic.List<Graphic> _decorations = new();

    public CropMarksUI TargetMarks => _marks;
    public bool TargetStripShown => _strip && _strip.gameObject.activeSelf;
    public bool FocusRingShown => _focusRing && _focusRing.enabled;
    /// <summary>Every graphic the dressing added; none of them takes raycasts.</summary>
    public System.Collections.Generic.IReadOnlyList<Graphic> Decorations => _decorations;

    public void OnSelect(BaseEventData eventData) { _focused = true; SyncFocusRing(); }
    public void OnDeselect(BaseEventData eventData) { _focused = false; SyncFocusRing(); }

    /// <summary>The enemy texts' Cut Sheet type; DealbreakUIStyle.ApplyHierarchy defers to this.</summary>
    public static void StyleOwnedText(EnemyDisplayUI enemy, TMP_Text text)
    {
        var theme = DealbreakUIStyle.Theme;
        if (!theme || !enemy || !text) return;
        var label = theme.labelFont ? theme.labelFont : theme.headingFont;
        if (text == enemy.nameText)
        {
            DealbreakUIStyle.Text(text, label, 22, theme.paper);
            text.fontStyle = FontStyles.UpperCase;
            text.alignment = TextAlignmentOptions.Center;
        }
        else if (text == enemy.hpText)
        {
            DealbreakUIStyle.Text(text, theme.numericFont ? theme.numericFont : label, 20, theme.paper);
            text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.MidlineLeft;
        }
        else if (text == enemy.atkText)
        {
            DealbreakUIStyle.Text(text, label, 22, theme.paper);
            text.fontStyle = FontStyles.UpperCase;
            text.alignment = TextAlignmentOptions.Center;
        }
        else if (text == enemy.statusText)
        {
            DealbreakUIStyle.Text(text, label, 18, theme.paper);
            text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.Top;
            text.overflowMode = TextOverflowModes.Overflow;
            return;
        }
        else return;
        // Type on the board needs no rim; drop any outline material an older pass left.
        if (text.font && text.fontSharedMaterial != text.font.material) text.fontSharedMaterial = text.font.material;
        text.characterSpacing = 0;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    void EnsureDressing()
    {
        if (_dressed) return;
        _dressed = true;
        // A view built without a RectTransform (legacy or test-built) shows its texts undressed.
        if (!(transform is RectTransform)) { _undressable = true; return; }
        var theme = DealbreakUIStyle.Theme;
        Color ink = theme ? theme.inkPrint : CutSheetShadow.FallbackTint;
        Color paper = theme ? theme.paper : new Color(.94f, .89f, .8f);
        Color red = theme ? theme.brick : new Color(.75f, .2f, .16f);
        Color teal = theme ? theme.ochre : new Color(.2f, .62f, .58f);
        StyleOwnedText(this, nameText);
        StyleOwnedText(this, hpText);
        StyleOwnedText(this, atkText);
        StyleOwnedText(this, statusText);
        LayoutRows();
        // The view frame never draws: the crop marks are the targeted state.
        // No frame until targeted: the view's editorial frame, the portrait mount (its dotted slate
        // mat) and the window's own backing stop drawing; the window keeps its RectMask2D.
        var frameTransform = transform.Find("Editorial Frame");
        if (frameTransform && frameTransform.TryGetComponent(out _viewFrame)) _viewFrame.enabled = false;
        var mount = transform.Find("PortraitMount");
        if (mount) mount.gameObject.SetActive(false);
        var portraitWindow = transform.Find("PortraitWindow");
        if (portraitWindow && portraitWindow.TryGetComponent(out Image windowBacking)) windowBacking.enabled = false;

        var bounds = PortraitBounds();
        if (!bounds) return;
        _marksBounds = bounds == transform ? null : bounds;
        _marksHolder = Mirror("Target Marks", bounds, false);
        _marks = CropMarksUI.Attach(_marksHolder);
        for (int i = 0; i < _marks.MarkCount; i++) Decorate(_marks.Mark(i));

        _focusRing = Mirror(FocusRingName, bounds, true).GetComponent<Image>();
        _focusRing.sprite = theme ? theme.stickerSprite : null;
        _focusRing.type = Image.Type.Sliced;
        _focusRing.fillCenter = false;
        _focusRing.color = paper;
        _focusRing.rectTransform.offsetMin -= Vector2.one * FocusRingOutset;
        _focusRing.rectTransform.offsetMax += Vector2.one * FocusRingOutset;
        _focusRing.enabled = false;
        Decorate(_focusRing);

        if (hpText && hpText.transform.parent)
        {
            var parent = hpText.transform.parent;
            _meter = Child(parent, MeterName, true);
            _meter.SetSiblingIndex(hpText.transform.GetSiblingIndex());
            // A plain ink block: the panel sprite's 9-slice borders exceed an 18px bar and collapse it.
            Decorate(_meter.GetComponent<Image>(), null, ink, Image.Type.Simple);
            var track = Child(_meter, "Track", true);
            Inset(track, MeterGap);
            // (owner 2026-10-29) An ink track under a cream fill: full HP is a bright bar, lost HP goes dark.
            Decorate(track.GetComponent<Image>(), null, ink, Image.Type.Simple);
            _meterFill = Child(track, "Fill", true);
            _meterFill.anchorMin = Vector2.zero;
            _meterFill.anchorMax = Vector2.one;
            _meterFill.pivot = new Vector2(0f, .5f);
            _meterFill.offsetMin = new Vector2(MeterGap, MeterGap);
            _meterFill.offsetMax = new Vector2(-MeterGap, -MeterGap);
            _meterFillImage = Decorate(_meterFill.GetComponent<Image>(), null, paper, Image.Type.Simple);
            var hatch = Child(track, "Shield Hatch", true);
            Inset(hatch, 0f);
            _meterHatch = Decorate(hatch.GetComponent<Image>(), theme ? theme.hatchSprite : null, Color.white, Image.Type.Tiled);
            if (!hatch.TryGetComponent(out ScreenPixelTile _)) hatch.gameObject.AddComponent<ScreenPixelTile>();
            _meterHatch.enabled = false;

            _strip = Decorate(Child(parent, StripName, true).GetComponent<Image>(), null, ink, Image.Type.Simple);
            var inset = Child(_strip.transform, "Inset", true);
            Inset(inset, 1f);
            Decorate(inset.GetComponent<Image>(), null, teal, Image.Type.Simple);
            _strip.gameObject.SetActive(false);
            if (parent == transform)
            {
                Row(_meter, .41f, .95f, MeterBottom, MeterTop);
                Row(_strip.rectTransform, .41f, .95f, StripBottom, StripTop);
            }
            else
            {
                // Outside the authored view (tests), follow the label's own rect.
                Copy(_meter, hpText.rectTransform);
                Copy(_strip.rectTransform, hpText.rectTransform);
            }
        }

        if (atkText && atkText.transform.parent)
        {
            var tag = Child(atkText.transform.parent, AttackTagName, false);
            tag.SetSiblingIndex(atkText.transform.GetSiblingIndex());
            Copy(tag, atkText.rectTransform);
            _attackTag = tag;
            CutSheetShadow shadow = null;
            foreach (Transform child in tag) if (child.TryGetComponent(out shadow)) break;
            if (!shadow) shadow = CutSheetShadow.Create(tag);
            var face = Child(tag, "Face", true);
            Inset(face, 0f);
            var faceImage = Decorate(face.GetComponent<Image>(), theme ? theme.tagSprite : null, red, Image.Type.Sliced);
            shadow.Match(faceImage, CutSheetShadow.Tint, CutSheetMetrics.ShadowSlot);
            Decorate(shadow.Image);
            atkText.transform.SetParent(tag, false);
            Inset(atkText.rectTransform, 0f);
            atkText.raycastTarget = false;
        }
        SyncPortraitShadow();
    }

    // ---- Tag row (owner 2026-10-29): the red ATTACK tag, then one paper tag per ability, then the
    // Shield glyphs, flowing left to right from the row under the HP meter and wrapping onto the next
    // line within the view. An ability tag is a raycast target that consumes its click (it opens the
    // ability's rules next to it through the single ContextualTooltipUI) so the click never also
    // targets or inspects the enemy; keyboard focus opens it too.
    public const string AbilityTagName = "Ability Tag";
    const float TagHeight = 30f, TagGap = 6f, TagLineStep = 34f, TagPadding = 10f, AbilityTagText = 20f;
    RectTransform _attackTag;
    readonly System.Collections.Generic.List<RectTransform> _abilityTags = new();
    readonly System.Collections.Generic.List<TMP_Text> _abilityLabels = new();
    readonly System.Collections.Generic.List<ContextualInfoTrigger> _abilityTriggers = new();
    EnemyTypeData _tagType;
    bool _tagsSynced;
    int _abilityTagCount, _laidAttack = int.MinValue;

    public int AbilityTagCount => _abilityTagCount;
    public RectTransform AbilityTag(int index) => index >= 0 && index < _abilityTagCount ? _abilityTags[index] : null;
    public TMP_Text AbilityTagLabel(int index) => index >= 0 && index < _abilityTagCount ? _abilityLabels[index] : null;
    public ContextualInfoTrigger AbilityTagDetail(int index) => index >= 0 && index < _abilityTagCount ? _abilityTriggers[index] : null;

    void SyncAbilityTags(EnemyRuntime enemy)
    {
        if (_undressable) return;
        var type = enemy != null ? enemy.type : null;
        if (_tagsSynced && ReferenceEquals(type, _tagType)) return;
        _tagsSynced = true;
        _tagType = type;
        int count = 0;
        var abilities = type ? type.abilities : null;
        if (abilities != null)
            for (int i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i];
                if (!ability || string.IsNullOrEmpty(ability.displayName)) continue;
                AbilityTagAt(count);
                // The name exactly as authored; the tag's type style sets the caps.
                _abilityLabels[count].text = ability.displayName;
                var detail = _abilityTriggers[count];
                detail.category = "Ability";
                detail.title = ability.displayName;
                detail.description = ability.description;
                if (!_abilityTags[count].gameObject.activeSelf) _abilityTags[count].gameObject.SetActive(true);
                count++;
            }
        for (int i = count; i < _abilityTags.Count; i++)
            if (_abilityTags[i].gameObject.activeSelf) _abilityTags[i].gameObject.SetActive(false);
        _abilityTagCount = count;
        LayoutTagFlow();
    }

    void AbilityTagAt(int index)
    {
        while (_abilityTags.Count <= index)
        {
            var theme = DealbreakUIStyle.Theme;
            var tag = Child(transform, $"{AbilityTagName} {_abilityTags.Count}", false);
            CutSheetShadow shadow = null;
            foreach (Transform child in tag) if (child.TryGetComponent(out shadow)) break;
            if (!shadow) shadow = CutSheetShadow.Create(tag);
            var face = Child(tag, "Face", true);
            Inset(face, 0f);
            var faceImage = face.GetComponent<Image>();
            faceImage.sprite = theme ? theme.tagSprite : null;
            faceImage.type = faceImage.sprite ? Image.Type.Sliced : Image.Type.Simple;
            faceImage.color = theme ? theme.paper : new Color(.94f, .89f, .8f);
            // The face takes the click; the tag's own handler consumes it before the enemy sees it.
            faceImage.raycastTarget = true;
            shadow.Match(faceImage, CutSheetShadow.Tint, CutSheetMetrics.ShadowSlot);
            Decorate(shadow.Image);
            var labelRect = Child(tag, "Ability Name", false);
            if (!labelRect.TryGetComponent(out TMP_Text label)) label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            Inset(labelRect, 0f);
            if (theme) DealbreakUIStyle.Text(label, theme.labelFont ? theme.labelFont : theme.headingFont, AbilityTagText, theme.inkPrint);
            if (label.font && label.fontSharedMaterial != label.font.material) label.fontSharedMaterial = label.font.material;
            label.fontStyle = FontStyles.UpperCase;
            label.characterSpacing = 0;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
            // Focus opens the rules (owner rule: focus opens info); a click pins them.
            if (!tag.TryGetComponent(out Selectable selectable)) selectable = tag.gameObject.AddComponent<Selectable>();
            selectable.transition = Selectable.Transition.None;
            selectable.targetGraphic = faceImage;
            if (!tag.TryGetComponent(out ContextualInfoTrigger detail)) detail = tag.gameObject.AddComponent<ContextualInfoTrigger>();
            detail.pinOnClick = true;
            _abilityTags.Add(tag);
            _abilityLabels.Add(label);
            _abilityTriggers.Add(detail);
        }
    }

    void OnRectTransformDimensionsChange()
    {
        if (_dressed) LayoutTagFlow();
    }

    // The intent line keeps the view's bottom row (0 to StatusTop). The tag lines stack above it; when
    // they wrap, the HP row, the name and the portrait's bottom edge move up by the extra lines, so
    // nothing leaves the view or reaches the INCOMING band below it.
    void LayoutTagFlow()
    {
        if (!_dressed || !(transform is RectTransform root)) return;
        float width = root.rect.width > 1f ? root.rect.width : 350f;
        float x0 = width * .05f, xMax = width * .95f;
        float extra = (FlowTags(x0, xMax, 0f, false) - 1) * TagLineStep;
        FlowTags(x0, xMax, extra, true);
        if (statusText && statusText.transform.parent == transform) Row(statusText.rectTransform, .04f, .96f, 0f, StatusTop);
        if (hpText && hpText.transform.parent == transform) Row(hpText.rectTransform, .05f, .40f, HpBottom + extra, HpTop + extra);
        if (nameText && nameText.transform.parent == transform) Row(nameText.rectTransform, .04f, .96f, NameBottom + extra, NameTop + extra);
        if (_meter && _meter.parent == transform) Row(_meter, .41f, .95f, MeterBottom + extra, MeterTop + extra);
        if (_strip && _strip.transform.parent == transform) Row(_strip.rectTransform, .41f, .95f, StripBottom + extra, StripTop + extra);
        for (int i = 0; i < PortraitParts.Length; i++)
        {
            var window = transform.Find(PortraitParts[i]) as RectTransform;
            if (!window) continue;
            var min = new Vector2(window.offsetMin.x, PortraitBottom + extra - (i == 1 ? 6f : 0f));
            if (window.offsetMin != min) window.offsetMin = min;
        }
        if (_marksHolder && _marksBounds) Copy(_marksHolder, _marksBounds);
        if (_focusRing && _marksBounds)
        {
            Copy(_focusRing.rectTransform, _marksBounds);
            _focusRing.rectTransform.offsetMin -= Vector2.one * FocusRingOutset;
            _focusRing.rectTransform.offsetMax += Vector2.one * FocusRingOutset;
        }
    }

    // Lays the ATTACK tag, the ability tags and the Shield glyphs left to right from the first line's
    // top (TagTop + extra), wrapping down; returns the number of lines. Measures only unless placing.
    int FlowTags(float x0, float xMax, float extra, bool place)
    {
        float x = x0, top = TagTop + extra;
        int lines = 1;
        if (_attackTag && atkText && atkText.isActiveAndEnabled)
            Flow(_attackTag, Mathf.Ceil(atkText.GetPreferredValues(atkText.text).x) + TagPadding * 2f, x0, xMax, ref x, ref top, ref lines, place);
        for (int i = 0; i < _abilityTagCount; i++)
            Flow(_abilityTags[i], Mathf.Ceil(_abilityLabels[i].GetPreferredValues(_abilityLabels[i].text).x) + TagPadding * 2f, x0, xMax, ref x, ref top, ref lines, place);
        if (_shieldRow && _shieldRow.gameObject.activeSelf)
        {
            int glyphs = 0;
            for (int i = 0; i < _shieldGlyphs.Count; i++) if (_shieldGlyphs[i].gameObject.activeSelf) glyphs++;
            float shieldWidth = glyphs * (ShieldGlyphSize + 4f) + (_shieldCount && _shieldCount.gameObject.activeSelf ? 48f : 0f);
            Flow(_shieldRow, shieldWidth, x0, xMax, ref x, ref top, ref lines, place);
        }
        return lines;
    }

    static void Flow(RectTransform item, float itemWidth, float x0, float xMax, ref float x, ref float top, ref int lines, bool place)
    {
        if (x > x0 && x + itemWidth > xMax) { x = x0; top -= TagLineStep; lines++; }
        if (!place) { x += itemWidth + TagGap; return; }
        item.anchorMin = item.anchorMax = Vector2.zero;
        item.pivot = new Vector2(0f, 1f);
        item.sizeDelta = new Vector2(itemWidth, TagHeight);
        item.anchoredPosition = new Vector2(x, top);
        x += itemWidth + TagGap;
    }

    // The bottom rows under the portrait, in pixels from the view's bottom edge, so a shorter view
    // shrinks only the portrait, never the type. Only the authored view (labels directly under it).
    void LayoutRows()
    {
        if (statusText && statusText.transform.parent == transform) Row(statusText.rectTransform, .04f, .96f, 0f, StatusTop);
        if (atkText && atkText.transform.parent == transform) Row(atkText.rectTransform, .05f, .50f, TagBottom, TagTop);
        if (hpText && hpText.transform.parent == transform) Row(hpText.rectTransform, .05f, .40f, HpBottom, HpTop);
        if (nameText && nameText.transform.parent == transform) Row(nameText.rectTransform, .04f, .96f, NameBottom, NameTop);
        for (int i = 0; i < PortraitParts.Length; i++)
        {
            var window = transform.Find(PortraitParts[i]) as RectTransform;
            if (!window) continue;
            window.anchorMin = new Vector2(window.anchorMin.x, 0f);
            window.offsetMin = new Vector2(window.offsetMin.x, PortraitBottom - (i == 1 ? 6f : 0f));
        }
    }

    RectTransform PortraitBounds()
    {
        if (!portrait || portrait.transform == transform) return transform as RectTransform;
        var rect = portrait.rectTransform;
        return rect.parent is RectTransform window && window.name == "PortraitWindow" ? window : rect;
    }

    // A decoration over (or around) the portrait bounds: a sibling with the same rect, or a stretched
    // child when the bounds are the view itself.
    RectTransform Mirror(string name, RectTransform bounds, bool image)
    {
        bool own = bounds == transform || !bounds.parent;
        var rect = Child(own ? transform : bounds.parent, name, image);
        if (own) Inset(rect, 0f);
        else Copy(rect, bounds);
        rect.SetAsLastSibling();
        return rect;
    }

    // The sticker shadow: the portrait's own sprite tinted ink, 6px down-right, under the portrait.
    void SyncPortraitShadow()
    {
        if (!portrait || portrait.transform == transform || !portrait.transform.parent) return;
        if (_portraitShadowHolder == null)
        {
            _portraitShadowHolder = Child(portrait.transform.parent, PortraitShadowName, false);
            _portraitShadow = CutSheetShadow.Create(_portraitShadowHolder);
            Decorate(_portraitShadow.Image);
        }
        int index = portrait.transform.GetSiblingIndex();
        if (_portraitShadowHolder.GetSiblingIndex() > index) _portraitShadowHolder.SetSiblingIndex(index);
        Copy(_portraitShadowHolder, portrait.rectTransform);
        _portraitShadowHolder.anchoredPosition = _portraitRestPosition;
        _portraitShadow.Match(portrait, CutSheetShadow.Tint, CutSheetMetrics.ShadowPanel);
        if (_portraitShadow.Image.preserveAspect != portrait.preserveAspect) _portraitShadow.Image.preserveAspect = portrait.preserveAspect;
    }

    void SyncFocusRing()
    {
        if (!_focusRing) return;
        if (!_inspectLooked) { _inspectLooked = true; TryGetComponent(out _inspect); }
        bool show = (_focused || _inspect && _inspect.IsInspected) && !IsDefeatCuePlaying;
        if (_focusRing.enabled != show) _focusRing.enabled = show;
    }

    /// <summary>Thin marks on valid targets, thick marks and the strip on the targeted one; only while two or more enemies stand.</summary>
    public void RefreshTargetMarks()
    {
        EnsureDressing();
        if (_marks == null) return;
        var state = CropMarksUI.State.Hidden;
        var combat = CombatManager.Instance;
        var enemy = DisplayedEnemy;
        if (combat != null && enemy != null && !enemy.IsDefeated && !IsDefeatCuePlaying && StandingEnemies(combat) >= 2)
        {
            bool choosing, eligible;
            if (_enemyTargeting) { choosing = true; eligible = _enemyTargetEligible; }
            else
            {
                var cards = CardManager.Instance;
                choosing = cards != null && cards.SelectedCards.Count > 0 && combat.currentState == GameState.PlayerTurn;
                eligible = choosing && combat.CanPlayCards(cards.SelectedCards, enemy);
            }
            bool current = ReferenceEquals(combat.currentEnemy, enemy) &&
                (combat.currentState == GameState.PlayerTurn || combat.currentState == GameState.EnemyAttacking);
            bool targeted = choosing ? eligible && (_enemyPointerHovered || current && !_enemyTargeting) : current;
            state = targeted ? CropMarksUI.State.Thick : eligible ? CropMarksUI.State.Thin : CropMarksUI.State.Hidden;
        }
        _marks.Set(state);
        bool strip = state == CropMarksUI.State.Thick;
        if (_strip && _strip.gameObject.activeSelf != strip) _strip.gameObject.SetActive(strip);
    }

    static int StandingEnemies(CombatManager combat)
    {
        int standing = 0;
        var enemies = combat.Enemies;
        for (int i = 0; i < enemies.Count; i++) if (enemies[i] != null && !enemies[i].IsDefeated) standing++;
        return standing;
    }

    void ApplyHpBar(float fraction, int shields = 0)
    {
        if (healthBar && healthBar.gameObject.activeSelf) healthBar.gameObject.SetActive(false);
        EnsureDressing();
        if (_meterFill == null) return;
        var max = new Vector2(Mathf.Clamp01(fraction), 1f);
        if (_meterFill.anchorMax != max) _meterFill.anchorMax = max;
        var theme = DealbreakUIStyle.Theme;
        Color fill = shields > 0 ? theme ? theme.muted : new Color(.55f, .51f, .44f) : theme ? theme.paper : new Color(.94f, .89f, .8f);
        if (_meterFillImage.color != fill) _meterFillImage.color = fill;
        ApplyShieldGlyphs(shields);
    }

    // One glyph per Shield charge beside ATTACK; past four, one glyph and "×n". A gain crossfades the
    // hatch in and pops the newest glyph.
    void ApplyShieldGlyphs(int shields)
    {
        if (shields == _dressedShields) return;
        bool gained = shields > _dressedShields && _dressedShields >= 0;
        _dressedShields = shields;
        bool animate = Application.isPlaying && !CutSheetMotion.ReduceMotion;
        if (_meterHatch)
        {
            bool hatch = shields > 0;
            if (_meterHatch.enabled != hatch) _meterHatch.enabled = hatch;
            if (hatch && gained && animate) { _hatchStart = Time.unscaledTime; SetAlpha(_meterHatch, 0f); }
            else if (_hatchStart < 0f) SetAlpha(_meterHatch, 1f);
        }
        if (!_shieldRow && shields <= 0) return;
        if (!_shieldRow)
        {
            var parent = atkText && atkText.transform.parent ? atkText.transform.parent : transform;
            _shieldRow = Child(parent, ShieldRowName, false);
            if (!_shieldRow.TryGetComponent(out HorizontalLayoutGroup layout)) layout = _shieldRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            if (parent != transform) Inset(_shieldRow, 0f);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        }
        if (_shieldRow.gameObject.activeSelf != shields > 0) _shieldRow.gameObject.SetActive(shields > 0);
        int glyphs = shields > MaxShieldGlyphs ? 1 : shields;
        while (_shieldGlyphs.Count < glyphs)
        {
            var glyph = new GameObject("Shield", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            glyph.layer = gameObject.layer;
            var rect = (RectTransform)glyph.transform;
            rect.SetParent(_shieldRow, false);
            rect.sizeDelta = new Vector2(ShieldGlyphSize, ShieldGlyphSize);
            var image = glyph.GetComponent<Image>();
            image.sprite = ShieldGlyph.Sprite;
            image.preserveAspect = true;
            Decorate(image);
            _shieldGlyphs.Add(image);
        }
        for (int i = 0; i < _shieldGlyphs.Count; i++)
        {
            bool show = i < glyphs;
            if (_shieldGlyphs[i].gameObject.activeSelf != show) _shieldGlyphs[i].gameObject.SetActive(show);
            _shieldGlyphs[i].rectTransform.localScale = Vector3.one;
        }
        if (gained && animate && glyphs > 0) _glyphPopStart = Time.unscaledTime;
        bool counted = shields > MaxShieldGlyphs;
        if (counted && !_shieldCount)
        {
            var count = new GameObject("Shield Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            count.layer = gameObject.layer;
            var rect = (RectTransform)count.transform;
            rect.SetParent(_shieldRow, false);
            rect.sizeDelta = new Vector2(48f, 28f);
            _shieldCount = count.GetComponent<TMP_Text>();
            var theme = DealbreakUIStyle.Theme;
            if (theme) DealbreakUIStyle.Text(_shieldCount, theme.labelFont ? theme.labelFont : theme.headingFont, 20, theme.paper);
            _shieldCount.alignment = TextAlignmentOptions.MidlineLeft;
            _shieldCount.raycastTarget = false;
        }
        if (_shieldCount)
        {
            if (_shieldCount.gameObject.activeSelf != counted) _shieldCount.gameObject.SetActive(counted);
            if (counted) _shieldCount.SetText("×{0}", shields);
            _shieldCount.transform.SetAsLastSibling();
        }
        LayoutTagFlow();
    }

    void TickDressing()
    {
        if (_hatchStart >= 0f && _meterHatch)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _hatchStart) / Mathf.Max(.0001f, CutSheetMotion.Seconds(CutSheetMetrics.StatusCrossfadeSeconds)));
            SetAlpha(_meterHatch, t);
            if (t >= 1f) _hatchStart = -1f;
        }
        if (_glyphPopStart >= 0f)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _glyphPopStart) / Mathf.Max(.0001f, CutSheetMotion.Seconds(GlyphPopSeconds)));
            int last = -1;
            for (int i = 0; i < _shieldGlyphs.Count; i++) if (_shieldGlyphs[i].gameObject.activeSelf) last = i;
            if (last >= 0)
            {
                float s = 1f + GlyphPopScale * (1f - t);
                _shieldGlyphs[last].rectTransform.localScale = new Vector3(s, s, 1f);
            }
            if (t >= 1f) _glyphPopStart = -1f;
        }
    }

    static void SetAlpha(Graphic graphic, float alpha)
    {
        var color = graphic.color;
        if (Mathf.Approximately(color.a, alpha)) return;
        color.a = alpha;
        graphic.color = color;
    }

    Image Decorate(Image image, Sprite sprite, Color color, Image.Type type)
    {
        image.sprite = sprite;
        image.type = sprite ? type : Image.Type.Simple;
        image.color = color;
        return Decorate(image);
    }

    Image Decorate(Image image)
    {
        if (!image) return image;
        image.raycastTarget = false;
        if (!_decorations.Contains(image)) _decorations.Add(image);
        return image;
    }

    RectTransform Child(Transform parent, string name, bool image)
    {
        var existing = parent.Find(name);
        if (existing) return (RectTransform)existing;
        var item = image
            ? new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            : new GameObject(name, typeof(RectTransform));
        item.layer = gameObject.layer;
        var rect = (RectTransform)item.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    static void Row(RectTransform rect, float xMin, float xMax, float bottom, float top)
    {
        rect.anchorMin = new Vector2(xMin, 0f);
        rect.anchorMax = new Vector2(xMax, 0f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(0f, bottom);
        rect.offsetMax = new Vector2(0f, top);
    }

    static void Inset(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    static void Copy(RectTransform rect, RectTransform from)
    {
        rect.anchorMin = from.anchorMin;
        rect.anchorMax = from.anchorMax;
        rect.pivot = from.pivot;
        rect.anchoredPosition = from.anchoredPosition;
        rect.sizeDelta = from.sizeDelta;
    }

    // The portrait face moves (hover lift, hit shake); its shadow stays put.
    void TickHitCue(float deltaTime)
    {
        if (portrait == null) return;
        var offset = _enemyPointerHovered && _defeatCue == null && portrait.transform != transform
            ? CutSheetMetrics.HoverFaceOffset : Vector2.zero;
        if (_shakeTime > 0f)
        {
            _shakeTime = Mathf.Max(0f, _shakeTime - deltaTime);
            float remaining = _shakeTime / Mathf.Max(0.0001f, _shakeDuration);
            offset.x += Mathf.Sin((1f - remaining) * _shakeDuration * 140f) * _shakeStrength * remaining;
        }
        if (offset != _portraitShownOffset && portrait.transform != transform)
        {
            _portraitShownOffset = offset;
            portrait.rectTransform.anchoredPosition = _portraitRestPosition + offset;
        }
        if (_flashTime > 0f && _defeatCue == null)
        {
            _flashTime = Mathf.Max(0f, _flashTime - deltaTime);
            var state = CombatManager.Instance != null ? CombatManager.Instance.currentState : GameState.Idle;
            portrait.color = Color.Lerp(RestPortraitColor(state), _flashColor, _flashTime / HitFlashTime);
        }
    }

    void TickEmphasis(float deltaTime)
    {
        if (_defeatCue != null || _baseTargetScale == Vector3.zero) return;
        float entrance = 1f;
        if (IsEntering)
        {
            if (_entranceDelay > 0f)
            {
                _entranceDelay -= deltaTime;
                entrance = 0f;
            }
            else
            {
                _entranceElapsed += deltaTime;
                float t = Mathf.Clamp01(_entranceElapsed / EntranceTime);
                entrance = 1f - (1f - t) * (1f - t) * (1f - t);
            }
            if (_targetCanvasGroup != null) _targetCanvasGroup.alpha = _targetAlpha * entrance;
        }
        else if (Mathf.Approximately(_scaleFactor, _targetScaleFactor)) return;
        _scaleFactor = Mathf.SmoothDamp(_scaleFactor, _targetScaleFactor, ref _scaleVelocity,
            EmphasisSmoothTime, Mathf.Infinity, deltaTime);
        if (Mathf.Abs(_scaleFactor - _targetScaleFactor) < 0.0005f) _scaleFactor = _targetScaleFactor;
        transform.localScale = _baseTargetScale * (_scaleFactor * Mathf.Lerp(0.88f, 1f, entrance));
    }

    void OnEnable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnEnemyChanged += RefreshDisplay;
            CombatManager.Instance.OnEnemiesChanged += RefreshDisplay;
            CombatManager.Instance.OnEnemyHpChanged += RefreshHp;
            CombatManager.Instance.OnStateChanged += HandleStateChanged;
            CombatManager.Instance.OnPendingDamageChanged += HandlePendingDamageChanged;
            CombatManager.Instance.OnDefenseAttackResolved += HandleDefenseAttackResolved;
        }
        RefreshDisplay();
        HandleStateChanged(CombatManager.Instance != null ? CombatManager.Instance.currentState : GameState.Idle);
    }

    void OnDisable()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnEnemyChanged -= RefreshDisplay;
            CombatManager.Instance.OnEnemiesChanged -= RefreshDisplay;
            CombatManager.Instance.OnEnemyHpChanged -= RefreshHp;
            CombatManager.Instance.OnStateChanged -= HandleStateChanged;
            CombatManager.Instance.OnPendingDamageChanged -= HandlePendingDamageChanged;
            CombatManager.Instance.OnDefenseAttackResolved -= HandleDefenseAttackResolved;
        }
    }

    public bool IsDefeatCuePlaying => _defeatCue != null;
    /// <summary>Raised as an enemy's killing hit visibly lands, when its defeat cue starts to play.</summary>
    public static event System.Action<EnemyDisplayUI> DefeatHitLanded;
    public bool IsPerfectBlockCueVisible => _perfectBlockCueText != null && _perfectBlockCueText.gameObject.activeSelf;

    /// <summary>Plays the defeat fade once the killing hit visibly lands.</summary>
    public void PlayDefeatCue(float delay = 0f)
    {
        if (!isActiveAndEnabled) return;
        CancelDefeatCue();
        _entranceElapsed = EntranceTime;
        _preDefeatScale = transform.localScale;
        _preDefeatPortraitColor = portrait != null ? portrait.color : Color.white;
        _defeatCanvasGroup = GetComponent<CanvasGroup>();
        if (_defeatCanvasGroup == null) _defeatCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        _defeatCanvasGroup.alpha = _targetAlpha;
        _defeatCue = StartCoroutine(DefeatCueRoutine(delay));
    }

    public void CancelDefeatCue()
    {
        if (_defeatCue != null) StopCoroutine(_defeatCue);
        _defeatCue = null;
        if (_preDefeatScale != Vector3.zero) transform.localScale = _preDefeatScale;
        if (portrait != null && _preDefeatScale != Vector3.zero) portrait.color = _preDefeatPortraitColor;
        if (_defeatCanvasGroup != null) _defeatCanvasGroup.alpha = 1f;
    }

    // The killing hit plays in full (flash, shake, HP draining to zero) before the enemy fades.
    const float DefeatHitTime = 0.3f;
    const float DefeatFadeTime = 0.35f;

    System.Collections.IEnumerator DefeatCueRoutine(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        DefeatHitLanded?.Invoke(this);
        var rect = transform;
        Vector3 initialScale = _preDefeatScale;
        var hitColor = new Color(1f, 0.35f, 0.2f, 1f);
        var spentColor = new Color(0.45f, 0.42f, 0.42f, 1f);
        StartShake(ShakeTime * 1.5f);
        if (portrait != null) portrait.color = hitColor;
        float elapsed = 0f;
        while (elapsed < DefeatHitTime)
        {
            yield return null;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / DefeatHitTime);
            if (portrait != null) portrait.color = Color.Lerp(hitColor, spentColor, t * t);
        }
        elapsed = 0f;
        while (elapsed < DefeatFadeTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / DefeatFadeTime);
            float eased = t * t * (3f - 2f * t);
            rect.localScale = Vector3.LerpUnclamped(initialScale, initialScale * 0.86f, eased);
            if (_defeatCanvasGroup != null) _defeatCanvasGroup.alpha = 1f - eased;
            yield return null;
        }
        _defeatCue = null;
    }

    void RefreshDisplay()
    {
        var enemy = DisplayedEnemy;
        bool hasEnemy = enemy != null;

        if (nameText) nameText.text = hasEnemy ? (combatPortraits?combatPortraits.DisplayName(enemy):enemy.DisplayName).ToUpperInvariant() : "AWAITING ENCOUNTER";
        EnsureDressing();
        if (atkText)
        {
            int attack = hasEnemy ? enemy.EffectiveAttack : int.MinValue + 1;
            if (hasEnemy) atkText.SetText("ATTACK {0}", attack);
            else atkText.text = "ATTACK —";
            if (attack != _laidAttack) { _laidAttack = attack; atkText.ForceMeshUpdate(); LayoutTagFlow(); }
        }
        SyncAbilityTags(enemy);
        if (hasEnemy)
        {
            // In play mode HP changes drain over a few frames (see TickHp); otherwise show them at once.
            if (_shownHp < 0f || !Application.isPlaying || !isActiveAndEnabled) _shownHp = enemy.currentHp;
            ApplyHp(enemy);
        }
        else
        {
            _shownHp = -1f;
            if (hpText) hpText.text = "HP —";
            ApplyHpBar(0f);
        }

        RefreshPortrait(enemy);
        RefreshStateGuidance();
    }

    void RefreshPortrait(EnemyRuntime enemy)
    {
        if (!portrait) return;

        CardData headlinerCard = enemy?.type?.sourceCard;
        Sprite headlinerSprite = headlinerCard != null && headlinerCard.IsFaceCard
            ? CardManager.Instance?.cardPrefab?.ResolveCardSprite(headlinerCard)
            : null;
        Sprite enemySprite = enemy?.type ? enemy.type.portraitSprite : null;
        Sprite combatSprite = combatPortraits != null ? combatPortraits.Resolve(enemy?.type) : null;
        if (combatSprite != null) { enemySprite = combatSprite; headlinerSprite = null; }
        _isDisplayingHeadlinerCard = headlinerSprite != null;
        _isDisplayingAuthoredPortrait = !_isDisplayingHeadlinerCard && enemySprite != null;

        var rect = portrait.rectTransform;
        if (_isDisplayingHeadlinerCard || _isDisplayingAuthoredPortrait)
        {
            portrait.sprite = _isDisplayingHeadlinerCard ? headlinerSprite : enemySprite;
            portrait.type = Image.Type.Simple;
            portrait.preserveAspect = true;
            portrait.color = Color.white;
            rect.anchorMin = combatPortraits ? new Vector2(0.09f, 0.31f) : new Vector2(0.03f, 0.08f);
            rect.anchorMax = combatPortraits ? new Vector2(0.91f, 0.92f) : new Vector2(0.30f, 0.92f);
            if(rect.parent.name=="PortraitWindow")
            {rect.anchorMin=new Vector2(-.025f,-.07f);rect.anchorMax=new Vector2(1.025f,1.03f);}
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            _portraitRestPosition = Vector2.zero;
        }
        else
        {
            portrait.sprite = _placeholderSprite;
            portrait.type = _placeholderImageType;
            portrait.preserveAspect = _placeholderPreserveAspect;
            portrait.color = _placeholderColor;
            rect.anchorMin = _portraitAnchorMin;
            rect.anchorMax = _portraitAnchorMax;
            rect.anchoredPosition = _portraitAnchoredPosition;
            rect.sizeDelta = _portraitSizeDelta;
            _portraitRestPosition = _portraitAnchoredPosition;
        }
        _portraitShownOffset = Vector2.zero;
        SyncPortraitShadow();

        // Until an enemy has commissioned art, its placeholder is typographic: the name set huge
        // over a dark halftone field.
        if (_portraitGlyph)
        {
            bool placeholder = !_isDisplayingHeadlinerCard && !_isDisplayingAuthoredPortrait;
            _portraitGlyph.gameObject.SetActive(placeholder);
            if (placeholder && nameText) _portraitGlyph.text = nameText.text.Replace(' ', '\n');
            // Each fact shows once: the big type is the name, so the name tag waits for real art.
            if (nameText) nameText.enabled = !placeholder;
        }
    }

    void RefreshHp() => RefreshDisplay();
    int _shownShields = -1;
    void LateUpdate()
    {
        // Shield comes and goes without an HP change (Royal Guard, a blocked hit): keep the bar in step.
        var shown = DisplayedEnemy;
        if (shown != null && _shownHp >= 0f && shown.ShieldCharges != _shownShields)
        {
            _shownShields = shown.ShieldCharges;
            ApplyHp(shown);
        }
        if (!Application.isPlaying) return;
        // A later styling pass may switch the retired frame back on.
        if (_viewFrame && _viewFrame.enabled) _viewFrame.enabled = false;
        RefreshTargetMarks();
        SyncFocusRing();
        TickDressing();
    }
    void HandlePendingDamageChanged(int _) => RefreshStateGuidance();

    void HandleDefenseAttackResolved(DefenseAttackResult result)
    {
        if (result.IsPerfectBlock && ReferenceEquals(result.Attacker, DisplayedEnemy))
            ShowPerfectBlockCue();
    }

    void ShowPerfectBlockCue()
    {
        if (!isActiveAndEnabled) return;
        EnsurePerfectBlockCue();
        if (_perfectBlockCueText == null) return;
        if (_perfectBlockCueFrame == Time.frameCount) _perfectBlockCueCount++;
        else { _perfectBlockCueFrame = Time.frameCount; _perfectBlockCueCount = 1; }
        _perfectBlockCueText.text = _perfectBlockCueCount > 1
            ? $"PERFECT BLOCK ×{_perfectBlockCueCount}" : "PERFECT BLOCK";
        _perfectBlockCueText.gameObject.SetActive(true);
        if (_perfectBlockCue != null) StopCoroutine(_perfectBlockCue);
        _perfectBlockCue = Application.isPlaying ? StartCoroutine(PerfectBlockCueRoutine()) : null;
    }

    void EnsurePerfectBlockCue()
    {
        if (_perfectBlockCueText != null) return;
        var cue = new GameObject("Perfect Block Cue", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        cue.transform.SetParent(transform, false);
        var rect = (RectTransform)cue.transform;
        rect.anchorMin = new Vector2(0.28f, 0.25f);
        rect.anchorMax = new Vector2(0.96f, 0.72f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        _perfectBlockCueText = cue.GetComponent<TMP_Text>();
        if (statusText != null) _perfectBlockCueText.font = statusText.font;
        _perfectBlockCueText.fontSize = 34f;
        _perfectBlockCueText.fontStyle = FontStyles.Bold;
        _perfectBlockCueText.alignment = TextAlignmentOptions.Center;
        _perfectBlockCueText.color = new Color(1f, 0.82f, 0.32f, 1f);
        _perfectBlockCueText.raycastTarget = false;
        DealbreakUIStyle.Outline(_perfectBlockCueText, 0.3f);
        cue.SetActive(false);
    }

    System.Collections.IEnumerator PerfectBlockCueRoutine()
    {
        var rect = _perfectBlockCueText.rectTransform;
        Vector2 start = rect.anchoredPosition;
        const float duration = 0.85f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _perfectBlockCueText.alpha = 1f - t;
            rect.anchoredPosition = start + Vector2.up * (20f * t);
            yield return null;
        }
        _perfectBlockCueText.gameObject.SetActive(false);
        _perfectBlockCue = null;
    }

    public void RefreshStateGuidance()
    {
        HandleStateChanged(CombatManager.Instance != null ? CombatManager.Instance.currentState : GameState.Idle);
    }

    void HandleStateChanged(GameState state)
    {
        if (statusText)
        {
            // Enemy facts only: phase hints and the pending total live on the player side.
            string guidance = string.Empty;
            // S14: the detail leaves out what the enemy itself shows (Shield, Double Strike, charged attack).
            string detail = string.Empty;
            var enemy = DisplayedEnemy;
            if (enemy != null && state == GameState.PlayerTurn && enemy.HasDoubleStrike)
            {
                int budget = enemy.ResponseAttack + (enemy.HasOppression
                    ? Mathf.Max(0, (CardManager.Instance?.HandCount ?? 0) - 3) * 2 : 0);
                guidance += $"\nDouble Strike next: 2 hits, {Mathf.FloorToInt(budget * 0.6f + 0.5f)} damage each";
            }
            else if (enemy != null && state == GameState.EnemyAttacking && enemy.HasDoubleStrike)
                guidance = $"Double Strike incoming: 2 hits, {Mathf.FloorToInt(enemy.PreparedResponseBudget * 0.6f + 0.5f)} damage each";
            if (enemy != null && enemy.HasSuitCall)
                { string line = $"\nCalled suit: {enemy.AnnouncedSuit}{(enemy.DuelistCallConsumed ? " (used)" : " (first matching hit -50%)")}"; guidance += line; detail += line; }
            if (enemy != null && enemy.HasSilence)
                { string line = "\nSilence active: held-card effects disabled"; guidance += line; detail += line; }
            else if (enemy != null && enemy.HasAbilityEffect(EnemyAbilityEffect.Silence))
                { string line = "\nSilence inactive at/below 50% HP"; guidance += line; detail += line; }
            if (enemy != null && enemy.HasWithering)
                { string line = "\nWithering active: player healing prevented"; guidance += line; detail += line; }
            else if (enemy != null && enemy.HasAbilityEffect(EnemyAbilityEffect.Withering))
                { string line = "\nWithering inactive at/below 50% HP"; guidance += line; detail += line; }
            if (enemy != null && enemy.HasOppression && state == GameState.PlayerTurn)
                { string line = $"\nOppression: +{Mathf.Max(0, (CardManager.Instance?.HandCount ?? 0) - 3) * 2} next response damage"; guidance += line; detail += line; }
            if (enemy != null && state == GameState.PlayerTurn && enemy.NextAttackIsCharged)
                guidance += $"\nWarning: charged attack next ({enemy.EffectiveAttack * 2} damage)";
            else if (enemy != null && state == GameState.EnemyAttacking && enemy.CurrentResponseIsCharged)
                guidance = $"Charged attack incoming ({enemy.ResponseAttack} damage)";
            if (enemy != null && state == GameState.PlayerTurn && enemy.HasRoyalGuard && !enemy.NextAttackIsCharged)
                { string line = $"\nNext: +1 Shield and {enemy.EffectiveAttack} damage"; guidance += line; detail += line; }
            if (enemy != null && enemy.ShieldCharges > 0)
                guidance += $"\nShield: {enemy.ShieldCharges}";
            if (enemy != null && enemy.CaptainAttackBonus > 0)
                { string line = "\nCaptain alive: +1 ATK"; guidance += line; detail += line; }
            guidance = guidance.TrimStart('\n');
            DetailGuidance = detail.TrimStart('\n');
            // Ability names and rules read on the ability tags (owner 2026-10-29), not here.
            statusText.text = CombatDetailPanelUI.IsCombatActive ? CompactIntent(enemy, state) : guidance;
            // Nothing to say (an ordinary attack): the line hides rather than showing an authored default.
            bool hasLine = !string.IsNullOrWhiteSpace(statusText.text);
            if (statusText.enabled != hasLine) statusText.enabled = hasLine;
        }

        // A running hit flash or defeat cue owns the tint until it ends.
        if (portrait && _flashTime <= 0f && _defeatCue == null) portrait.color = RestPortraitColor(state);
    }

    Color RestPortraitColor(GameState state)
    {
        if (_isDisplayingHeadlinerCard || _isDisplayingAuthoredPortrait) return Color.white;
        bool isSelectedTarget = DisplayedEnemy != null &&
            ReferenceEquals(CombatManager.Instance?.currentEnemy, DisplayedEnemy);
        return state == GameState.GameWon ? new Color(0.35f, 0.75f, 0.48f)
            : state == GameState.GameOver ? new Color(0.75f, 0.28f, 0.3f)
            : isSelectedTarget ? new Color(0.42f, 0.52f, 0.72f)
            : new Color(0.32f, 0.38f, 0.5f);
    }

    static string CompactIntent(EnemyRuntime enemy, GameState state)
    {
        if (enemy == null) return string.Empty;
        // Only intents that differ from an ordinary attack; the pending total is on the HP panel.
        var parts = new System.Collections.Generic.List<string>(4);
        if (state != GameState.EnemyAttacking && enemy.NextAttackIsCharged) parts.Add("CHARGED ATTACK NEXT");
        else if (state != GameState.EnemyAttacking && enemy.HasRoyalGuard) parts.Add("+1 SHIELD NEXT");
        if (enemy.HasDoubleStrike) parts.Add("2 HITS");
        // Shield shows as glyphs on the HP bar, not as text.
        if (enemy.type.sourceCard) parts.Add(enemy.type.sourceCard.DisplayName);
        // Abilities are tags under the enemy (owner 2026-10-29); this line carries intents only.
        return string.Join(" • ", parts);
    }
}
