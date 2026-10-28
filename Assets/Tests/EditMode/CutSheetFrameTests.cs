using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

// Cut Sheet plumbing (HUD slice S4): EditorialFrame sprite mode, the ink shadow and screen-pixel
// overlays. Nothing in a scene uses them yet; flat frames stay as they were.
public sealed class CutSheetFrameTests
{
    readonly List<Object> _objects = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var item in _objects) if (item) Object.DestroyImmediate(item);
        _objects.Clear();
    }

    Canvas MakeCanvas(float scale = 1f)
    {
        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)); _objects.Add(canvasObject);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.scaleFactor = scale;
        return canvas;
    }

    Sprite MakeSprite(int size)
    {
        var texture = new Texture2D(size, size) { wrapMode = TextureWrapMode.Repeat }; _objects.Add(texture);
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect); _objects.Add(sprite);
        return sprite;
    }

    EditorialFrame MakeFrame(Canvas canvas)
    {
        var frame = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorialFrame)).GetComponent<EditorialFrame>();
        frame.transform.SetParent(canvas.transform, false);
        frame.rectTransform.sizeDelta = new Vector2(200, 60);
        return frame;
    }

    EditorialFrame MakeSpriteFrame()
    {
        var frame = MakeFrame(MakeCanvas());
        frame.sprite = MakeSprite(64);
        frame.shadowDepth = CutSheetMetrics.ShadowButton;
        frame.SetSpriteMode(true);
        return frame;
    }

    static int ShadowCount(EditorialFrame frame) => frame.GetComponentsInChildren<CutSheetShadow>(true).Length;

    [Test]
    public void SpriteModeIsOffByDefaultAndBuildsNothing()
    {
        var frame = MakeFrame(MakeCanvas());
        frame.Refresh();
        Assert.That(frame.spriteMode, Is.False);
        Assert.That(frame.transform.childCount, Is.Zero, "A flat frame keeps drawing its own mesh.");
    }

    [Test]
    public void SpriteModeBuildsAFaceAndOneShadowBeforeIt()
    {
        var frame = MakeSpriteFrame();
        Assert.That(frame.SpriteFace, Is.Not.Null);
        Assert.That(frame.SpriteShadow, Is.Not.Null);
        Assert.That(ShadowCount(frame), Is.EqualTo(1));
        Assert.That(frame.SpriteShadow.transform.GetSiblingIndex(), Is.LessThan(frame.SpriteFace.transform.GetSiblingIndex()));
        var shadow = frame.SpriteShadow.Image;
        Assert.That(shadow.sprite, Is.SameAs(frame.SpriteFace.sprite));
        Assert.That(shadow.type, Is.EqualTo(frame.SpriteFace.type));
        Assert.That(shadow.color, Is.EqualTo(CutSheetShadow.Tint));
        Assert.That(((RectTransform)shadow.transform).offsetMin, Is.EqualTo(new Vector2(4, -4)));
    }

    [Test]
    public void ShadowFaceAndOverlaysAreNotRaycastTargets()
    {
        var frame = MakeSpriteFrame();
        Assert.That(frame.SpriteShadow.Image.raycastTarget, Is.False);
        Assert.That(frame.SpriteFace.raycastTarget, Is.False);

        var overlay = new GameObject("Grain", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        overlay.transform.SetParent(frame.transform, false);
        overlay.AddComponent<ScreenPixelTile>();
        Assert.That(overlay.GetComponent<RawImage>().raycastTarget, Is.False);
    }

    [Test]
    public void PressingMovesTheFaceNotTheShadow()
    {
        var frame = MakeSpriteFrame();
        var shadow = (RectTransform)frame.SpriteShadow.transform;
        var shadowBefore = shadow.anchoredPosition;
        frame.SetFaceOffset(CutSheetMetrics.PressedFaceOffset);
        Assert.That(frame.SpriteFace.rectTransform.anchoredPosition, Is.EqualTo(CutSheetMetrics.PressedFaceOffset));
        Assert.That(shadow.anchoredPosition, Is.EqualTo(shadowBefore));
        Assert.That(shadow.anchoredPosition - frame.SpriteFace.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero), "Pressed reads shadow 0.");
    }

    [Test]
    public void ZeroDepthDisablesTheShadowInsteadOfFading()
    {
        var frame = MakeSpriteFrame();
        frame.shadowDepth = CutSheetMetrics.ShadowMeter;
        frame.Refresh();
        Assert.That(frame.SpriteShadow.Image.enabled, Is.False);
    }

    [Test]
    public void TogglingSpriteModeTwiceLeavesOneShadow()
    {
        var frame = MakeSpriteFrame();
        frame.SetSpriteMode(false);
        Assert.That(frame.SpriteShadow.gameObject.activeSelf, Is.False);
        Assert.That(frame.SpriteFace.gameObject.activeSelf, Is.False);
        frame.SetSpriteMode(true);
        frame.SetSpriteMode(false);
        frame.SetSpriteMode(true);
        Assert.That(ShadowCount(frame), Is.EqualTo(1));
        Assert.That(frame.transform.childCount, Is.EqualTo(2));
        Assert.That(frame.SpriteShadow.gameObject.activeSelf, Is.True);
    }

    [TestCase(1f)]
    [TestCase(1.333f)]
    [TestCase(2f)]
    public void ImageOverlayTilesOneTexelPerScreenPixel(float scale)
    {
        var canvas = MakeCanvas(scale);
        var item = new GameObject("Hatch", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        item.transform.SetParent(canvas.transform, false);
        var image = item.GetComponent<Image>();
        image.sprite = MakeSprite(12);
        image.type = Image.Type.Tiled;
        item.AddComponent<ScreenPixelTile>().Refresh();
        // Tiling divides by pixelsPerUnit × pixelsPerUnitMultiplier (Image.multipliedPixelsPerUnit).
        float tileScreenPixels = image.sprite.rect.width / (image.pixelsPerUnit * image.pixelsPerUnitMultiplier) * scale;
        Assert.That(tileScreenPixels, Is.EqualTo(image.sprite.rect.width).Within(.001f));
    }

    [TestCase(1f)]
    [TestCase(1.333f)]
    [TestCase(2f)]
    public void RawImageOverlayMapsOneTexelPerScreenPixel(float scale)
    {
        var canvas = MakeCanvas(scale);
        var item = new GameObject("Halftone", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        item.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)item.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(300, 120);
        var raw = item.GetComponent<RawImage>();
        var texture = new Texture2D(16, 16) { wrapMode = TextureWrapMode.Repeat }; _objects.Add(texture);
        raw.texture = texture;
        item.AddComponent<ScreenPixelTile>().Refresh();
        Assert.That(raw.uvRect.width * texture.width / (rect.rect.width * scale), Is.EqualTo(1f).Within(.0001f));
        Assert.That(raw.uvRect.height * texture.height / (rect.rect.height * scale), Is.EqualTo(1f).Within(.0001f));
    }

    [Test]
    public void SlotTiltsStayWithinOneDegreeAndWrap()
    {
        for (int i = 0; i < CutSheetMetrics.SlotTiltCount; i++)
            Assert.That(Mathf.Abs(CutSheetMetrics.SlotTilt(i)), Is.LessThanOrEqualTo(CutSheetMetrics.SlotTiltMax));
        Assert.That(CutSheetMetrics.SlotTilt(CutSheetMetrics.SlotTiltCount), Is.EqualTo(CutSheetMetrics.SlotTilt(0)));
        Assert.That(CutSheetMotion.Seconds(CutSheetMetrics.PressSeconds), Is.EqualTo(CutSheetMetrics.PressSeconds), "Reduce Motion defaults off.");
    }
}
