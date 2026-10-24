using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Scene-local modal; neither its selection nor callbacks belong in a save.
public sealed class SavePromptUI : MonoBehaviour
{
    static SavePromptUI _active;
    Action _confirm;
    bool _closed;
    GameObject _previousSelection;
    public static bool IsOpen => _active != null && !_active._closed;
    public static void AskReplacement(Action confirm) => Show("Replace saved run?",
        "Starting a new run will permanently replace the current saved run. Continue the saved run instead to keep its progress.", confirm);
    public static void ShowError(string message) => Show("Run save", message, null);
    static void Show(string title, string message, Action confirm)
    {
        if (IsOpen) return;
        var root = new GameObject("SaveConfirmation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SavePromptUI));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        var ui = root.GetComponent<SavePromptUI>(); _active = ui; ui._confirm = confirm;
        ui._previousSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
        GameplayInputGate.SetOwned(ui, GameplayInputBlockReason.FrontendMenu, true);
        var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(root.transform, false);
        var br = (RectTransform)backdrop.transform; br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.sizeDelta = Vector2.zero;
        backdrop.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.06f, 0.94f);
        var card=DealbreakUIStyle.Child(backdrop.transform,"Save Modal Card");card.anchorMin=card.anchorMax=new Vector2(.5f,.5f);card.sizeDelta=new Vector2(860,480);card.anchoredPosition=Vector2.zero;
        DealbreakUIStyle.Frame(card);
        Label(card, title, new Vector2(0, 140), new Vector2(760, 60), 36);
        Label(card, message, Vector2.zero, new Vector2(760, 190), 26);
        if (confirm != null) Button(card, "Replace & Start", new Vector2(-180, -160), ui.Confirm);
        var cancel = Button(card, confirm != null ? "Cancel" : "OK", new Vector2(confirm != null ? 180 : 0, -160), ui.Cancel);
        DealbreakUIStyle.ApplyHierarchy(root.transform);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cancel.gameObject);
    }
    public void Confirm()
    {
        if (_closed) return;
        var action = _confirm; Close(); action?.Invoke();
    }
    public void Cancel() { if (!_closed) Close(); }
    void Update(){if(IsOpen&&UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame==true)Cancel();}
    void Close()
    {
        _closed = true; _confirm = null;
        GameplayInputGate.SetOwned(this, GameplayInputBlockReason.FrontendMenu, false);
        if (_active == this) _active = null;
        if(EventSystem.current&&_previousSelection&&_previousSelection.activeInHierarchy)EventSystem.current.SetSelectedGameObject(_previousSelection);
        gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }
    void OnDestroy()
    {
        if (_active == this)
        {
            _active = null;
            GameplayInputGate.SetOwned(this, GameplayInputBlockReason.FrontendMenu, false);
        }
    }
    static TMP_Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
        var label = go.GetComponent<TMP_Text>(); label.text = value; label.fontSize = fontSize; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        go.name=fontSize>=36?"Save Title":"Save Message";
        return label;
    }
    static Button Button(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = new Vector2(320, 64);
        go.GetComponent<Image>().color = new Color(.15f, .26f, .32f);
        Label(go.transform, text, Vector2.zero, rect.sizeDelta, 26);
        var button = go.GetComponent<Button>(); button.onClick.AddListener(action); return button;
    }
}
