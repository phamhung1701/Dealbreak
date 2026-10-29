using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The run's character as a Cut Sheet sticker above the Credits chip, so it shows wherever the HP
/// panel does (HUD slice S10a): the portrait art with its own cut line (no frame, no fill behind
/// it), a 3px ink shadow from its own alpha, a 2px lift on hover and a paper ring on focus. A click
/// opens its name and perk beside it. A run without a character (the tutorial, older saves) shows
/// the first character's look and opens nothing. Presentation only.
/// </summary>
public sealed class PlayerAvatarUI : MonoBehaviour
{
    public const float Width = PlayerCornerLayout.PortraitWidth, Height = PlayerCornerLayout.PortraitHeight,
        Bottom = PlayerCornerLayout.PortraitBottom;
    public const string StickerName = "Sticker", PortraitName = "Portrait", RingName = "Avatar Ring";

    Image _portrait;
    RectTransform _sticker;
    CutSheetShadow _shadow;
    CutSheetHoverRing _hover;
    CharacterData _shown;
    Sprite _shownSprite;
    bool _bound, _applied;

    public Image Portrait => _portrait;
    public CutSheetShadow Shadow => _shadow;

    /// <summary>Builds the avatar above <paramref name="healthPanel"/>.</summary>
    public static PlayerAvatarUI Install(RectTransform healthPanel)
    {
        if (!healthPanel) return null;
        var existing = healthPanel.GetComponentInChildren<PlayerAvatarUI>(true);
        if (existing) return existing;

        var root = DealbreakUIStyle.Child(healthPanel, "Player Avatar", typeof(Image), typeof(PlayerAvatarUI),
            typeof(ContextualInfoTrigger));
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 0f);
        root.anchoredPosition = new Vector2(0f, Bottom);
        root.sizeDelta = new Vector2(Width, Height);
        // The root only carries the click detail; the sticker art takes the pointer.
        var rootImage = root.GetComponent<Image>();
        rootImage.sprite = null;
        rootImage.color = Color.clear;
        rootImage.raycastTarget = false;

        var sticker = DealbreakUIStyle.Child(root, StickerName);
        sticker.anchorMin = new Vector2(0f, 0f);
        sticker.anchorMax = new Vector2(0f, 1f);
        sticker.pivot = new Vector2(0f, .5f);
        sticker.sizeDelta = new Vector2(Height, 0f);
        sticker.anchoredPosition = Vector2.zero;
        var shadow = CutSheetShadow.Create(sticker);
        var portrait = DealbreakUIStyle.Child(sticker, PortraitName, typeof(CanvasRenderer), typeof(Image));
        portrait.anchorMin = Vector2.zero;
        portrait.anchorMax = Vector2.one;
        portrait.offsetMin = portrait.offsetMax = Vector2.zero;
        var ring = CutSheetHoverRing.CreateRing(portrait, RingName, 2f);

        var avatar = root.GetComponent<PlayerAvatarUI>();
        avatar._sticker = sticker;
        avatar._shadow = shadow;
        avatar._portrait = portrait.GetComponent<Image>();
        avatar._portrait.preserveAspect = true;
        avatar._portrait.raycastTarget = true;
        avatar._hover = CutSheetHoverRing.Attach(root.gameObject, ring, false, true, portrait);
        var detail = root.GetComponent<ContextualInfoTrigger>();
        detail.pinOnClick = true;
        detail.allowWhileBlocked = true;
        detail.describe = () => Describe(CardManager.Instance ? CardManager.Instance.Character : null);
        avatar.Refresh();
        return avatar;
    }

    /// <summary>The detail a click opens: the character's name and perk; none without a character.</summary>
    public static ContextualInfo? Describe(CharacterData character) =>
        character ? new ContextualInfo("Character", character.displayName, character.description) : null;

    /// <summary>The portrait shown: the run's character, else the first character's look.</summary>
    public static Sprite PortraitFor(CharacterData character)
    {
        if (character && character.portrait) return character.portrait;
        var catalog = SaveContentCatalog.Load();
        if (!catalog) return null;
        foreach (var entry in catalog.characters)
            if (entry && entry.portrait) return entry.portrait;
        return null;
    }

    void LateUpdate()
    {
        var current = CardManager.Instance ? CardManager.Instance.Character : null;
        if (!_bound || current != _shown) Refresh();
    }

    void Refresh()
    {
        _shown = CardManager.Instance ? CardManager.Instance.Character : null;
        _bound = true;
        if (!_portrait) return;
        var sprite = PortraitFor(_shown);
        if (_applied && sprite == _shownSprite && _portrait.sprite == sprite) return;
        _applied = true;
        _shownSprite = sprite;
        _portrait.sprite = sprite;
        _portrait.enabled = sprite;
        // The sticker keeps the art's aspect, left-aligned in the portrait rect, full height.
        float aspect = sprite && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
        float width = Mathf.Min(Width, Height * aspect);
        if (_sticker && !Mathf.Approximately(_sticker.sizeDelta.x, width)) _sticker.sizeDelta = new Vector2(width, 0f);
        if (_shadow)
        {
            _shadow.Match(_portrait, CutSheetShadow.Tint, sprite ? CutSheetMetrics.ShadowSlot : 0f);
            if (!_shadow.Image.preserveAspect) _shadow.Image.preserveAspect = true;
        }
        if (_hover) _hover.RecaptureRest();
    }
}
