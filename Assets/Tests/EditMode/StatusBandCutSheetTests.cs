using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD slice S9 (visual-design 3b, hud.md sections C and E): the status band's one tag and the
// board tone / defense dot screen.
public sealed class StatusBandCutSheetTests
{
    readonly List<Object> _objects = new();

    [TearDown]
    public void TearDown()
    {
        CutSheetMotion.ReduceMotionOverride = null;
        GameplayInputGate.Clear();
        foreach (var item in _objects.AsEnumerable().Reverse())
            if (item) Object.DestroyImmediate(item);
        _objects.Clear();
    }

    GameObject Make(string name, params System.Type[] components)
    {
        var item = new GameObject(name, components);
        _objects.Add(item);
        return item;
    }

    TMP_Text Label(string name = "TurnStateLabel")
    {
        var canvas = Make("Status Canvas", typeof(RectTransform), typeof(Canvas));
        var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        label.transform.SetParent(canvas.transform, false);
        return label;
    }

    [Test]
    public void Tag_IsOnePaperStubWithInkTypeNoTiltAndAThreePixelShadow()
    {
        var theme = DealbreakUIStyle.Theme;
        Assume.That(theme, Is.Not.Null, "The Cut Sheet theme is required.");
        var label = Label();
        var tag = StatusTagUI.Dress(label);
        Assert.That(StatusTagUI.Dress(label), Is.SameAs(tag), "Dressing twice keeps one tag.");
        Assert.That(label.transform.parent.GetComponentsInParent<StatusTagUI>(true).Length, Is.EqualTo(1));
        Assert.That(tag.Label, Is.SameAs(label), "The tag prints on the existing label; no second label.");
        Assert.That(tag.GetComponentsInChildren<TMP_Text>(true).Length, Is.EqualTo(1));

        Assert.That(tag.Face.color, Is.EqualTo(theme.paper));
        Assert.That(tag.Face.sprite, Is.SameAs(theme.tagSprite));
        Assert.That(label.color.r, Is.EqualTo(theme.inkPrint.r).Within(.001f));
        Assert.That(label.color.g, Is.EqualTo(theme.inkPrint.g).Within(.001f));
        Assert.That(label.color.b, Is.EqualTo(theme.inkPrint.b).Within(.001f));
        Assert.That(label.fontSize, Is.EqualTo(22f));
        Assert.That(label.fontStyle & FontStyles.UpperCase, Is.EqualTo(FontStyles.UpperCase));
        if (theme.labelFont) Assert.That(label.font, Is.SameAs(theme.labelFont));
        Assert.That(tag.TiltDegrees, Is.EqualTo(CutSheetMetrics.StatusTagTilt).Within(.001f));
        Assert.That(tag.Shadow.Depth, Is.EqualTo(CutSheetMetrics.ShadowSlot));
    }

    [Test]
    public void Tag_SetLineIsInstantUnderReduceMotionAndHidesWhenEmpty()
    {
        CutSheetMotion.ReduceMotionOverride = true;
        var tag = StatusTagUI.Dress(Label());
        Assert.That(tag.BodyVisible, Is.False, "No line, no tag.");
        tag.SetLine("PLAYER TURN");
        Assert.That(tag.ShownLine, Is.EqualTo("PLAYER TURN"));
        Assert.That(tag.BodyVisible, Is.True);
        tag.SetLine("1 selected • Attack power 8");
        Assert.That(tag.ShownLine, Is.EqualTo("1 selected • Attack power 8"), "Swap is instant under Reduce Motion.");
        Assert.That(tag.Label.alpha, Is.EqualTo(1f));
        tag.SetVisible(false);
        Assert.That(tag.BodyVisible, Is.False);
    }

    [Test]
    public void Compose_TheSelectionPreviewReplacesTheStateText()
    {
        Assert.That(StatusTagUI.Compose("PLAYER TURN", string.Empty), Is.EqualTo("PLAYER TURN"));
        Assert.That(StatusTagUI.Compose("PLAYER TURN", null), Is.EqualTo("PLAYER TURN"));
        Assert.That(StatusTagUI.Compose("PLAYER TURN", "1 selected • Attack power 8"), Is.EqualTo("1 selected • Attack power 8"));
        Assert.That(StatusTagUI.Compose("DEFENSE", "Blocks 1 of 2 • 3 damage left"), Is.EqualTo("Blocks 1 of 2 • 3 damage left"));
    }

