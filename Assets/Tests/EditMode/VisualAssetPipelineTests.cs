using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class VisualAssetPipelineTests
{
    readonly List<Object> _created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var value in _created)
            if (value) Object.DestroyImmediate(value);
        _created.Clear();
    }

    [Test]
    public void GearSlot_UsesAuthoredSprite_AndFallsBackToGlyphWhenMissing()
    {
        var texture = new Texture2D(8, 8);
        _created.Add(texture);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(.5f, .5f));
        _created.Add(sprite);

        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.icon = "♣";
        gear.iconSprite = sprite;
        _created.Add(gear);
        var root = new GameObject("Icon slot", typeof(RectTransform), typeof(Image), typeof(GearIconSlotUI));
        _created.Add(root);
        var labelObject = new GameObject("Glyph", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform, false);
        var label = labelObject.GetComponent<TMP_Text>();
        root.GetComponent<GearIconSlotUI>().Bind(gear, null, null, null, 0);

        var iconImage = root.GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.gameObject != root);
        Assert.IsNotNull(iconImage);
        Assert.AreSame(sprite, iconImage.sprite);
        Assert.IsFalse(label.gameObject.activeSelf);

        gear.iconSprite = null;
        var secondRoot = new GameObject("Fallback slot", typeof(RectTransform), typeof(Image), typeof(GearIconSlotUI));
        _created.Add(secondRoot);
        var fallback = new GameObject("Glyph", typeof(RectTransform), typeof(TextMeshProUGUI));
        fallback.transform.SetParent(secondRoot.transform, false);
        var fallbackLabel = fallback.GetComponent<TMP_Text>();
        secondRoot.GetComponent<GearIconSlotUI>().Bind(gear, null, null, null, 0);
        Assert.AreEqual("♣", fallbackLabel.text);
        Assert.IsTrue(fallbackLabel.gameObject.activeSelf);
    }

    [Test]
    public void EnemyDisplay_CancelDefeatCue_RestoresPresentationState()
    {
        var root = new GameObject("Enemy display", typeof(RectTransform), typeof(Image), typeof(EnemyDisplayUI));
        _created.Add(root);
        var display = root.GetComponent<EnemyDisplayUI>();
        var portrait = root.GetComponent<Image>();
        display.portrait = portrait;
        var originalColor = portrait.color;
        var originalScale = root.transform.localScale;

        display.PlayDefeatCue();
        Assert.AreEqual(new Color(1f, 0.35f, 0.2f, 1f), portrait.color);
        display.CancelDefeatCue();

        Assert.AreEqual(originalColor, portrait.color);
        Assert.AreEqual(originalScale, root.transform.localScale);
        var group = root.GetComponent<CanvasGroup>();
        Assert.IsTrue(group == null || Mathf.Approximately(group.alpha, 1f));
    }

    [Test]
    public void EnemyDisplay_UsesAuthoredPortrait_AndRestoresPlaceholderWithoutOne()
    {
        var texture = new Texture2D(8, 8);
        _created.Add(texture);
        var authored = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(.5f, .5f));
        _created.Add(authored);
        var placeholder = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(.5f, .5f));
        _created.Add(placeholder);

        var type = ScriptableObject.CreateInstance<EnemyTypeData>();
        type.portraitSprite = authored;
        _created.Add(type);
        var enemy = new EnemyRuntime(type);
        var root = new GameObject("Enemy display");
        _created.Add(root);
        var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        portraitObject.transform.SetParent(root.transform, false);
        var portrait = portraitObject.GetComponent<Image>();
        portrait.sprite = placeholder;
        var glyphObject = new GameObject("Glyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        glyphObject.transform.SetParent(portraitObject.transform, false);
        var display = root.AddComponent<EnemyDisplayUI>();
        display.portrait = portrait;
        typeof(EnemyDisplayUI).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(display, null);
        typeof(EnemyDisplayUI).GetMethod("RefreshPortrait", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(display, new object[] { enemy });
        Assert.AreSame(authored, portrait.sprite);
        Assert.IsFalse(glyphObject.activeSelf);

        type.portraitSprite = null;
        typeof(EnemyDisplayUI).GetMethod("RefreshPortrait", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(display, new object[] { enemy });
        Assert.AreSame(placeholder, portrait.sprite);
        Assert.IsTrue(glyphObject.activeSelf);
    }
}
