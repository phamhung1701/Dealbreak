using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The run's Credits as a Cut Sheet chip just above the HP panel, so it shows wherever the HP panel
/// does (HUD slice S10a): a mustard coin disc (chip sprite + the coin art) over a mustard number
/// plate (tag sprite), each with a 3px ink shadow, the number in Courier Prime Bold 28 ink, a paper
/// ring on hover. A defeated enemy's Credits fly from it into the coin as the killing hit lands, the
/// number counts up once they arrive, and the chip stamps (120ms, scale 1.12) as they land.
/// Presentation only.
/// </summary>
public sealed class CreditsHudUI : MonoBehaviour
{
    public static CreditsHudUI Instance { get; private set; }
    public const float Height = PlayerCornerLayout.ChipHeight, Width = PlayerCornerLayout.ChipWidth,
        Gap = PlayerCornerLayout.ChipGap, PlateHeight = PlayerCornerLayout.PlateHeight;
    public const float NumberSize = 28f, CoinInset = 7f, StampSeconds = .12f, StampScale = 1.12f;
    public const string PlateName = "Credits Plate", CoinName = "Credits Coin", DiscName = "Disc", CoinArtName = "Coin Art",
        RingName = "Credits Ring";

    RectTransform _coin;
    Image _disc, _plate, _coinArt;
    TMP_Text _label;
    Sprite _coinSprite;
    float _holdUntil;
    float _stampStart = float.NegativeInfinity;
    // Defeat flights land at known times; a handful can overlap in one kill chain.
    readonly float[] _landings = new float[8];
    int _landingCount;
    bool _triggerCreditsHeld;

    /// <summary>
    /// True while Credits shown on the HUD should wait: an attack or defeat is still playing out, or
    /// Credits are still flying to the coin (from a defeat, or from a Mod or Gear trigger).
    /// </summary>
    public static bool HoldsCredits =>
        AttackCardPresentationUI.Instance && AttackCardPresentationUI.Instance.IsBoardBusy ||
        EffectTriggerPresenterUI.HoldsCredits || Instance && Time.unscaledTime < Instance._holdUntil;

    public RectTransform Coin => _coin;
    public Image Disc => _disc;
    public Image Plate => _plate;
    public Image CoinArt => _coinArt;
    public TMP_Text Label => _label;
    public Sprite CoinSprite => _coinSprite;
    /// <summary>The one coin every Credits flight and tag uses: the chip's coin, else the theme's Credits icon.</summary>
    public static Sprite CurrentCoin => Instance && Instance._coinSprite ? Instance._coinSprite
        : DealbreakUIStyle.Theme ? DealbreakUIStyle.Theme.creditsIcon : null;

