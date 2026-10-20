using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Art direction correction (2026-10-21): modern neo-punk editorial. A near-black stage, off-white
/// type, signal red for attack/damage/danger and acid lime for selection and valid targets. Texture
/// lives in artwork and one faint background grain, not on every surface. Runs last in the authoring
/// chain (after DealbreakCentralStageAuthoring) and can be run on its own.
/// </summary>
public static class DealbreakNeoPunkAuthoring
{
    // Theme fields keep their legacy names; their roles are now: ink = stage, charcoal = grouped
    // surface, slate = hairline/disabled grey, paper = off-white type and cards, brick = signal red,
    // ochre = acid lime (selection, valid target, valid drop).
    static readonly Color Ink = Hex(0x0B0B0C), Charcoal = Hex(0x16161A), Slate = Hex(0x3A3A40);
    static readonly Color Paper = Hex(0xECEAE4), Signal = Hex(0xE8322B), Lime = Hex(0xC8F031);

    [MenuItem("Dealbreak/UI/Apply Neo-Punk Correction")]
    public static void Apply()
    {
        Palette();
        Edit("Assets/Scenes/Game.unity", Stage);
        Edit("Assets/Scenes/MainMenu.unity", Stage);
        AssetDatabase.SaveAssets();
    }

    static void Palette()
    {
        var theme = Resources.Load<DealbreakUITheme>("DealbreakUITheme");
        theme.ink = Ink; theme.charcoal = Charcoal; theme.slate = Slate;
        theme.paper = Paper; theme.brick = Signal; theme.ochre = Lime;
        theme.labelFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Art/Fonts/Barlow Condensed UI.asset");
        EditorUtility.SetDirty(theme);
    }

    // The wordmark: DEAL in off-white, BREAK in signal red.
    const string Wordmark = "DEAL<color=#E8322B>BREAK</color>";

    static void Wordmarks(Scene scene)
    {
        foreach (var text in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TMPro.TMP_Text>(true))
                     .Where(t => t.name is "GameTitle" or "Dealbreak Shell Title"))
        {
            text.richText = true;
            text.text = Wordmark;
            EditorUtility.SetDirty(text);
        }
    }

    // A near-black stage with one faint photocopy grain; panels no longer carry their own blue field.
    static void Stage(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var background = root.transform.Find("Background");
            if (background)
            {
                if (background.TryGetComponent<Image>(out var image)) image.color = Ink;
                var field = background.Find("Slate Printed Field");
                if (field && field.TryGetComponent<RawImage>(out var grain)) grain.color = new Color(Paper.r, Paper.g, Paper.b, .045f);
            }
            foreach (var environment in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Environmental Slate"))
                environment.gameObject.SetActive(false);
        }
        Wordmarks(scene);
    }

    static void Edit(string path, System.Action<Scene> action)
    {
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try { action(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    static Color Hex(int rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
}