    [Test]
    public void ActionButtons_WriteTheStateIntoTheOneTag()
    {
        GameplayInputGate.Clear();
        var cards = Make("Status Cards").AddComponent<CardManager>();
        cards.Configure(null, null, null, System.Array.Empty<GearData>());
        cards.BuildDeck();
        cards.DealHand();
        var combat = Make("Status Combat").AddComponent<CombatManager>();
        combat.ConfigurePlayer(30);
        var enemyType = EnemyTypeData.Create("Status Enemy", 100, 3, 0);
        _objects.Add(enemyType);
        combat.StartEnemy(new EnemyRuntime(enemyType));
        var actions = Make("Status Actions").AddComponent<ActionButtonsUI>();
        actions.turnStateLabel = Label();
        actions.selectionLabel = new GameObject("SelectionLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        _objects.Add(actions.selectionLabel.gameObject);
        try
        {
            Refresh(actions);
            var tag = actions.StatusTag;
            Assert.That(tag, Is.Not.Null);
            Assert.That(tag.Line, Is.EqualTo("PLAYER TURN"));
            Assert.That(actions.selectionLabel.text, Is.Empty, "No selection, no preview.");

            typeof(CombatManager).GetMethod("BeginEnemyAttack", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(combat, null);
            Refresh(actions);
            Assert.That(tag.Line, Is.EqualTo("DEFENSE"));
        }
        finally { cards.CancelCardInteractions(); }
    }

    // A seeded hand of real CardViews, an enemy, and ActionButtonsUI with its status tag.
    (CardManager cards, CombatManager combat, ActionButtonsUI actions, EnemyRuntime enemy) Board(int enemyAttack)
    {
        GameplayInputGate.Clear();
        var canvas = Make("Board Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var hand = new GameObject("Board Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _objects.Add(hand);
        hand.transform.SetParent(canvas.transform, false);
        var field = hand.AddComponent<Field>();
        field.cardsHolder = (RectTransform)hand.transform;
        var drag = Make("Board Drag Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        drag.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefab = Make("Board View", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        var view = prefab.AddComponent<CardView>();
        view.face = prefab.GetComponent<Image>();
        view.canvasGroup = prefab.GetComponent<CanvasGroup>();
        var cards = Make("Board Cards").AddComponent<CardManager>();
        cards.Configure(field, drag, view, System.Array.Empty<GearData>());
        cards.ConfigureRandom(new DeterministicRandom(77));
        var combat = Make("Board Combat").AddComponent<CombatManager>();
        combat.ConfigurePlayer(30);
        combat.CriticalChancePercent = 0;
        cards.BuildDeck();
        cards.DealHand();
        var type = EnemyTypeData.Create("Board Target", 500, enemyAttack, 0);
        _objects.Add(type);
        var enemy = new EnemyRuntime(type);
        combat.StartEnemy(enemy);
        var actions = Make("Board Actions").AddComponent<ActionButtonsUI>();
        actions.turnStateLabel = Label();
        actions.selectionLabel = new GameObject("SelectionLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        _objects.Add(actions.selectionLabel.gameObject);
        return (cards, combat, actions, enemy);
    }

    static List<CardView> HandViews(CardManager cards) =>
        cards.handField.cardsHolder.GetComponentsInChildren<CardView>()
            .Where(view => view.data != null && cards.hand.Contains(view.data)).ToList();

    [Test]
    public void SelectionPreview_UsesTheExecutionDamageIncludingGearAndReachesTheTag()
    {
        var (cards, combat, actions, enemy) = Board(0);
        try
        {
            var gear = ScriptableObject.CreateInstance<GearData>();
            _objects.Add(gear);
            gear.id = gear.displayName = "status_preview_flat";
            gear.specialRule = GearSpecialRule.None;
            gear.effects = new[] { new GameplayEffectDefinition
            {
                kind = GameplayEffectKind.FlatActionDamage,
                trigger = GameplayEffectTrigger.ActionDamageCalculated,
                amount = 4,
                conditions = System.Array.Empty<GameplayEffectCondition>()
            } };
            cards.gearCatalog.Add(gear);
            Assert.That(cards.BuyGear(gear, 0), Is.True);
            var card = HandViews(cards).First(view => view.data.Rank != CardData.Rank.Ace);
            int faceOnly = combat.CalculateCardAttackDamage(card.data);

            cards.HandleCardClick(card);
            Refresh(actions);

            int expected = combat.PreviewActionTotalDamage(new[] { card.data }, enemy);
            Assert.That(expected, Is.EqualTo(faceOnly + 4), "The preview includes the Gear's flat action damage.");
            Assert.That(actions.StatusTag.Line, Is.EqualTo($"1 selected • Attack power {expected}"));
            Assert.That(actions.selectionLabel.text, Is.EqualTo(actions.StatusTag.Line), "One writer, one value.");

            int hp = enemy.currentHp;
            Assert.That(combat.TryPlayCards(new[] { card }, enemy), Is.True);
            Assert.That(hp - enemy.currentHp, Is.EqualTo(expected), "The preview equals the damage dealt.");
        }
        finally { cards.CancelCardInteractions(); }
    }

    [Test]
    public void BlockPreview_InDefenseReachesTheTag()
    {
        var (cards, combat, actions, _) = Board(6);
        try
        {
            typeof(CombatManager).GetMethod("BeginEnemyAttack", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(combat, null);
            Refresh(actions);
            Assert.That(actions.StatusTag.Line, Is.EqualTo("DEFENSE"));

            int blocked = 0, remaining = 0;
            var blocker = HandViews(cards).FirstOrDefault(view =>
                combat.TryPreviewDefense(new List<CardView> { view }, out blocked, out remaining));
            Assume.That(blocker, Is.Not.Null, "The seeded hand must contain a card that can block.");
            combat.TryPreviewDefense(new List<CardView> { blocker }, out blocked, out remaining);

            cards.HandleCardClick(blocker);
            Refresh(actions);

            Assert.That(actions.StatusTag.Line, Is.EqualTo($"Blocks {blocked} of {combat.PendingAttackCount} • {remaining} damage left"));
        }
        finally { cards.CancelCardInteractions(); }
    }

    static void Refresh(ActionButtonsUI actions) =>
        typeof(ActionButtonsUI).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(actions, null);

    BoardTone Tone(out Image board)
    {
        var canvas = Make("Tone Canvas", typeof(RectTransform), typeof(Canvas));
        board = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        board.transform.SetParent(canvas.transform, false);
        return BoardTone.Install(board);
    }

    [Test]
    public void BoardTone_DefenseToneIsBoardDefenseAndReduceMotionIsInstant()
    {
        var theme = DealbreakUIStyle.Theme;
        Assume.That(theme, Is.Not.Null, "The Cut Sheet theme is required.");
        Assert.That(BoardTone.ToneFor(true), Is.EqualTo(theme.boardDefense));
        Assert.That(BoardTone.ToneFor(false), Is.EqualTo(theme.ink));

        CutSheetMotion.ReduceMotionOverride = true;
        var tone = Tone(out var board);
        Assert.That(board.color, Is.EqualTo(theme.ink), "The board takes the theme's ink at install.");
        tone.Set(true, 10f);
        Assert.That(board.color, Is.EqualTo(theme.boardDefense), "Instant under Reduce Motion.");
        Assert.That(tone.DotScreen.color.a, Is.EqualTo(BoardTone.DotScreenAlpha));
        tone.Set(false, 11f);
        Assert.That(board.color, Is.EqualTo(theme.ink));
        Assert.That(tone.DotScreen.color.a, Is.EqualTo(0f));
    }

    [Test]
    public void DotScreen_IsASoftRedEdgeFadingFromTheTopToZeroByTheEnemyNameLine()
    {
        var tone = Tone(out var board);
        ((RectTransform)board.transform).sizeDelta = new Vector2(200f, 200f);
        var rect = tone.DotScreen.rectTransform;
        Assert.That(rect.anchorMax.y, Is.EqualTo(1f), "Strongest at the screen's top edge.");
        Assert.That(rect.anchorMin.y, Is.EqualTo(.62f).Within(.01f), "Gone by the enemies' name/HP line.");
        Assert.That(BoardTone.DotScreenAlpha, Is.EqualTo(.35f).Within(.001f));
        if (DealbreakUIStyle.Theme)
        {
            var brick = DealbreakUIStyle.Theme.brick;
            Assert.That(tone.DotScreen.color.r, Is.EqualTo(brick.r).Within(.001f), "Red halftone.");
        }
        Assert.That(tone.DotScreen.TryGetComponent(out ScreenPixelTile _), Is.True);
        Assert.That(tone.DotScreen.TryGetComponent(out VerticalAlphaGradient gradient), Is.True);
        Assert.That(gradient.bottomAlpha, Is.EqualTo(0f));

        var box = rect.rect;
        using var mesh = new VertexHelper();
        var white = new Color32(255, 255, 255, 255);
        mesh.AddVert(new Vector3(box.xMin, box.yMin), white, Vector2.zero);
        mesh.AddVert(new Vector3(box.xMin, box.yMax), white, Vector2.up);
        mesh.AddVert(new Vector3(box.xMax, box.yMax), white, Vector2.one);
        mesh.AddVert(new Vector3(box.xMax, box.yMin), white, Vector2.right);
        gradient.ModifyMesh(mesh);
        var vertex = new UIVertex();
        mesh.PopulateUIVertex(ref vertex, 0);
        Assert.That(vertex.color.a, Is.EqualTo(0), "No hard lower border: the bottom edge is transparent.");
        mesh.PopulateUIVertex(ref vertex, 1);
        Assert.That(vertex.color.a, Is.EqualTo(255), "Full strength at the top edge.");
    }

    [Test]
    public void BoardTone_CrossfadesOver250msAndTheDotScreenOverAtLeast300ms()
    {
        CutSheetMotion.ReduceMotionOverride = false;
        var tone = Tone(out var board);
        tone.Set(true, 0f);
        tone.Tick(.1f);
        Assert.That(board.color, Is.Not.EqualTo(BoardTone.ToneFor(true)), "Mid crossfade.");
        Assert.That(tone.DotScreen.color.a, Is.GreaterThan(0f).And.LessThan(BoardTone.DotScreenAlpha));
        tone.Tick(.25f);
        Assert.That(board.color, Is.EqualTo(BoardTone.ToneFor(true)));
        Assert.That(BoardTone.DotSeconds, Is.GreaterThanOrEqualTo(.3f));
        tone.Tick(.3f);
        Assert.That(tone.DotScreen.color.a, Is.EqualTo(BoardTone.DotScreenAlpha).Within(.0001f));
        float settled = tone.DotScreen.color.a;
        tone.Tick(5f);
        Assert.That(tone.DotScreen.color.a, Is.EqualTo(settled), "The dot screen never pulses.");
    }
}
