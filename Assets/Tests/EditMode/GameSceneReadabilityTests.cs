using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GameSceneReadabilityTests
{
    const string GameScenePath = "Assets/Scenes/Game.unity";
    Scene _scene;
    bool _openedAdditively;

    [SetUp]
    public void SetUp()
    {
        _scene = SceneManager.GetSceneByPath(GameScenePath);
        if (!_scene.IsValid() || !_scene.isLoaded)
        {
            _scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            _openedAdditively = true;
        }
    }

    [TearDown]
    public void TearDown()
    {
        if (_openedAdditively && _scene.IsValid() && _scene.isLoaded)
            EditorSceneManager.CloseScene(_scene, true);
        _openedAdditively = false;
    }

    [Test]
    public void ResultSummaryIsScrollableAndKeepsDedicatedPrimaryAction()
    {
        var body = Find("FrontendOverlayCanvas/RunResultsPanel/RunResultsCard/ResultSummaryViewport/ResultBodyLabel");
        var scroll = body != null ? body.GetComponentInParent<ScrollRect>(true) : null;
        Assert.That(scroll, Is.Not.Null);
        Assert.That(scroll.vertical, Is.True);
        Assert.That(scroll.horizontal, Is.False);
        Assert.That(scroll.content, Is.SameAs(body.GetComponent<RectTransform>()));
        Assert.That(scroll.verticalScrollbar, Is.Not.Null);
        Assert.That(scroll.viewport, Is.Not.Null);
        Assert.That(scroll.viewport.rect.height, Is.GreaterThan(100f));
        Assert.That(Find("FrontendOverlayCanvas/RunResultsPanel/RunResultsCard/RetrySameSeedButton"), Is.Not.Null);
    }

    [Test]
    public void PassActionIsAuthoredAsAReadableActionBarButton()
    {
        var actionButtons = Find("Canvas")?.GetComponent<ActionButtonsUI>();
        var passObject = Find("Canvas/PlayerArea/ActionBar/PassButton");
        Assert.That(actionButtons, Is.Not.Null);
        Assert.That(passObject, Is.Not.Null);
        Assert.That(actionButtons.passButton, Is.SameAs(passObject.GetComponent<Button>()));
        Assert.That(passObject.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("PASS"));
        AssertHitTarget("Canvas/PlayerArea/ActionBar/PassButton", 60f, 56f);
    }

    [Test]
    public void ImportantGameHudTextHasReadableMinimumSizesAtReferenceResolution()
    {
        var canvas = Find("Canvas");
        Assert.That(canvas, Is.Not.Null);
        var scaler = canvas.GetComponent<CanvasScaler>();
        Assert.That(scaler, Is.Not.Null);
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));

        AssertReadable("Canvas/CreditsLabel", 20f);
        AssertReadable("Canvas/PlayerArea/HealthPanel/PlayerHealthLabel", 19f);
        AssertReadable("Canvas/PlayerArea/ActionBar/PlayButton/PlayCardButtonLabel", 19f);
        AssertReadable("Canvas/PlayerArea/ActionBar/BlockButton/PlayCardButtonLabel", 19f);
        // S9: the status tag prints the state and the selection preview on TurnStateLabel (22pt).
        AssertReadable("Canvas/CombatStatusPanel/TurnStateLabel", 22f);
        AssertReadable("Canvas/RunHeader/RunProgressLabel", 17f);
        AssertReadable("Canvas/OwnedGearBar/GearHeader", 18f);
        AssertReadable("Canvas/PathPanel/PathCard/PathInstructionLabel", 16f);
        AssertReadable("Canvas/ShopPanel/ShopCard/ShopTitleLabel", 21f);
        AssertReadable("Canvas/EnhancementTargetPanel/TargetPickerCard/ConfirmTargetButton/ContinueRunButtonLabel", 20f);
        AssertReadable("Canvas/EnhancementTargetPanel/TargetPickerCard/BackToOfferButton/ContinueRunButtonLabel", 20f);
    }

    [Test]
    public void ImportantLabelsAndPrimaryButtonsFitTheir1920ReferenceRects()
    {
        Canvas.ForceUpdateCanvases();
        AssertTextFits("Canvas/CreditsLabel");
        AssertTextFits("Canvas/RunHeader/RunProgressLabel");
        AssertTextFits("Canvas/RunHeader/HeadlinerProgressLabel");
        AssertTextFits("Canvas/CombatStatusPanel/TurnStateLabel");
        AssertTextFits("Canvas/PlayerArea/ActionBar/PlayButton/PlayCardButtonLabel");
        AssertTextFits("Canvas/PlayerArea/ActionBar/BlockButton/PlayCardButtonLabel");
        AssertTextFits("Canvas/PathPanel/PathCard/PathInstructionLabel");
        AssertTextFits("Canvas/ShopPanel/ShopCard/ShopTitleLabel");

        AssertHitTarget("Canvas/PlayerArea/ActionBar/PlayButton", 200f, 56f);
        AssertHitTarget("Canvas/PlayerArea/ActionBar/BlockButton", 180f, 56f);
    }

    void AssertReadable(string path, float minimumSize)
    {
        var label = Find(path)?.GetComponent<TMP_Text>();
        Assert.That(label, Is.Not.Null, path);
        Assert.That(label.fontSizeMax, Is.GreaterThanOrEqualTo(minimumSize), path + " maximum font size");
        if (label.enableAutoSizing)
            Assert.That(label.fontSizeMin, Is.GreaterThanOrEqualTo(minimumSize), path + " minimum autosized font size");
    }

    void AssertTextFits(string path)
    {
        var label = Find(path)?.GetComponent<TMP_Text>();
        Assert.That(label, Is.Not.Null, path);
        if (string.IsNullOrWhiteSpace(label.text)) Assert.Ignore(path + " has no authored preview text.");
        var rect = label.rectTransform.rect;
        float preferredHeight = label.GetPreferredValues(label.text, Mathf.Max(1f, rect.width - 8f), 0f).y;
        Assert.That(preferredHeight, Is.LessThanOrEqualTo(rect.height + 2f),
            $"{path}: preferred height {preferredHeight:0.0} exceeds rect {rect.height:0.0} at the 1920×1080 reference resolution.");
    }

    void AssertHitTarget(string path, float minWidth, float minHeight)
    {
        var rect = Find(path)?.GetComponent<RectTransform>();
        Assert.That(rect, Is.Not.Null, path);
        Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(minWidth), path + " width");
        Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(minHeight), path + " height");
    }

    GameObject Find(string path)
    {
        string[] parts = path.Split('/');
        GameObject[] roots = _scene.GetRootGameObjects();
        Transform current = null;
        for (int i = 0; i < roots.Length; i++)
            if (roots[i].name == parts[0]) { current = roots[i].transform; break; }
        if (current == null) return null;
        for (int i = 1; i < parts.Length; i++)
        {
            current = current.Find(parts[i]);
            if (current == null) return null;
        }
        return current.gameObject;
    }
}