    /// <summary>
    /// Builds the chip above <paramref name="healthPanel"/> and moves the Credits label into it. The
    /// coin art is <paramref name="coin"/>, else the HP panel's <see cref="PlayerCornerUI.coinSprite"/>,
    /// else the theme's Credits icon.
    /// </summary>
    public static CreditsHudUI Install(RectTransform healthPanel, TMP_Text creditsLabel, Sprite coin = null)
    {
        if (!healthPanel || !creditsLabel) return null;
        var existing = healthPanel.GetComponentInChildren<CreditsHudUI>(true);
        if (existing) return existing;
        var theme = DealbreakUIStyle.Theme;
        if (!coin && healthPanel.TryGetComponent<PlayerCornerUI>(out var corner)) coin = corner.coinSprite;
        if (!coin && theme) coin = theme.creditsIcon;
        Color mustard = theme ? theme.mustard : new Color32(0xE6, 0xAB, 0x55, 0xFF);
        Color ink = theme ? theme.inkPrint : CutSheetShadow.FallbackTint;

        var chip = new GameObject("Credits Chip", typeof(RectTransform), typeof(CreditsHudUI));
        chip.layer = healthPanel.gameObject.layer;
        var rect = (RectTransform)chip.transform;
        rect.SetParent(healthPanel, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        // Centred pivot so the stamp scales about the chip's middle.
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(Width, Height);
        rect.anchoredPosition = new Vector2(Width * .5f, Gap + Height * .5f);

        // The plate runs from the disc's centre to the chip's right end, 38px tall.
        var plateHolder = DealbreakUIStyle.Child(rect, PlateName);
        plateHolder.anchorMin = new Vector2(0f, .5f);
        plateHolder.anchorMax = new Vector2(1f, .5f);
        plateHolder.pivot = new Vector2(.5f, .5f);
        plateHolder.offsetMin = new Vector2(Height * .5f, -PlateHeight * .5f);
        plateHolder.offsetMax = new Vector2(0f, PlateHeight * .5f);
        var plate = Face(plateHolder, "Face", theme ? theme.tagSprite : null, mustard, true);

        var coinRect = DealbreakUIStyle.Child(rect, CoinName);
        coinRect.anchorMin = new Vector2(0f, 0f);
        coinRect.anchorMax = new Vector2(0f, 1f);
        coinRect.pivot = new Vector2(.5f, .5f);
        coinRect.sizeDelta = new Vector2(Height, 0f);
        coinRect.anchoredPosition = new Vector2(Height * .5f, 0f);
        var disc = Face(coinRect, DiscName, theme ? theme.chipSprite : null, mustard, true);
        disc.type = Image.Type.Simple;
        var art = DealbreakUIStyle.Child(coinRect, CoinArtName, typeof(CanvasRenderer), typeof(Image));
        art.anchorMin = Vector2.zero;
        art.anchorMax = Vector2.one;
        art.offsetMin = Vector2.one * CoinInset;
        art.offsetMax = -Vector2.one * CoinInset;
        var artImage = art.GetComponent<Image>();
        artImage.sprite = coin;
        artImage.preserveAspect = true;
        artImage.raycastTarget = false;
        artImage.enabled = coin;

        var label = creditsLabel.rectTransform;
        label.SetParent(rect, false);
        label.anchorMin = Vector2.zero;
        label.anchorMax = Vector2.one;
        label.pivot = new Vector2(0f, .5f);
        label.offsetMin = new Vector2(Height + 8f, 0f);
        label.offsetMax = new Vector2(-8f, 0f);
        if (theme) DealbreakUIStyle.Text(creditsLabel, theme.numericFont, NumberSize, ink);
        creditsLabel.alignment = TextAlignmentOptions.MidlineLeft;
        creditsLabel.textWrappingMode = TextWrappingModes.NoWrap;
        creditsLabel.overflowMode = TextOverflowModes.Overflow;
        creditsLabel.raycastTarget = true;

        var ring = CutSheetHoverRing.CreateRing(rect, RingName, 2f);
        CutSheetHoverRing.Attach(chip, ring, true, true);

        var hud = chip.GetComponent<CreditsHudUI>();
        hud._coin = coinRect;
        hud._disc = disc;
        hud._plate = plate;
        hud._coinArt = artImage;
        hud._label = creditsLabel;
        hud._coinSprite = coin;
        return hud;
    }

    // A face Image over its own 3px ink shadow, both filling the holder.
    static Image Face(RectTransform holder, string name, Sprite sprite, Color tint, bool raycast)
    {
        var shadowHolder = holder.Find(CutSheetShadow.ObjectName);
        var shadow = shadowHolder ? shadowHolder.GetComponent<CutSheetShadow>() : CutSheetShadow.Create(holder);
        var face = DealbreakUIStyle.Child(holder, name, typeof(CanvasRenderer), typeof(Image));
        face.anchorMin = Vector2.zero;
        face.anchorMax = Vector2.one;
        face.offsetMin = face.offsetMax = Vector2.zero;
        var image = face.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sprite && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.color = tint;
        image.raycastTarget = raycast;
        if (shadow) shadow.Match(image, CutSheetShadow.Tint, CutSheetMetrics.ShadowSlot);
        return image;
    }

    void OnEnable()
    {
        Instance = this;
        EnemyDisplayUI.DefeatHitLanded += HandleDefeatHitLanded;
    }

    void OnDisable()
    {
        EnemyDisplayUI.DefeatHitLanded -= HandleDefeatHitLanded;
        if (Instance == this) Instance = null;
        _landingCount = 0;
        transform.localScale = Vector3.one;
    }

    /// <summary>The stamp a coin landing plays: 1.12 settling to 1 over 120ms; nothing under Reduce Motion.</summary>
    public void Stamp()
    {
        if (CutSheetMotion.Seconds(StampSeconds) <= 0f) return;
        _stampStart = Time.unscaledTime;
    }

    void LateUpdate()
    {
        float now = Time.unscaledTime;
        for (int i = _landingCount - 1; i >= 0; i--)
            if (_landings[i] <= now) { _landings[i] = _landings[--_landingCount]; AudioService.Play(AudioCue.CoinLand); Stamp(); }
        // A Mod or Gear trigger's Credits chips: stamp once they have all landed.
        bool held = EffectTriggerPresenterUI.HoldsCredits;
        if (_triggerCreditsHeld && !held) Stamp();
        _triggerCreditsHeld = held;
        if (float.IsNegativeInfinity(_stampStart)) return;
        float duration = CutSheetMotion.Seconds(StampSeconds);
        float t = duration > 0f ? (now - _stampStart) / duration : 1f;
        if (t >= 1f) { _stampStart = float.NegativeInfinity; transform.localScale = Vector3.one; }
        else transform.localScale = Vector3.one * Mathf.Lerp(StampScale, 1f, t);
    }

    // Fleeing enemies play no defeat, so they send no Credits.
    void HandleDefeatHitLanded(EnemyDisplayUI view)
    {
        var enemy = view ? view.DisplayedEnemy : null;
        if (enemy == null || enemy.CreditsReward <= 0 || !_coin) return;
        var flight = ItemDragUI.Get();
        if (!flight) return;
        Transform from = view.portrait ? view.portrait.transform : view.transform;
        flight.FlyIntoSlot(from.position, _coin, $"+{enemy.CreditsReward}", _coinSprite);
        float landing = Time.unscaledTime + ItemDragUI.FlightSeconds;
        _holdUntil = Mathf.Max(_holdUntil, landing);
        if (_landingCount < _landings.Length) _landings[_landingCount++] = landing;
    }
}
