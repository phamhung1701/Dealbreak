using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

// (owner, 2026-10-24) UI fills print with a halftone dot screen like the item and character art;
// borders and rings stay solid.
public sealed class HalftonePrintTests
{
    readonly List<Object> _objects = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var item in _objects) if (item) Object.DestroyImmediate(item);
        _objects.Clear();
    }

    [Test]
    public void TheThemeCarriesTheHalftoneMaterial()
    {
        var material = DealbreakUIStyle.Halftone;
        Assert.That(material, Is.Not.Null);
        Assert.That(material.shader.name, Is.EqualTo("UI/Dealbreak Halftone"));
    }

    [Test]
    public void EditorialFrame_PrintsItsFill_AndKeepsItsBorderSolid()
    {
        var go = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorialFrame));
        _objects.Add(go);
        ((RectTransform)go.transform).sizeDelta = new Vector2(200, 60);
        var frame = go.GetComponent<EditorialFrame>();
        frame.border = 2;
        Assert.That(frame.material, Is.SameAs(DealbreakUIStyle.Halftone));

        var mesh = new VertexHelper();
        typeof(EditorialFrame).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(VertexHelper) }, null).Invoke(frame, new object[] { mesh });
        var vertices = new List<UIVertex>();
        mesh.GetUIVertexStream(vertices);
        var fill = vertices.Where(v => v.uv0.x >= 0).ToList();
        var border = vertices.Where(v => v.uv0.x < 0).ToList();
        Assert.That(fill, Is.Not.Empty);
        Assert.That(border, Is.Not.Empty, "The border prints solid.");
        Assert.That(fill.All(v => v.uv0.x <= 1 && v.uv0.y >= 0 && v.uv0.y <= 1), Is.True, "Fill UVs are the place in the shape.");
        Assert.That(fill.Select(v => v.uv0.y).Distinct().Count(), Is.GreaterThan(1), "Top and foot differ, so the dots shade.");
    }

    [Test]
    public void PrintFill_PrintsOnlyPlainFills()
    {
        var plain = new GameObject("Plain", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var art = new GameObject("Art", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        _objects.Add(plain.gameObject); _objects.Add(art.gameObject);
        var texture = new Texture2D(4, 4); _objects.Add(texture);
        art.sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f); _objects.Add(art.sprite);

        DealbreakUIStyle.PrintFill(plain);
        DealbreakUIStyle.PrintFill(art);

        Assert.That(plain.material, Is.SameAs(DealbreakUIStyle.Halftone));
        Assert.That(art.material, Is.Not.SameAs(DealbreakUIStyle.Halftone), "Art keeps its own look.");
    }
}
