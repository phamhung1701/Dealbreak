using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

// A missing EditorialSurface is Unity's fake null in the Editor; reading its treatment through ?.
// returned Open (enum value 0). These pin the build behaviour: no surface means not Open.
public sealed class EditorialButtonSurfaceTests
{
    GameObject _buttonObject;

    [TearDown]
    public void TearDown()
    {
        if (_buttonObject) Object.DestroyImmediate(_buttonObject);
    }

    EditorialButton StyledSelectedButton(bool openSurface)
    {
        Assume.That(DealbreakUIStyle.Theme, Is.Not.Null);
        _buttonObject = new GameObject("Surface Test Button", typeof(RectTransform), typeof(Image), typeof(Button));
        if (openSurface) _buttonObject.AddComponent<EditorialSurface>().treatment = EditorialSurface.Treatment.Open;
        var button = _buttonObject.GetComponent<Button>();
        DealbreakUIStyle.Button(button, EditorialButton.Role.Secondary);
        Assert.That(_buttonObject.TryGetComponent<EditorialButton>(out var style), Is.True);
        style.selected = true;
        Assert.That(style.Refresh(), Is.True);
        return style;
    }

    [Test]
    public void ButtonWithoutSurface_IsNotTreatedAsOpen()
    {
        var style = StyledSelectedButton(openSurface: false);
        Assert.That(_buttonObject.GetComponent<EditorialSurface>() == null, Is.True);
        Assert.That(style.frame.rim, Is.EqualTo(DealbreakUIStyle.Theme.ochre), "A framed button takes the lime selection rule.");
        Assert.That(style.frame.border, Is.EqualTo(2f));
    }

    [Test]
    public void ButtonWithOpenSurface_KeepsFramelessSelection()
    {
        var style = StyledSelectedButton(openSurface: true);
        Assert.That(style.frame.rim, Is.Not.EqualTo(DealbreakUIStyle.Theme.ochre), "Open buttons show selection in the label, not the rule.");
        Assert.That(style.frame.border, Is.EqualTo(1f));
    }
}
