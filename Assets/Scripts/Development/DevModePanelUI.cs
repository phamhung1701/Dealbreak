#if UNITY_EDITOR || DEVELOPMENT_BUILD
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Build-gated runtime UI for entering and managing developer runs.</summary>
public sealed class DevModePanelUI : MonoBehaviour
{
    static readonly Color PanelColor = new(0.055f, 0.075f, 0.11f, 0.98f);
    static readonly Color ButtonColor = new(0.18f, 0.27f, 0.38f, 1f);
    Canvas _canvas;
    GameplayMenuCoordinator _coordinator;
    ConfirmationDialogUI _confirmation;
    GameObject _setupPanel, _activePanel, _indicator;
    TMP_InputField _seed;
    Toggle _health, _money, _offers;
    TMP_Text _status;
    Button _openButton;

    public static void InstallMainMenu(MainMenuUI menu)
    {
        if (!DevModeRuntime.Available || menu == null || menu.newRunButton == null || menu.GetComponent<DevModePanelUI>()) return;
        var ui = menu.gameObject.AddComponent<DevModePanelUI>();
        ui.BuildMainMenu(menu);
    }

    public static void InstallGame(PauseMenuUI pause)
    {
        if (!DevModeRuntime.Available || pause == null || pause.GetComponent<DevModePanelUI>()) return;
        var ui = pause.gameObject.AddComponent<DevModePanelUI>();
        ui.BuildGame(pause);
    }

