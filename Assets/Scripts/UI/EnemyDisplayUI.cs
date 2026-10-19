using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "EnemyDisplayUI")]
public class EnemyDisplayUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    public TMP_Text nameText;
    public TMP_Text hpText;
    public TMP_Text atkText;
    public TMP_Text statusText;
    public Image portrait;
    public Slider healthBar;

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
    Outline _targetOutline;
    Vector3 _baseTargetScale;

    // Presentation-only motion: HP drain, hit shake/flash, entrance and target emphasis.
    // Gameplay values are read from EnemyRuntime; these only ease how they are shown.
    const float HpSmoothTime = 0.14f;
    const float ShakeTime = 0.24f;
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
            _targetCanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        if (_enemyTargeting && portrait != null && _targetOutline == null)
        {
            _targetOutline = portrait.GetComponent<Outline>() ?? portrait.gameObject.AddComponent<Outline>();
            _targetOutline.effectDistance = new Vector2(3f, -3f);
        }
        RefreshEnemyTargetPresentation();
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

    void RefreshEnemyTargetPresentation()
    {
        if (!_enemyTargeting)
        {
            _targetAlpha = 1f;
            if (_targetCanvasGroup != null && !IsEntering) _targetCanvasGroup.alpha = 1f;
            if (_targetOutline != null) _targetOutline.enabled = false;
            SetEmphasis(1f);
            return;
        }
        if (statusText) statusText.text = _enemyTargetEligible ? "Click to target" : "Not a valid target";
        _targetAlpha = _enemyTargetEligible ? 1f : 0.38f;
        if (_targetCanvasGroup != null && !IsEntering) _targetCanvasGroup.alpha = _targetAlpha;
        if (_targetOutline != null)
        {
            _targetOutline.enabled = _enemyTargetEligible;
            _targetOutline.effectColor = _enemyPointerHovered
                ? new Color(1f, 0.86f, 0.32f, 1f)
                : new Color(0.36f, 1f, 0.52f, 1f);
        }
        SetEmphasis(_enemyTargetEligible ? (_enemyPointerHovered ? 1.07f : 1.035f) : 0.96f);
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
        _shakeTime = ShakeTime;
        _shakeStrength = blocked ? 5f : 10f;
        _flashTime = HitFlashTime;
        _flashColor = blocked ? new Color(0.55f, 0.9f, 1f, 1f) : new Color(1f, 0.42f, 0.36f, 1f);
    }

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
        if (hpText) hpText.text = $"HP  {Mathf.RoundToInt(_shownHp)}/{enemy.maxHp}";
        if (healthBar)
        {
            healthBar.minValue = 0;
            healthBar.maxValue = Mathf.Max(1, enemy.maxHp);
            healthBar.value = _shownHp;
        }
    }

    void TickHitCue(float deltaTime)
    {
        if (portrait == null) return;
        if (_shakeTime > 0f)
        {
            _shakeTime = Mathf.Max(0f, _shakeTime - deltaTime);
            float remaining = _shakeTime / ShakeTime;
            float offset = Mathf.Sin((1f - remaining) * ShakeTime * 70f) * _shakeStrength * remaining;
            portrait.rectTransform.anchoredPosition = _portraitRestPosition + new Vector2(offset, 0f);
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

    System.Collections.IEnumerator DefeatCueRoutine(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        var rect = (RectTransform)transform;
        Vector3 initialScale = _preDefeatScale;
        float elapsed = 0f;
        // Brief warm impact flash, then a compact fade/shrink; presentation only.
        if (portrait != null) portrait.color = new Color(1f, 0.35f, 0.2f, 1f);
        yield return new WaitForSeconds(0.09f);
        while (elapsed < 0.39f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.39f);
            float eased = t * t * (3f - 2f * t);
            rect.localScale = Vector3.LerpUnclamped(initialScale, initialScale * 0.72f, eased);
            if (_defeatCanvasGroup != null) _defeatCanvasGroup.alpha = 1f - t;
            yield return null;
        }
        _defeatCue = null;
    }

    void RefreshDisplay()
    {
        var enemy = DisplayedEnemy;
        bool hasEnemy = enemy != null;

        if (nameText) nameText.text = hasEnemy ? enemy.DisplayName.ToUpperInvariant() : "AWAITING ENCOUNTER";
        if (atkText) atkText.text = hasEnemy ? $"ATTACK  {enemy.EffectiveAttack}" : "ATTACK  —";
        if (hasEnemy)
        {
            // In play mode HP changes drain over a few frames (see TickHp); otherwise show them at once.
            if (_shownHp < 0f || !Application.isPlaying || !isActiveAndEnabled) _shownHp = enemy.currentHp;
            ApplyHp(enemy);
        }
        else
        {
            _shownHp = -1f;
            if (hpText) hpText.text = "HP  —";
            if (healthBar)
            {
                healthBar.minValue = 0;
                healthBar.maxValue = 1;
                healthBar.value = 0;
            }
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
        _isDisplayingHeadlinerCard = headlinerSprite != null;
        _isDisplayingAuthoredPortrait = !_isDisplayingHeadlinerCard && enemySprite != null;

        var rect = portrait.rectTransform;
        if (_isDisplayingHeadlinerCard || _isDisplayingAuthoredPortrait)
        {
            portrait.sprite = _isDisplayingHeadlinerCard ? headlinerSprite : enemySprite;
            portrait.type = Image.Type.Simple;
            portrait.preserveAspect = true;
            portrait.color = Color.white;
            rect.anchorMin = new Vector2(0.03f, 0.08f);
            rect.anchorMax = new Vector2(0.30f, 0.92f);
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

        if (_portraitGlyph)
            _portraitGlyph.gameObject.SetActive(!_isDisplayingHeadlinerCard && !_isDisplayingAuthoredPortrait);
    }

    void RefreshHp() => RefreshDisplay();
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
        _perfectBlockCueText.fontSize = 25f;
        _perfectBlockCueText.fontStyle = FontStyles.Bold;
        _perfectBlockCueText.alignment = TextAlignmentOptions.Center;
        _perfectBlockCueText.color = new Color(1f, 0.82f, 0.32f, 1f);
        _perfectBlockCueText.raycastTarget = false;
        var outline = cue.GetComponent<Outline>() ?? cue.AddComponent<Outline>();
        outline.effectColor = new Color(0.12f, 0.08f, 0.02f, 1f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = false;
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
            string guidance = state switch
            {
                GameState.PlayerTurn => "Choose a card to attack",
                GameState.EnemyAttacking => "Enemy attack incoming",
                GameState.GameWon => "Encounter cleared",
                GameState.GameOver => "Player defeated",
                _ => "Choose your next encounter"
            };
            var enemy = DisplayedEnemy;
            if (enemy != null && state == GameState.EnemyAttacking)
            {
                int incoming = CombatManager.Instance != null
                    ? CombatManager.Instance.GetPendingAttackDamage(enemy) : 0;
                guidance += incoming > 0 ? $"\nINCOMING: {incoming} DAMAGE" : "\nBLOCKED";
            }
            if (enemy != null && state == GameState.PlayerTurn && enemy.HasDoubleStrike)
            {
                int budget = enemy.ResponseAttack + (enemy.HasOppression
                    ? Mathf.Max(0, (CardManager.Instance?.HandCount ?? 0) - 3) * 2 : 0);
                guidance += $"\nDouble Strike next: 2 hits, {Mathf.FloorToInt(budget * 0.6f + 0.5f)} damage each";
            }
            else if (enemy != null && state == GameState.EnemyAttacking && enemy.HasDoubleStrike)
                guidance = $"Double Strike incoming: 2 hits, {Mathf.FloorToInt(enemy.PreparedResponseBudget * 0.6f + 0.5f)} damage each";
            if (enemy != null && enemy.HasSuitCall)
                guidance += $"\nCalled suit: {enemy.AnnouncedSuit}{(enemy.DuelistCallConsumed ? " (used)" : " (first matching hit -50%)")}";
            if (enemy != null && enemy.HasSilence)
                guidance += "\nSilence active: held-card effects disabled";
            if (enemy != null && enemy.HasAbilityEffect(EnemyAbilityEffect.Silence))
                guidance += "\nSilence inactive at/below 50% HP";
            if (enemy != null && enemy.HasWithering)
                guidance += "\nWithering active: player healing prevented";
            else if (enemy != null && enemy.HasAbilityEffect(EnemyAbilityEffect.Withering))
                guidance += "\nWithering inactive at/below 50% HP";
            if (enemy != null && enemy.HasOppression && state == GameState.PlayerTurn)
                guidance += $"\nOppression: +{Mathf.Max(0, (CardManager.Instance?.HandCount ?? 0) - 3) * 2} next response damage";
            if (enemy != null && state == GameState.PlayerTurn && enemy.NextAttackIsCharged)
                guidance += $"\nWarning: charged attack next ({enemy.EffectiveAttack * 2} damage)";
            else if (enemy != null && state == GameState.EnemyAttacking && enemy.CurrentResponseIsCharged)
                guidance = $"Charged attack incoming ({enemy.ResponseAttack} damage)";
            if (enemy != null && state == GameState.PlayerTurn && enemy.HasRoyalGuard && !enemy.NextAttackIsCharged)
                guidance += $"\nNext: +1 Shield and {enemy.EffectiveAttack} damage";
            if (enemy != null && enemy.ShieldCharges > 0)
                guidance += $"\nShield: {enemy.ShieldCharges}";
            if (enemy != null && enemy.CaptainAttackBonus > 0)
                guidance += "\nCaptain alive: +1 ATK";
            string abilities = enemy?.AbilitySummary;
            statusText.text = string.IsNullOrEmpty(abilities)
                ? guidance
                : $"{guidance}\nTrait: {abilities}";
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
}
