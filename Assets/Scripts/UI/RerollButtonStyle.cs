using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Black Market's reroll: a solid acid-lime button with ink type, so it never reads as an
/// offer (owner, 2026-10-23); grey while it cannot be afforded. Applied every frame because the
/// shared style pass restyles buttons.
/// </summary>
public sealed class RerollButtonStyle : MonoBehaviour
{
    Button _button;
    TMP_Text _label;
    Image _image;

    public void Bind(Button button, TMP_Text label)
    {
        _button = button;
        _label = label;
        _image = button ? button.targetGraphic as Image : null;
        foreach (var style in GetComponents<EditorialButton>()) style.enabled = false;
        foreach (var frame in GetComponentsInChildren<EditorialFrame>(true)) frame.gameObject.SetActive(false);
        if (_button) _button.transition = Selectable.Transition.None;
        LateUpdate();
    }

    void LateUpdate()
    {
        var theme = DealbreakUIStyle.Theme;
        if (!theme || !_button) return;
        bool on = _button.interactable;
        if (_image)
        {
            _image.sprite = null;
            _image.color = on ? theme.ochre : theme.slate;
        }
        if (_label)
        {
            _label.color = on ? theme.ink : new Color(theme.paper.r, theme.paper.g, theme.paper.b, .5f);
            _label.alignment = TextAlignmentOptions.Center;
            if (theme.headingFont) _label.font = theme.headingFont;
            _label.fontSize = 22;
        }
    }
}
