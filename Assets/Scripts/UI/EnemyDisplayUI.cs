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
    Outline _targetOutline;
    Vector3 _baseTargetScale;

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
        var frame=transform.Find("Editorial Frame")?.GetComponent<EditorialFrame>();
        if(frame&&DealbreakUIStyle.Theme)
        {frame.rim=_enemyTargeting&&_enemyTargetEligible?DealbreakUIStyle.Theme.ochre:DealbreakUIStyle.Theme.slate;frame.border=_enemyTargeting&&_enemyTargetEligible?6:3;frame.Refresh();}
        if (!_enemyTargeting)
        {
            if (_targetCanvasGroup != null) _targetCanvasGroup.alpha = 1f;
            if (_targetOutline != null) _targetOutline.enabled = false;
            if (_baseTargetScale != Vector3.zero) transform.localScale = _baseTargetScale;
            return;
        }
        if (statusText) statusText.text = _enemyTargetEligible ? "VALID TARGET" : "Not a valid target";
        if (_targetCanvasGroup != null) _targetCanvasGroup.alpha = _enemyTargetEligible ? 1f : 0.38f;
        if (_targetOutline != null)
        {
            _targetOutline.enabled = _enemyTargetEligible;
            _targetOutline.effectColor = _enemyPointerHovered
                ? new Color(0.87f, 0.67f, 0.28f, 1f)
                : new Color(0.94f, 0.89f, 0.77f, 1f);
        }
        float scale = _enemyTargetEligible ? (_enemyPointerHovered ? 1.07f : 1.035f) : 0.96f;
        transform.localScale = _baseTargetScale * scale;
    }

    public void Bind(EnemyRuntime enemy)
    {
        _hasExplicitBinding = true;
        _boundEnemy = enemy;
        RefreshDisplay();
        RefreshStateGuidance();
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

    public void PlayDefeatCue()
    {
        if (!isActiveAndEnabled) return;
        CancelDefeatCue();
        _preDefeatScale = transform.localScale;
        _preDefeatPortraitColor = portrait != null ? portrait.color : Color.white;
        _defeatCanvasGroup = GetComponent<CanvasGroup>();
        if (_defeatCanvasGroup == null) _defeatCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        _defeatCue = StartCoroutine(DefeatCueRoutine());
    }

    public void CancelDefeatCue()
    {
        if (_defeatCue != null) StopCoroutine(_defeatCue);
        _defeatCue = null;
        if (_preDefeatScale != Vector3.zero) transform.localScale = _preDefeatScale;
        if (portrait != null && _preDefeatScale != Vector3.zero) portrait.color = _preDefeatPortraitColor;
        if (_defeatCanvasGroup != null) _defeatCanvasGroup.alpha = 1f;
    }

    System.Collections.IEnumerator DefeatCueRoutine()
    {
        var rect = (RectTransform)transform;
        Vector3 initialScale = _preDefeatScale;
        float elapsed = 0f;
        if (portrait != null) portrait.color = new Color(1f, 0.35f, 0.2f, 1f);
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

        if (nameText) nameText.text = hasEnemy ? (combatPortraits?combatPortraits.DisplayName(enemy):enemy.DisplayName).ToUpperInvariant() : "AWAITING ENCOUNTER";
        if (hpText) hpText.text = hasEnemy ? $"HP  {enemy.currentHp}/{enemy.maxHp}" : "HP  —";
        if (atkText) atkText.text = hasEnemy ? $"ATTACK  {enemy.EffectiveAttack}" : "ATTACK  —";
        if (healthBar)
        {
            healthBar.minValue = 0;
            healthBar.maxValue = hasEnemy ? Mathf.Max(1, enemy.maxHp) : 1;
            healthBar.value = hasEnemy ? enemy.currentHp : 0;
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
        }

        if (_portraitGlyph)
            _portraitGlyph.gameObject.SetActive(!_isDisplayingHeadlinerCard && !_isDisplayingAuthoredPortrait);
    }

    void RefreshHp() => RefreshDisplay();
    void LateUpdate()
    {
        if (!CombatDetailPanelUI.IsCombatActive || _enemyTargeting || IsDefeatCuePlaying) return;
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        bool selected = cards != null && cards.SelectedCards.Count > 0 && combat.currentState == GameState.PlayerTurn;
        bool valid = selected && combat.CanPlayCards(cards.SelectedCards, DisplayedEnemy);
        bool inspecting = TryGetComponent<CombatInspectUI>(out var inspect) && inspect.IsInspected;
        if (portrait && _targetOutline == null)
        {
            _targetOutline = portrait.GetComponent<Outline>() ?? portrait.gameObject.AddComponent<Outline>();
            _targetOutline.effectDistance = new Vector2(3f, -3f);
        }
        if (_targetOutline)
        {
            _targetOutline.enabled = selected || inspecting;
            _targetOutline.effectColor = !selected || valid ? new Color(0.94f, 0.89f, 0.77f) : new Color(0.62f, 0.24f, 0.20f);
        }
        if (statusText) statusText.text = CompactIntent(DisplayedEnemy, combat.currentState) +
            (selected ? valid ? "\nVALID TARGET" : "\nINVALID TARGET" : string.Empty);
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
            else if (enemy != null && enemy.HasAbilityEffect(EnemyAbilityEffect.Silence))
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
            DetailGuidance = guidance;
            string abilities = enemy?.AbilitySummary;
            statusText.text = CombatDetailPanelUI.IsCombatActive ? CompactIntent(enemy, state) : string.IsNullOrEmpty(abilities)
                ? guidance
                : $"{guidance}\nTrait: {abilities}";
        }

        if (portrait)
        {
            if (_isDisplayingHeadlinerCard || _isDisplayingAuthoredPortrait)
            {
                portrait.color = Color.white;
                return;
            }

            bool isSelectedTarget = DisplayedEnemy != null &&
                ReferenceEquals(CombatManager.Instance?.currentEnemy, DisplayedEnemy);
            portrait.color = state == GameState.GameWon ? new Color(0.35f, 0.75f, 0.48f)
                : state == GameState.GameOver ? new Color(0.75f, 0.28f, 0.3f)
                : isSelectedTarget ? new Color(0.42f, 0.52f, 0.72f)
                : new Color(0.32f, 0.38f, 0.5f);
        }
    }

    static string CompactIntent(EnemyRuntime enemy, GameState state)
    {
        if (enemy == null) return string.Empty;
        string intent = state == GameState.EnemyAttacking
            ? $"INCOMING {CombatManager.Instance.GetPendingAttackDamage(enemy)}"
            : enemy.NextAttackIsCharged ? "CHARGED ATTACK NEXT" : "ATTACK NEXT";
        if (enemy.HasDoubleStrike) intent += " • 2 HITS";
        if (enemy.ShieldCharges > 0) intent += $" • SHIELD {enemy.ShieldCharges}";
        if (enemy.type.sourceCard) intent += $" • {enemy.type.sourceCard.DisplayName}";
        return intent;
    }
}
