using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DrawDeckStackUITests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    DrawDeckStackUI _stack;
    RectTransform _root;
    Button _button;
    TMP_Text _count;
    Canvas _canvas;
    Field _field;
    CardView _cardPrefab;
    CombatManager _combat;
    EnemyTypeData _enemyType;

    [SetUp]
    public void SetUp()
    {
        _cards = Create("Cards").AddComponent<CardManager>();
        _canvas = Create("Cards Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        var holderObject = Create("Card Holder", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Field));
        holderObject.transform.SetParent(_canvas.transform, false);
        var holder = holderObject.GetComponent<RectTransform>();
        holder.sizeDelta = new Vector2(900f, 240f);
        _field = holderObject.GetComponent<Field>();
        _field.cardsHolder = holder;
        var template = Create("Card View Template", typeof(RectTransform), typeof(Image), typeof(CardView));
        template.transform.SetParent(_canvas.transform, false);
        ((RectTransform)template.transform).anchoredPosition = new Vector2(5000f, 5000f);
        template.GetComponent<Image>().enabled = false;
        _cardPrefab = template.GetComponent<CardView>();
        _cards.Configure(_field, _canvas, _cardPrefab, System.Array.Empty<GearData>());
        var host = Create("Deck Stack", typeof(RectTransform));
        _root = (RectTransform)host.transform;
        _root.sizeDelta = new Vector2(90f, 120f);
        _button = host.AddComponent<Button>();
        _count = Create("Count", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TMP_Text>();
        _count.transform.SetParent(_root, false);
        _stack = host.AddComponent<DrawDeckStackUI>();
        _stack.stackRoot = _root;
        _stack.deckButton = _button;
        _stack.countLabel = _count;
        _stack.Refresh();
    }

    [TearDown]
    public void TearDown()
    {
        if (_combat) _combat.Reset();
        if (_cards) _cards.Reset();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [TestCase(0, 0)]
    [TestCase(1, 1)]
    [TestCase(9, 1)]
    [TestCase(10, 2)]
    [TestCase(19, 2)]
    [TestCase(20, 3)]
    [TestCase(29, 3)]
    [TestCase(30, 4)]
    [TestCase(40, 4)]
    public void DepthFollowsRemainingDeckCountBuckets(int remaining, int expectedDepth)
    {
        Assert.That(DrawDeckStackUI.GetDepthForCount(remaining), Is.EqualTo(expectedDepth));
    }

    [Test]
    public void BuildDrawAndReset_UpdateCountAndEmptyPresentationWithoutRecreatingLayers()
    {
        _cards.BuildDeck();
        Assert.That(_count.text, Is.EqualTo("40"));
        Assert.That(_stack.DisplayedCount, Is.EqualTo(40));
        Assert.That(_stack.DisplayedDepth, Is.EqualTo(4));
        Assert.That(ActiveLayers(), Is.EqualTo(4));
        int children = _root.childCount; // Four cards, the count label, its disc and shadow, the focus ring.
        Assert.That(LayerChildren(), Is.EqualTo(4));
        var front = _stack.Layer(0);
        Assert.That(front, Is.Not.Null);
        Assert.That(front.raycastTarget, Is.False);

        Assert.That(_cards.DrawToHand(8), Is.EqualTo(8));
        Assert.That(_count.text, Is.EqualTo("32"));
        Assert.That(_stack.DisplayedCount, Is.EqualTo(32));
        Assert.That(_root.childCount, Is.EqualTo(children));

        _cards.Reset();
        Assert.That(_count.text, Is.EqualTo("0"));
        Assert.That(_stack.DisplayedCount, Is.Zero);
        Assert.That(_stack.DisplayedDepth, Is.Zero);
        Assert.That(ActiveLayers(), Is.EqualTo(1));
        Assert.That(front.color.a, Is.LessThan(0.3f));
        Assert.That(_button.interactable, Is.True);

        _cards.BuildDeck();
        Assert.That(_count.text, Is.EqualTo("40"));
        Assert.That(ActiveLayers(), Is.EqualTo(4));
        Assert.That(_root.childCount, Is.EqualTo(children));
        Assert.That(front.color.a, Is.EqualTo(1f));
    }

    [Test]
    public void CardLayersDoNotInterceptClicksOrAddButtonListeners()
    {
        int clicks = 0;
        _button.onClick.AddListener(() => clicks++);
        _cards.BuildDeck();
        foreach (Transform child in _root)
        {
            var image = child.GetComponent<Image>();
            if (image != null) Assert.That(image.raycastTarget, Is.False);
        }
        _button.onClick.Invoke();
        Assert.That(clicks, Is.EqualTo(1));
        _cards.Reset();
        _button.onClick.Invoke();
        Assert.That(clicks, Is.EqualTo(2));
    }

    [Test]
    public void EncounterStartDraw_ImmediatelyRefreshesStackCount()
    {
        _cards.BuildDeck();
        Assert.That(_cards.DrawToHand(3), Is.EqualTo(3));
        Assert.That(_stack.DisplayedCount, Is.EqualTo(37));

        _combat = Create("Combat").AddComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
        _enemyType = EnemyTypeData.Create("Deck Refresh Enemy", 10, 1, 0);
        _created.Add(_enemyType);
        _combat.StartEnemy(new EnemyRuntime(_enemyType));

        Assert.That(_cards.HandCount, Is.EqualTo(4));
        Assert.That(_cards.deck.Count, Is.EqualTo(36));
        Assert.That(_stack.DisplayedCount, Is.EqualTo(36));
    }

    [Test]
    public void RecycleAndHeadlinerRewardMutations_ImmediatelyRefreshCount()
    {
        _cards.BuildDeck();
        Assert.That(_cards.DrawToHand(3), Is.EqualTo(3));
        Assert.That(_stack.DisplayedCount, Is.EqualTo(37));
        var hand = _field.cardsHolder.GetComponentsInChildren<CardView>();
        Assert.That(_cards.TryDiscardCards(new[] { hand[0], hand[1] }), Is.True);
        Assert.That(_stack.DisplayedCount, Is.EqualTo(37), "Discarding hand cards does not change the draw deck.");

        Assert.That(_cards.ReturnRandomDiscardToDeck(1), Is.EqualTo(1));
        Assert.That(_stack.DisplayedCount, Is.EqualTo(38));
        Assert.That(_stack.DisplayedDepth, Is.EqualTo(4));

        var sourceHeadlinerCard = CardData.Create(CardData.Suit.Spades, CardData.Rank.King);
        _created.Add(sourceHeadlinerCard);
        Assert.That(_cards.AddHeadlinerReward(sourceHeadlinerCard), Is.Not.Null);
        Assert.That(_stack.DisplayedCount, Is.EqualTo(39));

        _cards.Reset();
        Assert.That(_stack.DisplayedCount, Is.Zero);
        Assert.That(_stack.DisplayedDepth, Is.Zero);
        _cards.BuildDeck();
        Assert.That(_stack.DisplayedCount, Is.EqualTo(40));
    }

    [Test]
    public void StackedShadows_Are2_4_6pxInkBacksUnderTheTopCard()
    {
        // With no card-back art the pile keeps its legacy red fallback; give it a back so the
        // top card's untinted print is what's measured.
        var back = new Texture2D(4, 4);
        _stack.redCardBack = Sprite.Create(back, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f));
        _cards.BuildDeck();
        var theme = DealbreakUIStyle.Theme;
        Assume.That(theme, Is.Not.Null);
        Assert.That(_stack.Layer(0).color, Is.EqualTo(Color.white), "The top card prints its back untinted.");
        float[] expected = { 2f, 4f, 6f };
        for (int i = 1; i <= 3; i++)
        {
            var layer = _stack.Layer(i);
            Assert.That(layer.gameObject.activeSelf, Is.True);
            Assert.That(layer.color, Is.EqualTo(theme.inkPrint), $"Shadow {i} is the back tinted inkPrint.");
            Assert.That(layer.sprite, Is.EqualTo(_stack.Layer(0).sprite));
            var offset = layer.rectTransform.offsetMin - _stack.Layer(0).rectTransform.offsetMin;
            Assert.That(offset.x, Is.EqualTo(expected[i - 1]).Within(.01f));
            Assert.That(offset.y, Is.EqualTo(-expected[i - 1]).Within(.01f));
            Assert.That(layer.transform.GetSiblingIndex(), Is.LessThan(_stack.Layer(0).transform.GetSiblingIndex()), "Shadows draw under the top card.");
        }
    }

    [Test]
    public void CountDisc_IsPaperChipWithInkCourierCountOverA3pxShadow()
    {
        _cards.BuildDeck();
        var theme = DealbreakUIStyle.Theme;
        Assume.That(theme, Is.Not.Null);
        var disc = _stack.CountDisc;
        Assert.That(disc, Is.Not.Null);
        Assert.That(disc.sprite, Is.EqualTo(theme.chipSprite));
        Assert.That(disc.color, Is.EqualTo(theme.paper));
        Assert.That(disc.raycastTarget, Is.False);
        Assert.That(disc.transform.GetSiblingIndex(), Is.LessThan(_count.transform.GetSiblingIndex()), "The count prints over its disc.");
        Assert.That(_count.font, Is.EqualTo(theme.numericFont));
        Assert.That(_count.fontSize, Is.EqualTo(28f));
        Assert.That(_count.color, Is.EqualTo(theme.inkPrint));
        Assert.That(_stack.CountDiscShadow.Image.enabled, Is.True);
        Assert.That(_stack.DiscShadowOffset, Is.EqualTo(new Vector2(3f, -3f)));
        Assert.That(_stack.CountDiscShadow.transform.GetSiblingIndex(), Is.LessThan(disc.transform.GetSiblingIndex()));
    }

    [Test]
    public void HoverLiftsTheTopCard4px_PressDropsTheStack2pxAndCollapsesShadows()
    {
        _cards.BuildDeck();
        var top = _stack.Layer(0).rectTransform;
        var shadow = _stack.Layer(3).rectTransform;
        var restTop = top.offsetMin;
        var restShadow = shadow.offsetMin;

        _stack.SetHovered(true);
        Assert.That(top.offsetMin - restTop, Is.EqualTo(new Vector2(0f, 4f)), "Hover lifts the top card 4px.");
        Assert.That(shadow.offsetMin, Is.EqualTo(restShadow), "The shadows stay put on hover.");

        _stack.SetPressed(true);
        Assert.That(top.offsetMin - restTop, Is.EqualTo(new Vector2(0f, -2f)), "Pressed: the stack drops 2px.");
        for (int i = 1; i <= 3; i++)
        {
            var offset = _stack.Layer(i).rectTransform.offsetMin - top.offsetMin;
            Assert.That(offset.x, Is.EqualTo(2f).Within(.01f), $"Shadow {i} collapses to 2px.");
            Assert.That(offset.y, Is.EqualTo(-2f).Within(.01f));
        }

        _stack.SetPressed(false);
        _stack.SetHovered(false);
        Assert.That(top.offsetMin, Is.EqualTo(restTop));
        Assert.That(shadow.offsetMin, Is.EqualTo(restShadow));
    }

    [Test]
    public void CountLabel_StaysVisibleInkCourierAboveTheDisc_AfterAnotherWriterRestylesIt()
    {
        var theme = DealbreakUIStyle.Theme;
        Assume.That(theme, Is.Not.Null);
        _cards.BuildDeck();
        // As captured: the HUD text pass tints it paper (blank on the paper disc), swaps its face
        // material, and the label lands behind the disc.
        _count.color = theme.paper;
        if (theme.headingFont) _count.fontSharedMaterial = theme.headingFont.material;
        _count.transform.SetSiblingIndex(_stack.CountDisc.transform.GetSiblingIndex());

        _stack.Refresh();

        Assert.That(_count.text, Is.EqualTo("40"));
        Assert.That(_count.enabled && _count.gameObject.activeInHierarchy, Is.True);
        Assert.That(_count.color, Is.EqualTo(theme.inkPrint));
        Assert.That(_count.font, Is.EqualTo(theme.numericFont));
        Assert.That(_count.fontSharedMaterial, Is.EqualTo(_count.font.material));
        Assert.That(_count.transform.GetSiblingIndex(), Is.GreaterThan(_stack.CountDisc.transform.GetSiblingIndex()), "The number prints over its disc.");
    }

    [Test]
    public void LegacyBacking_StopsDrawingButKeepsRectAndRaycast()
    {
        var theme = DealbreakUIStyle.Theme;
        Assume.That(theme, Is.Not.Null);
        // As captured: the pile button carries the neo-punk frame (a charcoal rect) and an opaque root image.
        var root = _root.gameObject.AddComponent<Image>();
        root.color = theme.charcoal;
        _button.targetGraphic = root;
        var frame = DealbreakUIStyle.Frame(_root);
        Assume.That(frame, Is.Not.Null);
        root.color = theme.charcoal;
        Assert.That(_stack.BackingDraws, Is.True);
        var size = _root.sizeDelta;

        _stack.Refresh();

        Assert.That(_stack.BackingDraws, Is.False, "No charcoal backing behind the stack.");
        Assert.That(frame.gameObject.activeSelf, Is.False);
        Assert.That(root.color.a, Is.Zero);
        Assert.That(root.raycastTarget, Is.True, "The pile still takes clicks.");
        Assert.That(_root.sizeDelta, Is.EqualTo(size));
    }

    [Test]
    public void Focus_ShowsTheRing()
    {
        _cards.BuildDeck();
        Assert.That(_stack.FocusRingShown, Is.False);
        _stack.SetFocused(true);
        Assert.That(_stack.FocusRingShown, Is.True);
        _stack.OnDeselect(null);
        Assert.That(_stack.FocusRingShown, Is.False);
    }

    int LayerChildren()
    {
        int result = 0;
        foreach (Transform child in _root)
            if (child.name.StartsWith("Draw Deck Card")) result++;
        return result;
    }

    int ActiveLayers()
    {
        int result = 0;
        foreach (Transform child in _root)
            if (child.name.StartsWith("Draw Deck Card") && child.gameObject.activeSelf) result++;
        return result;
    }

    GameObject Create(string name, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        _created.Add(go);
        return go;
    }
}
