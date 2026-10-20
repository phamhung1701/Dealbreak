using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Full-screen cover shown while GameFlowService swaps scenes. It appears in the same frame as the
/// load request, so it covers the load itself, then holds briefly and fades once the new scene is
/// up. Scene loads stay synchronous; this changes nothing about when gameplay starts.
/// </summary>
public sealed class LoadingScreenUI : MonoBehaviour
{
    // Above every scene canvas, including modal menus (100).
    const int SortingOrder = 1000;
    const float MinimumShowTime = .45f;
    const float FadeTime = .35f;
    static readonly string[] Suits = { "♠", "♥", "♦", "♣" };

    static LoadingScreenUI _instance;
    CanvasGroup _group;
    TMP_Text _suits;
    RectTransform _bar;
    float _shownAt;
    bool _sceneLoaded;
    Coroutine _hide;

    public static void Show()
    {
        if (!Application.isPlaying) return;
        if (!_instance) Create();
        _instance.Begin();
    }

    static void Create()
    {
        var root = new GameObject("[Loading Screen]", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup));
        DontDestroyOnLoad(root);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        _instance = root.AddComponent<LoadingScreenUI>();
        _instance.Build();
        root.SetActive(false);
    }

    void Build()
    {
        var theme = DealbreakUIStyle.Theme;
        _group = GetComponent<CanvasGroup>();
        var backdrop = Child("Backdrop", typeof(Image));
        Stretch(backdrop);
        backdrop.GetComponent<Image>().color = theme ? theme.ink : new Color(.06f, .07f, .08f);

        var title = Label("Title", theme ? theme.headingFont : null, 128f, theme ? theme.paper : Color.white);
        Place(title.rectTransform, new Vector2(.5f, .56f), new Vector2(1200f, 160f));
        title.text = "DEALBREAK";

        _suits = Label("Suits", theme ? theme.numericFont : null, 44f, theme ? theme.ochre : Color.yellow);
        Place(_suits.rectTransform, new Vector2(.5f, .43f), new Vector2(600f, 70f));

        var track = Child("Progress Track", typeof(Image));
        Place(track, new Vector2(.5f, .35f), new Vector2(420f, 8f));
        track.GetComponent<Image>().color = theme ? theme.charcoal : new Color(.2f, .2f, .2f);
        _bar = Child("Progress", typeof(Image));
        _bar.SetParent(track, false);
        _bar.anchorMin = new Vector2(0f, 0f);
        _bar.anchorMax = new Vector2(.2f, 1f);
        _bar.offsetMin = _bar.offsetMax = Vector2.zero;
        _bar.GetComponent<Image>().color = theme ? theme.ochre : Color.yellow;
    }

    void Begin()
    {
        if (_hide != null) StopCoroutine(_hide);
        gameObject.SetActive(true);
        _group.alpha = 1f;
        _group.blocksRaycasts = true;
        _shownAt = Time.unscaledTime;
        _sceneLoaded = false;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        _hide = StartCoroutine(HideWhenReady());
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        _sceneLoaded = true;
    }

    IEnumerator HideWhenReady()
    {
        while (!_sceneLoaded || Time.unscaledTime - _shownAt < MinimumShowTime)
        {
            Animate();
            yield return null;
        }
        // One more frame lets the new scene's Start methods lay out before it is revealed.
        yield return null;
        float elapsed = 0f;
        while (elapsed < FadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            _group.alpha = 1f - Mathf.Clamp01(elapsed / FadeTime);
            Animate();
            yield return null;
        }
        _group.blocksRaycasts = false;
        gameObject.SetActive(false);
        _hide = null;
    }

    void Animate()
    {
        float t = Time.unscaledTime - _shownAt;
        int lit = Mathf.FloorToInt(t * 8f) % Suits.Length;
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < Suits.Length; i++)
            text.Append(i == lit ? $"<alpha=#FF>{Suits[i]}" : $"<alpha=#55>{Suits[i]}").Append(i < Suits.Length - 1 ? "   " : "");
        _suits.text = text.ToString();
        float sweep = Mathf.Repeat(t * 1.4f, 1.2f) - .2f;
        _bar.anchorMin = new Vector2(Mathf.Clamp01(sweep), 0f);
        _bar.anchorMax = new Vector2(Mathf.Clamp01(sweep + .2f), 1f);
    }

    RectTransform Child(string name, params System.Type[] components)
    {
        var types = new System.Type[components.Length + 1];
        types[0] = typeof(RectTransform);
        components.CopyTo(types, 1);
        var rect = (RectTransform)new GameObject(name, types).transform;
        rect.SetParent(transform, false);
        return rect;
    }

    TMP_Text Label(string name, TMP_FontAsset font, float size, Color color)
    {
        var label = Child(name, typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        if (font) label.font = font;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (_instance == this) _instance = null;
    }
}
