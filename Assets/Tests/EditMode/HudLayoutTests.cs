using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// hud.md section C anchors in Game.unity (1920x1080 reference, canvas fractions), written by
// DealbreakCentralStageAuthoring (Dealbreak/UI/Apply Enemy Row Layout).
public sealed class HudLayoutTests
{
    const string GamePath = "Assets/Scenes/Game.unity";
    const float RefWidth = 1920f, RefHeight = 1080f, Tolerance = .01f;
    readonly List<Scene> _opened = new();

    Scene Open(string path)
    {
        var scene = SceneManager.GetSceneByPath(path);
        if (scene.IsValid() && scene.isLoaded) return scene;
        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        _opened.Add(scene);
        return scene;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var scene in _opened) if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        _opened.Clear();
    }

    GameObject Canvas() => Open(GamePath).GetRootGameObjects().First(g => g.name == "Canvas");

    [Test]
    public void EnemyRow_SitsAtY049To0846AndEndsAtX0871()
    {
        var root = Canvas();
        var container = root.GetComponent<EnemyGroupUI>().enemyContainer;
        Absolute(container, root.transform, out var min, out var max);
        var padding = container.TryGetComponent(out HorizontalLayoutGroup layout) ? layout.padding : new RectOffset();

        float bottom = min.y + padding.bottom / RefHeight;
        float top = max.y - padding.top / RefHeight;
        float right = max.x - padding.right / RefWidth;

        Assert.That(bottom, Is.EqualTo(.49f).Within(Tolerance), "Enemy row bottom (raised about 20px).");
        Assert.That(top, Is.EqualTo(.846f).Within(Tolerance), "Enemy row top.");
        Assert.That(right, Is.EqualTo(.871f).Within(Tolerance), "The enemy row ends clear of the draw pile.");
    }

    [Test]
    public void IncomingTag_IsCentredUnderTheEnemyRowAtY043To047()
    {
        var root = Canvas();
        var actions = root.GetComponent<ActionButtonsUI>();
        Absolute(actions.pendingDamageLabel.rectTransform, root.transform, out var min, out var max);
        var container = root.GetComponent<EnemyGroupUI>().enemyContainer;
        Absolute(container, root.transform, out var rowMin, out var rowMax);
        float rowRight = rowMax.x - (container.TryGetComponent(out HorizontalLayoutGroup layout) ? layout.padding.right : 0) / RefWidth;

        Assert.That(min.y, Is.EqualTo(.43f).Within(Tolerance));
        Assert.That(max.y, Is.EqualTo(.47f).Within(Tolerance));
        Assert.That((min.x + max.x) * .5f, Is.EqualTo((rowMin.x + rowRight) * .5f).Within(Tolerance), "Centred under the enemy row.");
    }

    // A stretched rect's anchors in the canvas root's fractions, composed up its parents (offsets are zero).
    static void Absolute(RectTransform rect, Transform canvasRoot, out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.one;
        for (var r = rect; r && r != canvasRoot; r = r.parent as RectTransform)
        {
            var a = r.anchorMin;
            var b = r.anchorMax;
            min = a + Vector2.Scale(b - a, min);
            max = a + Vector2.Scale(b - a, max);
        }
    }
}
