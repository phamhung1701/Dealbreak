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

    // S9 (hud.md section C): the status band is centred at x .18-.76, y .37-.42, below INCOMING.
    [Test]
    public void StatusBand_IsCentredBelowIncomingAtY037To042()
    {
        var root = Canvas();
        var actions = root.GetComponent<ActionButtonsUI>();
        Absolute(actions.turnStateLabel.rectTransform, root.transform, out var min, out var max);
        Absolute(actions.pendingDamageLabel.rectTransform, root.transform, out var incomingMin, out _);

        Assert.That(min.y, Is.EqualTo(.37f).Within(Tolerance), "Status band bottom.");
        Assert.That(max.y, Is.EqualTo(.42f).Within(Tolerance), "Status band top.");
        Assert.That(min.x, Is.EqualTo(.18f).Within(Tolerance), "Status band left.");
        Assert.That(max.x, Is.EqualTo(.76f).Within(Tolerance), "Status band right.");
        Assert.That(max.y, Is.LessThanOrEqualTo(incomingMin.y + .0001f), "The status band sits below INCOMING, never over it.");
        Assert.That(actions.turnStateLabel.fontSize, Is.GreaterThanOrEqualTo(22f));
    }

    // S10a (hud.md section C, owner ruling 2026-10-27): the HP panel sits at x .025-.235, y .014-.082,
    // written by Dealbreak/UI/Apply Player Corner Layout.
    [Test]
    public void HpPanel_SitsAtX025To235AndY014To082()
    {
        var root = Canvas();
        var panel = root.GetComponent<DealbreakUIRuntime>().playerState;
        Absolute(panel, root.transform, out var min, out var max);

        Assert.That(min.x, Is.EqualTo(.025f).Within(Tolerance), "HP panel left.");
        Assert.That(max.x, Is.EqualTo(.235f).Within(Tolerance), "HP panel right.");
        Assert.That(min.y, Is.EqualTo(.014f).Within(Tolerance), "HP panel bottom.");
        Assert.That(max.y, Is.EqualTo(.082f).Within(Tolerance), "HP panel top.");
        // Sub-pixel float residue from the anchor rewrite is fine; a stray offset is pixels.
        Assert.That(panel.offsetMin.magnitude, Is.LessThan(.01f), "No stray offsets move it.");
        Assert.That(panel.offsetMax.magnitude, Is.LessThan(.01f), "No stray offsets move it.");
        Assert.That(min.x * RefWidth, Is.GreaterThanOrEqualTo(24f), "24px left edge margin.");
    }

    [Test]
    public void HpPanel_CarriesTheCreditsCoinAndTheHpNumberPreset()
    {
        var root = Canvas();
        var panel = root.GetComponent<DealbreakUIRuntime>().playerState;
        Assert.That(panel.TryGetComponent<PlayerCornerUI>(out var corner), Is.True);
        Assert.That(UnityEditor.AssetDatabase.GetAssetPath(corner.coinSprite), Is.EqualTo("Assets/Art/UI/Sprites/Credits_Coin.png"));
        Assert.That(UnityEditor.AssetDatabase.GetAssetPath(corner.hpNumberMaterial), Is.EqualTo("Assets/Art/Fonts/Courier Prime UI - Paper Outline.mat"));
        var label = root.GetComponent<ActionButtonsUI>().playerHealthLabel;
        Assert.That(label.fontSharedMaterial, Is.SameAs(corner.hpNumberMaterial));
        Assert.That(label.fontSize, Is.EqualTo(20f));
        var frame = panel.Find("Editorial Frame");
        Assert.That(frame == null || !frame.gameObject.activeSelf, Is.True, "The old charcoal frame no longer draws.");
    }

    // A stretched rect's anchors in the canvas root's fractions, composed up its parents (offsets are zero).
    // S10b (hud.md section C): the action bar re-centred under the hand, the sort column inside it.
    [Test]
    public void ActionBar_SpansX025To069AtY0016To0086()
    {
        var root = Canvas();
        var actions = root.GetComponent<ActionButtonsUI>();
        Absolute((RectTransform)actions.playButton.transform.parent, root.transform, out var min, out var max);

        Assert.That(min.x, Is.EqualTo(.25f).Within(Tolerance), "Action bar left.");
        Assert.That(max.x, Is.EqualTo(.69f).Within(Tolerance), "Action bar right.");
        Assert.That((min.x + max.x) * .5f, Is.EqualTo(.47f).Within(Tolerance), "Centred under the hand.");
        Assert.That(min.y, Is.EqualTo(.016f).Within(Tolerance), "Action bar bottom.");
        Assert.That(max.y, Is.EqualTo(.086f).Within(Tolerance), "Action bar top.");
    }

    [Test]
    public void SortButtons_StackInsideTheActionBarsLeft18Percent()
    {
        var root = Canvas();
        var actions = root.GetComponent<ActionButtonsUI>();
        Absolute((RectTransform)actions.playButton.transform.parent, root.transform, out var barMin, out var barMax);
        float columnRight = barMin.x + (barMax.x - barMin.x) * .18f;
        Absolute((RectTransform)actions.playButton.transform, root.transform, out var attackMin, out _);
        Absolute((RectTransform)actions.rankSortButton.transform, root.transform, out var rankMin, out var rankMax);
        Absolute((RectTransform)actions.suitSortButton.transform, root.transform, out var suitMin, out var suitMax);

        foreach (var (label, min, max) in new[] { ("Rank sort", rankMin, rankMax), ("Suit sort", suitMin, suitMax) })
        {
            Assert.That(min.x, Is.GreaterThanOrEqualTo(barMin.x - .0001f), label + " left inside the bar.");
            Assert.That(max.x, Is.LessThanOrEqualTo(columnRight + .0001f), label + " inside the bar's left 18%.");
            Assert.That(min.y, Is.GreaterThanOrEqualTo(barMin.y - .0001f), label + " bottom inside the bar.");
            Assert.That(max.y, Is.LessThanOrEqualTo(barMax.y + .0001f), label + " top inside the bar.");
            Assert.That((max.y - min.y) * RefHeight, Is.EqualTo(29f).Within(1.5f), label + " is 29px tall.");
        }
        Assert.That(rankMin.y, Is.GreaterThanOrEqualTo(suitMax.y), "Stacked, rank above suit, not overlapping.");
        Assert.That(attackMin.x, Is.GreaterThanOrEqualTo(columnRight - .0001f), "ATTACK starts after the sort column.");
    }

    // S11 right column (Dealbreak/UI/Apply Right Column Layout; MAP and DISCARD are runtime rects).
    static Rect MapRect() => Rect.MinMaxRect(
        (RefWidth + PathScreenUI.MapButtonOffset.x - PathScreenUI.MapButtonSize.x) / RefWidth,
        (RefHeight + PathScreenUI.MapButtonOffset.y - PathScreenUI.MapButtonSize.y) / RefHeight,
        (RefWidth + PathScreenUI.MapButtonOffset.x) / RefWidth,
        (RefHeight + PathScreenUI.MapButtonOffset.y) / RefHeight);

    static Rect DiscardRect() => Rect.MinMaxRect(ItemDragUI.DiscardMinX, ItemDragUI.DiscardMinY, ItemDragUI.DiscardMaxX, ItemDragUI.DiscardMaxY);

    [Test]
    public void MapButton_SitsAtX09085To09875AndY0837To0876()
    {
        var map = MapRect();
        Assert.That(map.xMin, Is.EqualTo(.9085f).Within(Tolerance));
        Assert.That(map.xMax, Is.EqualTo(.9875f).Within(Tolerance));
        Assert.That(map.yMin, Is.EqualTo(.837f).Within(Tolerance));
        Assert.That(map.yMax, Is.EqualTo(.876f).Within(Tolerance));
        Assert.That(PathScreenUI.MapButtonSize, Is.EqualTo(new Vector2(152f, 42f)));
        // At 16:10 the canvas grows to 1920x1200; MAP stays pinned 134px under the top, like the header rows.
        Assert.That(-PathScreenUI.MapButtonOffset.y, Is.GreaterThanOrEqualTo(134f));
    }

    [Test]
    public void DrawPile_SitsAtX0871To098AndY0187To050()
    {
        var root = Canvas();
        var deck = root.GetComponentInChildren<DrawDeckStackUI>(true);
        Absolute((RectTransform)deck.transform, root.transform, out var min, out var max);
        Assert.That(min.x, Is.EqualTo(.871f).Within(Tolerance));
        Assert.That(max.x, Is.EqualTo(.98f).Within(Tolerance));
        Assert.That(min.y, Is.EqualTo(.187f).Within(Tolerance));
        Assert.That(max.y, Is.EqualTo(.50f).Within(Tolerance));
    }

    [Test]
    public void DiscardStrip_SitsAtX0895To0985AndY055To083_ClearOfMapAndTheContractLine()
    {
        var discard = DiscardRect();
        Assert.That(discard.xMin, Is.EqualTo(.895f).Within(Tolerance));
        Assert.That(discard.xMax, Is.EqualTo(.985f).Within(Tolerance));
        Assert.That(discard.yMin, Is.EqualTo(.55f).Within(Tolerance));
        Assert.That(discard.yMax, Is.EqualTo(.83f).Within(Tolerance));
        Assert.That(discard.Overlaps(MapRect()), Is.False, "DISCARD clears MAP.");

        var root = Canvas();
        var status = root.GetComponent<RunStatusUI>();
        Absolute(status.runProgressLabel.rectTransform, root.transform, out var min, out var max);
        Assert.That(discard.Overlaps(Rect.MinMaxRect(min.x, min.y, max.x, max.y)), Is.False, "DISCARD clears the Contract line.");
        var deck = root.GetComponentInChildren<DrawDeckStackUI>(true);
        Absolute((RectTransform)deck.transform, root.transform, out _, out var pileMax);
        Assert.That(min.y, Is.GreaterThanOrEqualTo(pileMax.y - .0001f), "The Contract line sits above the pile.");
        Assert.That(max.x, Is.EqualTo(pileMax.x).Within(Tolerance), "The Contract line is right-aligned with the pile.");
    }

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
