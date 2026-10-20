using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DealbreakPlayerCopyTests
{
    Scene _mainMenuScene;
    bool _openedAdditively;
    Scene _previousActiveScene;

    [SetUp]
    public void SetUp()
    {
        _previousActiveScene = EditorSceneManager.GetActiveScene();
        _mainMenuScene = SceneManager.GetSceneByPath("Assets/Scenes/MainMenu.unity");
        if (!_mainMenuScene.IsValid() || !_mainMenuScene.isLoaded)
        {
            _mainMenuScene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Additive);
            _openedAdditively = true;
        }
    }

    [TearDown]
    public void TearDown()
    {
        if (_openedAdditively && _mainMenuScene.IsValid() && _mainMenuScene.isLoaded)
            EditorSceneManager.CloseScene(_mainMenuScene, true);
        if (_previousActiveScene.IsValid() && _previousActiveScene.isLoaded)
            EditorSceneManager.SetActiveScene(_previousActiveScene);
        _openedAdditively = false;
    }

    [Test]
    public void MenuUsesDealbreakTitleWithoutSecondaryTaglineOrRedundantSeedDefinitions()
    {
        var title = Find(_mainMenuScene, "Canvas/FrontendRoot/HomePanel/HomeCard/GameTitle")?.GetComponent<TMP_Text>();
        var tagline = Find(_mainMenuScene, "Canvas/FrontendRoot/HomePanel/HomeCard/Tagline");
        Assert.That(title, Is.Not.Null);
        // The wordmark styles BREAK with rich-text tags; the words themselves stay DEALBREAK.
        Assert.That(System.Text.RegularExpressions.Regex.Replace(title.text, "<[^>]*>", ""), Is.EqualTo("DEALBREAK"));
        Assert.That(tagline == null || !tagline.activeSelf, Is.True,
            "A removed or disabled tagline must not be shown.");

        var texts = new List<TMP_Text>();
        foreach (var root in _mainMenuScene.GetRootGameObjects())
            texts.AddRange(root.GetComponentsInChildren<TMP_Text>(true).Where(item => item.gameObject.activeInHierarchy));
        var gameScene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        if (gameScene.IsValid() && gameScene.isLoaded)
            foreach (var root in gameScene.GetRootGameObjects())
                texts.AddRange(root.GetComponentsInChildren<TMP_Text>(true).Where(item => item.gameObject.activeInHierarchy));

        Assert.That(texts.Any(item => item.text.Contains("CardPG is", System.StringComparison.OrdinalIgnoreCase)), Is.False);
        Assert.That(texts.Any(item => item.text.Contains("A seed is", System.StringComparison.OrdinalIgnoreCase)), Is.False);
    }

    static GameObject Find(Scene scene, string path)
    {
        var parts = path.Split('/');
        var current = scene.GetRootGameObjects().FirstOrDefault(root => root.name == parts[0])?.transform;
        for (int i = 1; i < parts.Length && current != null; i++) current = current.Find(parts[i]);
        return current != null ? current.gameObject : null;
    }
}
