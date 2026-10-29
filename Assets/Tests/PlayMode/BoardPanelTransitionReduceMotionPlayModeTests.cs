using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// HUD slice S18 in Play mode: under Reduce Motion, <see cref="BoardPanelTransition"/> Show, Hide and
/// the overlay fade-in are cuts, finished in the frame they are called.
/// </summary>
public sealed class BoardPanelTransitionReduceMotionPlayModeTests
{
    GameObject _root;

    [SetUp]
    public void SetUp() => CutSheetMotion.ReduceMotionOverride = true;

    [TearDown]
    public void TearDown()
    {
        CutSheetMotion.ReduceMotionOverride = null;
        if (_root) Object.Destroy(_root);
    }

    static bool Animating(GameObject panel) =>
        panel.TryGetComponent<BoardPanelTransition>(out var transition) &&
        (bool)typeof(BoardPanelTransition).GetField("_animating", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(transition);

    static void AssertShown(GameObject panel, string step)
    {
        var group = panel.GetComponent<CanvasGroup>();
        Assert.That(panel.activeSelf, Is.True, step + ": active");
        Assert.That(group.alpha, Is.EqualTo(1f), step + ": alpha");
        Assert.That(group.interactable, Is.True, step + ": interactable");
        Assert.That(group.blocksRaycasts, Is.True, step + ": blocks raycasts");
        Assert.That(panel.transform.localScale, Is.EqualTo(Vector3.one), step + ": scale");
        Assert.That(Animating(panel), Is.False, step + ": not animating");
    }

    [UnityTest]
    public IEnumerator ShowHideAndFadeIn_AreCutsUnderReduceMotion()
    {
        Assert.That(Application.isPlaying, Is.True);
        _root = new GameObject("Reduce Motion Board", typeof(RectTransform), typeof(Canvas));
        var panel = new GameObject("Reduce Motion Panel", typeof(RectTransform), typeof(CanvasGroup));
        panel.transform.SetParent(_root.transform, false);
        panel.SetActive(false);

        BoardPanelTransition.Show(panel);
        AssertShown(panel, "Show");

        BoardPanelTransition.Hide(panel);
        var group = panel.GetComponent<CanvasGroup>();
        Assert.That(panel.activeSelf, Is.False, "Hide: inactive");
        Assert.That(group.alpha, Is.EqualTo(0f), "Hide: alpha");
        Assert.That(group.interactable, Is.False, "Hide: interactable");
        Assert.That(group.blocksRaycasts, Is.False, "Hide: blocks raycasts");
        Assert.That(Animating(panel), Is.False, "Hide: not animating");

        BoardPanelTransition.FadeIn(panel);
        AssertShown(panel, "FadeIn");

        // Nothing left running changes it on the next frame.
        yield return null;
        AssertShown(panel, "FadeIn, next frame");
    }
}