    void BuildMainMenu(MainMenuUI menu)
    {
        var canvas = menu.GetComponentInParent<Canvas>();
        if (!canvas) return;
        _canvas = canvas;
        var parent = menu.newRunButton.transform.parent;
        _openButton = MakeButton(parent, "Dev Mode", new Vector2(0, -290), OpenSetup);
        if(DealbreakUIStyle.Theme){var rect=(RectTransform)_openButton.transform;rect.anchorMin=rect.anchorMax=new Vector2(.46f,.06f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(190,42);}
        _setupPanel = MakePanel("Dev Mode Setup", canvas.transform, new Vector2(620, 450));
        AddText(_setupPanel.transform, "DEV MODE", new Vector2(0, 180), 34, TextAlignmentOptions.Center);
        _seed = MakeInput(_setupPanel.transform, "Optional seed (blank = random)", new Vector2(0, 115));
        _health = MakeToggle(_setupPanel.transform, "Infinite Health", new Vector2(0, 55), true);
        _money = MakeToggle(_setupPanel.transform, "Infinite Credits", new Vector2(0, 5), true);
        _offers = MakeToggle(_setupPanel.transform, "All Eligible Black Market Goods", new Vector2(0, -45), true);
        MakeButton(_setupPanel.transform, "Start Dev Run", new Vector2(0, -120), StartDevRun);
        MakeButton(_setupPanel.transform, "Back", new Vector2(0, -180), CloseSetup);
        _setupPanel.SetActive(false);
    }

    void BuildGame(PauseMenuUI pause)
    {
        _canvas = pause.GetComponentInParent<Canvas>();
        if (!_canvas) return;
        _coordinator = pause.coordinator;
        _confirmation = pause.confirmationDialog;
        _activePanel = MakePanel("Dev Mode Controls", _canvas.transform, new Vector2(650, 400));
        AddText(_activePanel.transform, "DEV MODE", new Vector2(0, 150), 32, TextAlignmentOptions.Center);
        _status = AddText(_activePanel.transform, "", new Vector2(0, 85), 22, TextAlignmentOptions.Center);
        MakeButton(_activePanel.transform, "Disable Cheats", new Vector2(0, 20), DisableCheats);
        MakeButton(_activePanel.transform, "Lose Run Now…", new Vector2(0, -45), ConfirmLose);
        MakeButton(_activePanel.transform, "Close", new Vector2(0, -115), CloseActive);
        _activePanel.SetActive(false);
        _indicator = new GameObject("DEV MODE Indicator", typeof(RectTransform));
        _indicator.transform.SetParent(_canvas.transform, false);
        var rect = (RectTransform)_indicator.transform;
        // Bottom right, just above the DEV button: the header carries the Gear and Trick rows.
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-170, 112); rect.sizeDelta = new Vector2(300, 48);
        AddText(_indicator.transform, "DEV MODE — click to manage", new Vector2(0, 0), 20, TextAlignmentOptions.Center);
        var button = _indicator.AddComponent<Button>();
        button.targetGraphic = _indicator.AddComponent<Image>();
        button.targetGraphic.color = new Color(0.6f, 0.12f, 0.08f, 0.95f);
        button.onClick.AddListener(OpenActive);
        _indicator.SetActive(false);
        var open = MakeButton(_canvas.transform, "DEV", new Vector2(-80, 55), OpenActive, new Vector2(120, 54));
        open.GetComponent<RectTransform>().anchorMin = open.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);
        open.gameObject.SetActive(false);
        _openButton = open;
    }

    void Update()
    {
        if (_indicator) _indicator.SetActive(DevModeRuntime.Enabled);
        if (_openButton && _canvas && _activePanel) _openButton.gameObject.SetActive(DevModeRuntime.Enabled && !_activePanel.activeSelf);
    }

    void OpenSetup() { if (_setupPanel) _setupPanel.SetActive(true); }
    void CloseSetup() { if (_setupPanel) _setupPanel.SetActive(false); }
    void StartDevRun()
    {
        string seed = RunSetupUI.ResolveStartSeed(_seed != null ? _seed.text : "");
        CloseSetup();
        GameFlowService.Instance?.StartDevRun(seed, _health.isOn, _money.isOn, _offers.isOn);
    }

    void OpenActive()
    {
        if (!DevModeRuntime.Enabled || _coordinator == null || _coordinator.HasBlockingMenu || _activePanel == null) return;
        if (_status) _status.text = $"Health: {(DevModeRuntime.InfiniteHealth ? "Infinite" : "Normal")}  |  Credits: {(DevModeRuntime.InfiniteMoney ? "Infinite" : "Normal")}\nBlack Market Goods: {(DevModeRuntime.UnlimitedShopOffers ? "All eligible" : "Normal limit")}";
        _coordinator.Push(this, _activePanel, null);
    }
    void CloseActive() { if (_coordinator && _coordinator.IsTop(this)) _coordinator.Pop(this); }
    void DisableCheats() { DevModeRuntime.Disable(); CloseActive(); }
    void ConfirmLose()
    {
        if (_confirmation == null || CombatManager.Instance == null || CombatManager.Instance.currentState is GameState.GameOver or GameState.GameWon) return;
        _confirmation.Open(_coordinator, "LOSE RUN NOW?", "Immediately end this run and show the normal defeat result?", "LOSE RUN", () => DevModeRuntime.TryForceDefeat());
    }

    static GameObject MakePanel(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = size;
        go.GetComponent<Image>().color = PanelColor;DealbreakUIStyle.Frame(go.transform);return go;
    }
    static Button MakeButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action, Vector2? size = null)
    {
        var go = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f); rt.anchoredPosition = position; rt.sizeDelta = size ?? new Vector2(430,48);
        var image = go.GetComponent<Image>(); image.color = ButtonColor;
        var button = go.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
        AddText(go.transform, label, Vector2.zero, 20, TextAlignmentOptions.Center); return button;
    }
    static TMP_Text AddText(Transform parent, string value, Vector2 position, float fontSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f); rt.anchoredPosition = position; rt.sizeDelta = new Vector2(550,44);
        var text = go.GetComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = fontSize; text.alignment = alignment; text.color = Color.white; text.raycastTarget = false; return text;
    }
    static TMP_InputField MakeInput(Transform parent, string placeholder, Vector2 position)
    {
        var go = new GameObject("Seed Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField)); go.transform.SetParent(parent, false);
        var rt=(RectTransform)go.transform; rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f); rt.anchoredPosition=position; rt.sizeDelta=new Vector2(430,44);
        go.GetComponent<Image>().color=Color.white;
        var text=AddText(go.transform,"",Vector2.zero,20,TextAlignmentOptions.Left); text.color=Color.black;
        var input=go.GetComponent<TMP_InputField>(); input.textComponent=text; input.placeholder=AddText(go.transform,placeholder,Vector2.zero,18,TextAlignmentOptions.Left); input.placeholder.color=Color.gray; return input;
    }
    static Toggle MakeToggle(Transform parent, string label, Vector2 position, bool initial)
    {
        var go=new GameObject(label+" Toggle",typeof(RectTransform),typeof(Toggle)); go.transform.SetParent(parent,false);
        var rt=(RectTransform)go.transform; rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f); rt.anchoredPosition=position; rt.sizeDelta=new Vector2(430,42);
        var box=new GameObject("Check",typeof(RectTransform),typeof(Image)); box.transform.SetParent(go.transform,false); var b=(RectTransform)box.transform; b.anchorMin=b.anchorMax=new Vector2(0,.5f); b.anchoredPosition=new Vector2(15,0); b.sizeDelta=new Vector2(26,26); box.GetComponent<Image>().color=Color.white;
        var mark=new GameObject("Mark",typeof(RectTransform),typeof(Image)); mark.transform.SetParent(box.transform,false); var m=(RectTransform)mark.transform; m.anchorMin=Vector2.zero;m.anchorMax=Vector2.one;m.offsetMin=m.offsetMax=new Vector2(5,5); mark.GetComponent<Image>().color=new Color(.1f,.7f,.25f);
        var toggle=go.GetComponent<Toggle>(); toggle.targetGraphic=box.GetComponent<Image>(); toggle.graphic=mark.GetComponent<Image>(); toggle.isOn=initial;
        AddText(go.transform,label,new Vector2(40,0),20,TextAlignmentOptions.Left); return toggle;
    }
}
#endif
