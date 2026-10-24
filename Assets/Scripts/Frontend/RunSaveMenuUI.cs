using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RunSaveMenuUI : MonoBehaviour
{
    public Button ContinueButton { get; private set; }
    TMP_Text _status;
    public static void Install(MainMenuUI menu)
    {
        if (menu.newRunButton == null) return;
        var ui = menu.GetComponent<RunSaveMenuUI>();
        if (ui == null) { ui = menu.gameObject.AddComponent<RunSaveMenuUI>(); ui.Build(menu); }
        ui.Refresh();
    }
    void Build(MainMenuUI menu)
    {
        ContinueButton = Instantiate(menu.newRunButton, menu.newRunButton.transform.parent);
        ContinueButton.name = "ContinueRunButton";
        ContinueButton.onClick = new Button.ButtonClickedEvent();
        ContinueButton.onClick.AddListener(Continue);
        var label = ContinueButton.GetComponentInChildren<TMP_Text>(); if (label) label.text = "CONTINUE";
        var buttons = new[] { ContinueButton, menu.newRunButton, menu.TutorialButton, menu.settingsButton, menu.creditsButton, menu.quitButton };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (!buttons[i]) continue;
            var rect = buttons[i].GetComponent<RectTransform>();
            if(DealbreakUIStyle.Theme) rect.anchorMin=rect.anchorMax=new Vector2(.22f,.32f);
            rect.anchoredPosition = new Vector2(0, 175 - i * 65); rect.sizeDelta = new Vector2(430, 54);
            if(buttons[i].GetComponent<EditorialSurface>()?.treatment==EditorialSurface.Treatment.Open)
            {rect.anchorMin=rect.anchorMax=new Vector2(.08f,.32f);rect.pivot=new Vector2(0,.5f);var text=buttons[i].GetComponentInChildren<TMP_Text>();if(text)text.alignment=TextAlignmentOptions.MidlineLeft;}
        }
        var go = new GameObject("RunSaveStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(menu.newRunButton.transform.parent, false);
        var sr = (RectTransform)go.transform; sr.anchorMin = sr.anchorMax = new Vector2(.5f, .5f);
        if(DealbreakUIStyle.Theme)sr.anchorMin=sr.anchorMax=new Vector2(.22f,.32f);
        sr.anchoredPosition = new Vector2(0, -225); sr.sizeDelta = new Vector2(570, 70);
        _status = go.GetComponent<TMP_Text>(); _status.fontSize = 18; _status.alignment = TextAlignmentOptions.Center; _status.raycastTarget = false;
        if(menu.newRunButton.GetComponent<EditorialSurface>()?.treatment==EditorialSurface.Treatment.Open)
        {sr.anchorMin=sr.anchorMax=new Vector2(.08f,.32f);sr.pivot=new Vector2(0,.5f);_status.alignment=TextAlignmentOptions.MidlineLeft;}
    }
    void Continue()
    {
        if (SavePromptUI.IsOpen) return;
        GameFlowService.Instance?.ContinueRun();
        Refresh();
    }
    public void Refresh()
    {
        if (ContinueButton == null) return;
        bool valid = SaveGameService.TryRead(out var data, out var error);
        ContinueButton.interactable = valid;
        if (_status) _status.text = valid
            ? $"Saved run • District {data.bossIndex + 1} • {data.cards.gold} Credits" + (SaveGameService.Store.RecoveredFromBackup ? "\nRecovered from backup" : string.Empty)
            : error;
    }
}
