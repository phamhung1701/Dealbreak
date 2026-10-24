using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The run's character as a square portrait just above the Credits chip, so it shows wherever the
/// HP panel does. A click opens its name and perk beside it. A run without a character (the tutorial,
/// older saves) shows the first character's look and opens nothing. Presentation only.
/// </summary>
public sealed class PlayerAvatarUI : MonoBehaviour
{
    // Above the Credits chip: its gap, its height, then this gap.
    const float Size = 130f, Bottom = 10f + 46f + 14f, Inset = 6f;

    Image _portrait;
    CharacterData _shown;
    bool _bound;

    /// <summary>Builds the avatar above <paramref name="healthPanel"/>.</summary>
    public static PlayerAvatarUI Install(RectTransform healthPanel)
    {
        if (!healthPanel) return null;
        var existing = healthPanel.GetComponentInChildren<PlayerAvatarUI>(true);
        if (existing) return existing;

        var theme = DealbreakUIStyle.Theme;
        var root = DealbreakUIStyle.Child(healthPanel, "Player Avatar", typeof(Image), typeof(PlayerAvatarUI),
            typeof(ContextualInfoTrigger));
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 0f);
        root.anchoredPosition = new Vector2(0f, Bottom);
        root.sizeDelta = new Vector2(Size, Size);
        // The 1px hairline: the root draws slate, the fill inside it charcoal.
        var rim = root.GetComponent<Image>();
        rim.color = theme ? theme.slate : Color.grey;
        var fill = DealbreakUIStyle.Child(root, "Fill", typeof(Image));
        DealbreakUIStyle.Place(fill, 0, 0, 1, 1);
        fill.offsetMin = Vector2.one;
        fill.offsetMax = -Vector2.one;
        var fillImage = fill.GetComponent<Image>();
        fillImage.color = theme ? theme.charcoal : Color.black;
        fillImage.raycastTarget = false;
        DealbreakUIStyle.PrintFill(fillImage);
        var portrait = DealbreakUIStyle.Child(root, "Portrait", typeof(Image));
        DealbreakUIStyle.Place(portrait, 0, 0, 1, 1);
        portrait.offsetMin = new Vector2(Inset, Inset);
        portrait.offsetMax = new Vector2(-Inset, -Inset);

        var avatar = root.GetComponent<PlayerAvatarUI>();
        avatar._portrait = portrait.GetComponent<Image>();
        avatar._portrait.preserveAspect = true;
        avatar._portrait.raycastTarget = false;
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
        _portrait.sprite = PortraitFor(_shown);
        _portrait.enabled = _portrait.sprite;
    }
}
