using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A cyberpunk "glitch" on a hand card whose suit a living enemy resists (a Headliner's own suit,
/// or a Suit Call's called suit): cyan and magenta copies of the face jitter in short bursts.
/// Presentation only; it reads combat state and uses its own random source.
/// </summary>
[RequireComponent(typeof(CardView))]
public sealed class CardGlitchUI : MonoBehaviour
{
    // Drawn over the face at low alpha, so the split reads as a colour fringe across the art.
    static readonly Color Cyan = new(0f, .95f, 1f, .32f);
    static readonly Color Magenta = new(1f, 0f, .8f, .32f);

    CardView _view;
    Image _cyan, _magenta;
    RectTransform _faceRect;
    Vector2 _faceRest;
    bool _faceShifted;
    float _nextBurst, _burstUntil;
    readonly System.Random _random = new();

    public static void Attach(CardView view)
    {
        if (view && view.face && !view.TryGetComponent(out CardGlitchUI _))
            view.gameObject.AddComponent<CardGlitchUI>();
    }

    void Awake() => _view = GetComponent<CardView>();

    void OnDisable() => SetGlitching(false);

    void LateUpdate()
    {
        bool glitching = IsResisted();
        SetGlitching(glitching);
        if (!glitching) return;
        float now = Time.unscaledTime;
        if (now >= _nextBurst)
        {
            _burstUntil = now + Range(.05f, .14f);
            _nextBurst = now + Range(.35f, 1.1f);
        }
        bool burst = now < _burstUntil;
        float spread = burst ? Range(7f, 13f) : 3.5f + Mathf.Sin(now * 9f) * 1f;
        Place(_cyan, new Vector2(-spread, burst ? Range(-2f, 2f) : 0f));
        Place(_magenta, new Vector2(spread, burst ? Range(-2f, 2f) : 0f));
        _faceRect.anchoredPosition = _faceRest + (burst ? new Vector2(Range(-3f, 3f), 0f) : Vector2.zero);
        _faceShifted = burst;
    }

    bool IsResisted()
    {
        var card = _view ? _view.data : null;
        var combat = CombatManager.Instance;
        if (card == null || _view.homeField == null || !DealbreakUIRuntime.CombatActive || combat == null) return false;
        foreach (var enemy in combat.Enemies)
            if (enemy != null && enemy.ResistsSuit(card.Suit)) return true;
        return false;
    }

    void SetGlitching(bool on)
    {
        if (on) Ensure();
        if (_cyan) _cyan.enabled = on;
        if (_magenta) _magenta.enabled = on;
        if (!on && _faceShifted && _faceRect)
        {
            _faceRect.anchoredPosition = _faceRest;
            _faceShifted = false;
        }
    }

    void Ensure()
    {
        if (_cyan) { SyncSprite(_cyan); SyncSprite(_magenta); return; }
        _faceRect = _view.face.rectTransform;
        _faceRest = _faceRect.anchoredPosition;
        _cyan = Ghost("Glitch Cyan", Cyan);
        _magenta = Ghost("Glitch Magenta", Magenta);
    }

    Image Ghost(string name, Color color)
    {
        var ghost = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)ghost.transform;
        rect.SetParent(_faceRect.parent, false);
        rect.SetSiblingIndex(_faceRect.GetSiblingIndex() + 1);
        var image = ghost.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = color;
        SyncSprite(image);
        Place(image, Vector2.zero);
        return image;
    }

    void SyncSprite(Image ghost)
    {
        if (ghost.sprite == _view.face.sprite) return;
        ghost.sprite = _view.face.sprite;
        ghost.preserveAspect = _view.face.preserveAspect;
    }

    void Place(Image ghost, Vector2 offset)
    {
        var rect = ghost.rectTransform;
        rect.anchorMin = _faceRect.anchorMin;
        rect.anchorMax = _faceRect.anchorMax;
        rect.pivot = _faceRect.pivot;
        rect.sizeDelta = _faceRect.sizeDelta;
        rect.localRotation = _faceRect.localRotation;
        rect.localScale = _faceRect.localScale;
        rect.anchoredPosition = _faceRest + offset;
    }

    float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
}
