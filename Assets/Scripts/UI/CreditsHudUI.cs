using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The run's Credits as a coin and a number just above the HP panel, so it shows wherever the HP
/// panel does. A defeated enemy's Credits fly from it into the coin as the killing hit lands, like a
/// Trick drop flies into its slot, and the number counts up once they arrive. Presentation only.
/// </summary>
public sealed class CreditsHudUI : MonoBehaviour
{
    public static CreditsHudUI Instance { get; private set; }
    const float Height = 46f, Width = 210f, Gap = 10f;

    RectTransform _coin;
    float _holdUntil;

    /// <summary>
    /// True while Credits shown on the HUD should wait: an attack or defeat is still playing out, or
    /// Credits are still flying to the coin (from a defeat, or from a Mod or Gear trigger).
    /// </summary>
    public static bool HoldsCredits =>
        AttackCardPresentationUI.Instance && AttackCardPresentationUI.Instance.IsBoardBusy ||
        EffectTriggerPresenterUI.HoldsCredits || Instance && Time.unscaledTime < Instance._holdUntil;

    /// <summary>Builds the chip above <paramref name="healthPanel"/> and moves the Credits label into it.</summary>
    public static CreditsHudUI Install(RectTransform healthPanel, TMP_Text creditsLabel)
    {
        if (!healthPanel || !creditsLabel) return null;
        var existing = healthPanel.GetComponentInChildren<CreditsHudUI>(true);
        if (existing) return existing;

        var chip = new GameObject("Credits Chip", typeof(RectTransform), typeof(CreditsHudUI));
        var rect = (RectTransform)chip.transform;
        rect.SetParent(healthPanel, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(0f, Gap);
        rect.sizeDelta = new Vector2(Width, Height);

        var coin = new GameObject("Credits Coin", typeof(RectTransform));
        var coinRect = (RectTransform)coin.transform;
        coinRect.SetParent(rect, false);
        coinRect.anchorMin = new Vector2(0f, 0f);
        coinRect.anchorMax = new Vector2(0f, 1f);
        coinRect.pivot = new Vector2(0f, .5f);
        coinRect.sizeDelta = new Vector2(Height, 0f);
        coinRect.anchoredPosition = Vector2.zero;
        var theme = DealbreakUIStyle.Theme;
        ItemIconUI.Show(coinRect, theme ? theme.creditsIcon : null, Vector2.zero, Vector2.one);

        var label = creditsLabel.rectTransform;
        label.SetParent(rect, false);
        label.anchorMin = Vector2.zero;
        label.anchorMax = Vector2.one;
        label.pivot = new Vector2(0f, .5f);
        label.offsetMin = new Vector2(Height + 8f, 0f);
        label.offsetMax = Vector2.zero;
        creditsLabel.alignment = TextAlignmentOptions.MidlineLeft;
        creditsLabel.textWrappingMode = TextWrappingModes.NoWrap;
        creditsLabel.overflowMode = TextOverflowModes.Overflow;
        creditsLabel.fontSize = 30f;
        creditsLabel.raycastTarget = true;

        var hud = chip.GetComponent<CreditsHudUI>();
        hud._coin = coinRect;
        return hud;
    }

    public RectTransform Coin => _coin;

    void OnEnable()
    {
        Instance = this;
        EnemyDisplayUI.DefeatHitLanded += HandleDefeatHitLanded;
    }

    void OnDisable()
    {
        EnemyDisplayUI.DefeatHitLanded -= HandleDefeatHitLanded;
        if (Instance == this) Instance = null;
    }

    // Fleeing enemies play no defeat, so they send no Credits.
    void HandleDefeatHitLanded(EnemyDisplayUI view)
    {
        var enemy = view ? view.DisplayedEnemy : null;
        if (enemy == null || enemy.CreditsReward <= 0 || !_coin) return;
        var flight = ItemDragUI.Get();
        if (!flight) return;
        var theme = DealbreakUIStyle.Theme;
        Transform from = view.portrait ? view.portrait.transform : view.transform;
        flight.FlyIntoSlot(from.position, _coin, $"+{enemy.CreditsReward}", theme ? theme.creditsIcon : null);
        _holdUntil = Mathf.Max(_holdUntil, Time.unscaledTime + ItemDragUI.FlightSeconds);
    }
}
