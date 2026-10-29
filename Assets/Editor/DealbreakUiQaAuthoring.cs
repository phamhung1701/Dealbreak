using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene corrections from the 2026-10-24 UI QA pass. Runs after DealbreakNeoPunkAuthoring (which calls
/// it) and can be run on its own: every canvas only grows past 1920×1080 (CanvasScaler Expand) so no
/// aspect ratio squeezes the 16:9 layout; the header rows are pinned (DealbreakCentralStageAuthoring);
/// full-screen menus show the same stage and grain as the home screen; old blue-slate frames take the
/// palette; the detail's scrollbar only shows when it overflows; the VSync row lines up; the Incident
/// title matches the Black Market's; the credits name every font and the art pack.
/// </summary>
public static class DealbreakUiQaAuthoring
{
    static readonly Color Ink = new(.043f, .043f, .047f), Charcoal = new(.086f, .086f, .102f), Slate = new(.227f, .227f, .251f);
    static readonly Color OldPanel = new(.055f, .065f, .075f), OldFill = new(.105f, .13f, .155f), OldRim = new(.29f, .36f, .41f);
    const string GrainPath = "Assets/Art/UI/Textures/Slate_Grain.png";

    [MenuItem("Dealbreak/UI/Apply UI QA Corrections")]
    public static void Apply()
    {
        Edit("Assets/Scenes/MainMenu.unity", Scene);
        Edit("Assets/Scenes/Game.unity", Scene);
        DealbreakCentralStageAuthoring.ApplyInventoryLayout();
        DealbreakNeoPunkAuthoring.ApplyEnemyView();
        AssetDatabase.SaveAssets();
    }

    static void Scene(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        foreach (var scaler in roots.SelectMany(g => g.GetComponentsInChildren<CanvasScaler>(true)))
        {
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            EditorUtility.SetDirty(scaler);
        }
        foreach (var tooltip in roots.SelectMany(g => g.GetComponentsInChildren<ContextualTooltipUI>(true)))
        {
            if (!tooltip.scroll || !tooltip.panel) continue;
            var bar = tooltip.panel.transform.Find("Detail Scrollbar");
            if (bar) tooltip.scroll.verticalScrollbar = bar.GetComponent<Scrollbar>();
            tooltip.scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            EditorUtility.SetDirty(tooltip.scroll);
        }
        foreach (var settings in roots.SelectMany(g => g.GetComponentsInChildren<SettingsMenuUI>(true))) { MotionRows(settings); VSyncRow(settings); }
        var stage = roots.Select(g => g.transform.Find("Background/Slate Printed Field")).FirstOrDefault(t => t)?.GetComponent<RawImage>();
        foreach (var panel in roots.SelectMany(g => g.GetComponentsInChildren<Image>(true)).Where(IsOldMenuPanel).ToList())
            StageBackdrop(panel, stage);
        foreach (var frame in roots.SelectMany(g => g.GetComponentsInChildren<EditorialFrame>(true)))
        {
            bool changed = false;
            if (Near(frame.fill, OldFill)) { frame.fill = Charcoal; changed = true; }
            if (Near(frame.rim, OldRim)) { frame.rim = Slate; changed = true; }
            if (changed) EditorUtility.SetDirty(frame);
        }
        foreach (var incident in roots.SelectMany(g => g.GetComponentsInChildren<RunEventUI>(true)))
        {
            if (!incident.titleLabel) continue;
            incident.titleLabel.fontSize = 48;
            incident.titleLabel.fontStyle |= FontStyles.UpperCase;
            EditorUtility.SetDirty(incident.titleLabel);
        }
        foreach (var credits in roots.SelectMany(g => g.GetComponentsInChildren<CreditsUI>(true)))
        {
            credits.creditsText = CreditsUI.DefaultText;
            if (credits.bodyLabel) { credits.bodyLabel.text = CreditsUI.DefaultText; EditorUtility.SetDirty(credits.bodyLabel); }
            EditorUtility.SetDirty(credits);
        }
    }

    // A full-screen menu panel still filled with the retired blue-black.
    static bool IsOldMenuPanel(Image image)
    {
        var r = image.rectTransform;
        return image.sprite == null && Near(image.color, OldPanel) && image.color.a > .99f &&
               r.anchorMin == Vector2.zero && r.anchorMax == Vector2.one && r.offsetMin == Vector2.zero && r.offsetMax == Vector2.zero;
    }

    // The stage's ink and its one faint grain, so a menu reads as the same screen as home and the board.
    static void StageBackdrop(Image panel, RawImage stage)
    {
        panel.color = Ink;
        EditorUtility.SetDirty(panel);
        var grain = panel.transform.Find("Stage Grain") as RectTransform;
        if (!grain)
        {
            grain = new GameObject("Stage Grain", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RectTransform>();
            grain.SetParent(panel.transform, false);
            grain.gameObject.layer = panel.gameObject.layer;
        }
        grain.SetAsFirstSibling();
        grain.anchorMin = Vector2.zero; grain.anchorMax = Vector2.one; grain.offsetMin = grain.offsetMax = Vector2.zero;
        var image = grain.GetComponent<RawImage>();
        image.raycastTarget = false;
        image.texture = stage ? stage.texture : AssetDatabase.LoadAssetAtPath<Texture2D>(GrainPath);
        image.uvRect = stage ? stage.uvRect : new Rect(.03f, .12f, .9f, .85f);
        image.color = stage ? stage.color : new Color(.925f, .918f, .894f, .045f);
        EditorUtility.SetDirty(image);
    }

    // The VSync label takes the Resolution and Display Mode label column; its box starts where their
    // dropdowns do.
    static void VSyncRow(SettingsMenuUI settings)
    {
        var toggle = settings.vSyncToggle ? (RectTransform)settings.vSyncToggle.transform : null;
        var reference = settings.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "ResolutionLabel");
        var dropdown = settings.GetComponentsInChildren<TMP_Dropdown>(true).FirstOrDefault(d => d.name == "ResolutionDropdown");
        if (!toggle || !reference || !dropdown) return;
        var label = (RectTransform)reference.transform; var field = (RectTransform)dropdown.transform;
        if (toggle.parent != label.parent || field.parent != label.parent) return;
        float left = label.offsetMin.x, right = field.offsetMax.x, labelWidth = label.offsetMax.x - label.offsetMin.x;
        toggle.offsetMin = new Vector2(left, toggle.offsetMin.y);
        toggle.offsetMax = new Vector2(right, toggle.offsetMax.y);
        var text = settings.vSyncToggle.GetComponentInChildren<TMP_Text>(true);
        if (text)
        {
            var r = text.rectTransform;
            r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, .5f);
            r.offsetMin = Vector2.zero; r.offsetMax = new Vector2(labelWidth, 0);
            text.alignment = reference.alignment;
            text.margin = reference.margin;
            EditorUtility.SetDirty(text);
        }
        var box = (toggle.Find("Background") ?? toggle.Find("Box")) as RectTransform;
        if (box)
        {
            box.anchorMin = box.anchorMax = new Vector2(0, .5f); box.pivot = new Vector2(0, .5f);
            box.anchoredPosition = new Vector2(field.offsetMin.x - left, 0);
            EditorUtility.SetDirty(box);
        }
        EditorUtility.SetDirty(toggle);
    }

    // HUD slice S17: four motion rows under VSync, each a label in the Resolution label column and a
    // dropdown cloned from the Resolution dropdown; Status, Apply and Back move down to make room.
    // Idempotent: rows found by name are reused and every position is written absolutely.
    static readonly (string name, string label, float y)[] MotionRowLayout =
    {
        ("ReduceMotion", "Reduce Motion", -55f),
        ("ScreenShake", "Screen Shake", -125f),
        ("TriggerSpeed", "Trigger Speed", -195f),
        ("TooltipText", "Tooltip Text", -265f),
    };

    static void MotionRows(SettingsMenuUI settings)
    {
        var label = settings.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "ResolutionLabel");
        var dropdown = settings.GetComponentsInChildren<TMP_Dropdown>(true).FirstOrDefault(d => d.name == "ResolutionDropdown");
        if (!label || !dropdown || label.transform.parent != dropdown.transform.parent) return;
        var card = label.transform.parent;
        var rows = new TMP_Dropdown[MotionRowLayout.Length];
        for (int i = 0; i < MotionRowLayout.Length; i++)
        {
            var (name, text, y) = MotionRowLayout[i];
            var rowLabel = CloneRowPart(label.gameObject, card, name + "Label").GetComponent<TMP_Text>();
            rowLabel.text = text;
            rowLabel.rectTransform.anchoredPosition = new Vector2(((RectTransform)label.transform).anchoredPosition.x, y);
            EditorUtility.SetDirty(rowLabel);
            var row = CloneRowPart(dropdown.gameObject, card, name + "Dropdown").GetComponent<TMP_Dropdown>();
            ((RectTransform)row.transform).anchoredPosition = new Vector2(((RectTransform)dropdown.transform).anchoredPosition.x, y);
            var options = i < 2 ? SettingsMenuUI.OnOffOptions : i == 2 ? SettingsMenuUI.TriggerSpeedOptions : SettingsMenuUI.TooltipTextOptions;
            row.onValueChanged.RemoveAllListeners();
            row.ClearOptions();
            row.AddOptions(options.ToList());
            row.SetValueWithoutNotify(i == 1 ? 1 : 0);
            row.RefreshShownValue();
            EditorUtility.SetDirty(row);
            rows[i] = row;
        }
        settings.reduceMotionDropdown = rows[0];
        settings.screenShakeDropdown = rows[1];
        settings.triggerSpeedDropdown = rows[2];
        settings.tooltipTextDropdown = rows[3];
        EditorUtility.SetDirty(settings);
        MoveRow(settings.statusLabel ? settings.statusLabel.transform : null, card, -322f, 50f);
        MoveRow(settings.applyButton ? settings.applyButton.transform : null, card, -385f, 0f);
        MoveRow(settings.backButton ? settings.backButton.transform : null, card, -463f, 0f);
    }

    static GameObject CloneRowPart(GameObject source, Transform card, string name)
    {
        var existing = card.Find(name);
        if (existing) return existing.gameObject;
        var clone = Object.Instantiate(source, card, false);
        clone.name = name;
        return clone;
    }

    static void MoveRow(Transform row, Transform card, float y, float height)
    {
        if (!row || row.parent != card) return;
        var rect = (RectTransform)row;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        if (height > 0f) rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        EditorUtility.SetDirty(rect);
    }

    static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < .01f && Mathf.Abs(a.g - b.g) < .01f && Mathf.Abs(a.b - b.b) < .01f;

    static void Edit(string path, System.Action<Scene> action)
    {
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try { action(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
}
