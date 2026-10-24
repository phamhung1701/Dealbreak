using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The character row on the run setup screen: one tile per <see cref="SaveContentCatalog.characters"/>
/// entry with its portrait, name and perk. A click picks it (lime ring); the pick is remembered for
/// the next visit. Presentation only: the run starts with <see cref="SelectedId"/>.
/// </summary>
public sealed class CharacterPickerUI : MonoBehaviour
{
    public const string LastPickKey = "Dealbreak.Character";
    const float TileWidth = 200f, PortraitSize = 200f, Gap = 24f, NameHeight = 44f, PerkHeight = 64f;

    sealed class Tile
    {
        public CharacterData Character;
        public Image Ring;
        public TMP_Text Name;
    }

    readonly List<Tile> _tiles = new();
    CharacterData _selected;

    public CharacterData Selected => _selected;
    public string SelectedId => _selected ? _selected.id : null;

    void OnEnable() => Refresh();

    /// <summary>Builds the tiles once and selects the remembered (or first) character.</summary>
    public void Refresh()
    {
        var catalog = SaveContentCatalog.Load();
        var characters = catalog ? catalog.characters : System.Array.Empty<CharacterData>();
        if (_tiles.Count == 0) Build(characters);
        var remembered = catalog ? catalog.Character(PlayerPrefs.GetString(LastPickKey, string.Empty)) : null;
        Select(remembered ? remembered : _selected ? _selected : _tiles.Count > 0 ? _tiles[0].Character : null, false);
    }

    public void Select(CharacterData character, bool remember = true)
    {
        _selected = character;
        if (remember && character) { PlayerPrefs.SetString(LastPickKey, character.id); PlayerPrefs.Save(); }
        var theme = DealbreakUIStyle.Theme;
        if (!theme) return;
        foreach (var tile in _tiles)
        {
            bool on = tile.Character == _selected;
            tile.Ring.color = on ? theme.ochre : theme.slate;
            // A 3px lime ring marks the pick; the others keep a 1px hairline.
            float ring = on ? 3f : 1f;
            tile.Ring.rectTransform.offsetMin = new Vector2(-ring, -ring);
            tile.Ring.rectTransform.offsetMax = new Vector2(ring, ring);
            tile.Name.color = on ? theme.ochre : theme.paper;
        }
    }

    void Build(IReadOnlyList<CharacterData> characters)
    {
        var theme = DealbreakUIStyle.Theme;
        int count = 0;
        foreach (var character in characters) if (character) count++;
        float rowWidth = count * TileWidth + Mathf.Max(0, count - 1) * Gap;
        int index = 0;
        foreach (var character in characters)
        {
            if (!character) continue;
            float left = -rowWidth / 2f + index++ * (TileWidth + Gap);
            _tiles.Add(BuildTile(character, left, theme));
        }
    }

    Tile BuildTile(CharacterData character, float left, DealbreakUITheme theme)
    {
        var tile = DealbreakUIStyle.Child(transform, $"Character {character.id}", typeof(Image), typeof(Button));
        tile.anchorMin = tile.anchorMax = new Vector2(.5f, 1f);
        tile.pivot = new Vector2(0f, 1f);
        tile.anchoredPosition = new Vector2(left, 0f);
        tile.sizeDelta = new Vector2(TileWidth, PortraitSize + NameHeight + PerkHeight);
        var hit = tile.GetComponent<Image>();
        hit.color = Color.clear;
        var button = tile.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Select(character));

        var box = DealbreakUIStyle.Child(tile, "Portrait Box", typeof(Image));
        box.anchorMin = box.anchorMax = new Vector2(.5f, 1f);
        box.pivot = new Vector2(.5f, 1f);
        box.anchoredPosition = Vector2.zero;
        box.sizeDelta = new Vector2(PortraitSize, PortraitSize);
        var ring = DealbreakUIStyle.Child(box, "Ring", typeof(Image));
        DealbreakUIStyle.Place(ring, 0, 0, 1, 1);
        ring.SetAsFirstSibling();
        var fill = DealbreakUIStyle.Child(box, "Fill", typeof(Image));
        DealbreakUIStyle.Place(fill, 0, 0, 1, 1);
        var portrait = DealbreakUIStyle.Child(box, "Portrait", typeof(Image));
        DealbreakUIStyle.Place(portrait, 0, 0, 1, 1);
        portrait.offsetMin = new Vector2(8f, 8f);
        portrait.offsetMax = new Vector2(-8f, -8f);
        var art = portrait.GetComponent<Image>();
        art.sprite = character.portrait;
        art.preserveAspect = true;
        art.raycastTarget = false;
        art.enabled = character.portrait;
        box.GetComponent<Image>().color = Color.clear;
        box.GetComponent<Image>().raycastTarget = false;
        ring.GetComponent<Image>().raycastTarget = false;
        var fillImage = fill.GetComponent<Image>();
        fillImage.raycastTarget = false;
        if (theme) fillImage.color = theme.charcoal;
        DealbreakUIStyle.PrintFill(fillImage);

        var name = Label(tile, "Name", character.displayName, PortraitSize + 6f, NameHeight,
            theme ? theme.headingFont : null, 32f);
        name.fontStyle = FontStyles.UpperCase;
        var perk = Label(tile, "Perk", character.description, PortraitSize + NameHeight, PerkHeight,
            theme ? theme.bodyFont : null, 19f);
        if (theme) perk.color = theme.paper;
        perk.textWrappingMode = TextWrappingModes.Normal;
        return new Tile { Character = character, Ring = ring.GetComponent<Image>(), Name = name };
    }

    static TMP_Text Label(RectTransform tile, string name, string text, float top, float height, TMP_FontAsset font, float size)
    {
        var rect = DealbreakUIStyle.Child(tile, name, typeof(TextMeshProUGUI));
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(0f, height);
        var label = rect.GetComponent<TextMeshProUGUI>();
        if (font) label.font = font;
        label.fontSize = size;
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.Top;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }
}
