using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DeckViewerUITests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    DeckViewerUI _viewer;
    RectTransform _content;
    TMP_Text _deckText;
    Button _toggleButton;

    [SetUp]
    public void SetUp()
    {
        var managerObject = Create("Deck Viewer CardManager");
        _cards = managerObject.AddComponent<CardManager>();

        var canvasObject = Create("Deck Viewer Canvas", typeof(RectTransform), typeof(Canvas));
        var viewportObject = Create("Deck Viewer Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(canvasObject.transform, false);
        var viewport = viewportObject.GetComponent<RectTransform>();
        viewport.sizeDelta = new Vector2(700f, 300f);
        var scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;

        var contentObject = Create("DrawDeckContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        _content = contentObject.GetComponent<RectTransform>();
        _content.sizeDelta = new Vector2(700f, 300f);
        scroll.content = _content;
        _deckText = Create("DrawPileText", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        _deckText.transform.SetParent(_content, false);

        var panel = Create("Deck Viewer Panel");
        var viewerObject = Create("Deck Viewer");
        viewerObject.SetActive(false);
        _viewer = viewerObject.AddComponent<DeckViewerUI>();
        _viewer.panel = panel;
        _viewer.deckText = _deckText;
        _viewer.cardPrefab = CreateCardPrefab(canvasObject.transform);
        _viewer.cardsPerRow = 5;
        _viewer.cardScale = 0.5f;
        var toggleObject = Create("Deck Viewer Toggle", typeof(RectTransform), typeof(Image), typeof(Button));
        _toggleButton = toggleObject.GetComponent<Button>();
        _viewer.toggleButton = _toggleButton;
        _cards.Configure(null, null, _viewer.cardPrefab, System.Array.Empty<GearData>());
        viewerObject.SetActive(true);
        // EditMode does not consistently run MonoBehaviour lifecycle callbacks; reset then enable once.
        InvokeViewer("OnDisable");
        InvokeViewer("OnEnable");
    }

    [TearDown]
    public void TearDown()
    {
        if (_viewer != null) InvokeViewer("OnDisable");
        if (_cards != null) _cards.Reset();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void Refresh_RendersExactlyTheAuthoritativeDeckInstancesAndCount()
    {
        _cards.BuildDeck();
        OpenViewer();
        var snapshots = Snapshots();

        Assert.That(_deckText.text, Does.Contain("40"));
        Assert.That(snapshots, Has.Length.EqualTo(_cards.deck.Count));
        CollectionAssert.AreEquivalent(_cards.deck, snapshots.Select(view => view.data));
        Assert.That(snapshots.Select(view => view.data.Id).Distinct().Count(), Is.EqualTo(snapshots.Length));
    }

    [Test]
    public void Snapshots_AreNoninteractiveAndDoNotJoinHandSelection()
    {
        _cards.BuildDeck();
        OpenViewer();
        var snapshots = Snapshots();

        Assert.That(snapshots, Is.Not.Empty);
        foreach (var snapshot in snapshots)
        {
            Assert.That(snapshot.enabled, Is.False);
            Assert.That(snapshot.homeField, Is.Null);
            Assert.That(snapshot.canvasGroup == null || !snapshot.canvasGroup.interactable, Is.True);
            Assert.That(snapshot.canvasGroup == null || !snapshot.canvasGroup.blocksRaycasts, Is.True);
            Assert.That(snapshot.GetComponentsInChildren<Canvas>(true).All(canvas => canvas.enabled), Is.True,
                "Visual snapshot canvases remain enabled so card artwork is rendered.");
            Assert.That(snapshot.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
            Assert.That(snapshot.GetComponentsInChildren<GraphicRaycaster>(true).All(raycaster => !raycaster.enabled), Is.True,
                "Snapshot cards must not have an active nested pointer raycaster.");
        }
        Assert.That(_cards.SelectedCards, Is.Empty);
    }

    [Test]
    public void Snapshot_LeavesNestedArtworkCanvasEnabledButNonInteractive()
    {
        var visual = new GameObject("VisualRoot", typeof(RectTransform), typeof(Canvas),
            typeof(Image), typeof(GraphicRaycaster));
        _created.Add(visual);
        visual.transform.SetParent(_viewer.cardPrefab.transform, false);
        var nestedCanvas = visual.GetComponent<Canvas>();
        nestedCanvas.overrideSorting = true;
        _viewer.cardPrefab.visualRoot = visual.transform as RectTransform;
        var definition = CardData.Create(CardData.Suit.Hearts, CardData.Rank.Ace);
        _created.Add(definition);
        var card = new CardInstance(definition, 999);

        typeof(DeckViewerUI).GetField("_content", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(_viewer, _content);
        var rebuild = typeof(DeckViewerUI).GetMethod("RebuildSnapshots",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        rebuild.Invoke(_viewer, new object[] { new[] { card } });
        Assert.That(_content.GetComponentsInChildren<CardView>(true).Any(view => view.name.StartsWith("DeckCardSnapshot_")),
            Is.True, "The viewer should create a snapshot for the supplied runtime card.");
        var snapshot = Snapshots().Single();
        var snapshotCanvas = snapshot.visualRoot.GetComponent<Canvas>();

        Assert.That(snapshotCanvas, Is.Not.Null);
        Assert.That(snapshotCanvas.overrideSorting, Is.False,
            "Snapshot canvases must inherit the modal canvas sorting order.");
        Assert.That(snapshot.visualRoot.GetComponent<GraphicRaycaster>().enabled, Is.False);
        Assert.That(snapshot.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), Is.True);
    }

    [Test]
    public void DrawAndReset_RefreshSnapshotListAndRestoreStartingDeckCount()
    {
        _cards.BuildDeck();
        OpenViewer();
        Assert.That(_cards.DrawToHand(3), Is.EqualTo(3));
        Assert.That(_deckText.text, Does.Contain("37"));
        Assert.That(Snapshots(), Has.Length.EqualTo(37));
        Assert.That(_cards.SelectedCards, Is.Empty);

        _cards.Reset();
        Assert.That(_deckText.text, Does.Contain("0"));
        Assert.That(Snapshots(), Is.Empty);
        _cards.BuildDeck();
        OpenViewer();
        Assert.That(_deckText.text, Does.Contain("40"));
        Assert.That(Snapshots(), Has.Length.EqualTo(40));
    }

    [Test]
    public void ClosedViewer_UpdatesCountWithoutCreatingSnapshotsThenRebuildsWhenOpened()
    {
        _cards.BuildDeck();
        Assert.That(Snapshots(), Is.Empty, "Closed viewer should not maintain hidden snapshot instances.");
        Assert.That(_deckText.text, Does.Contain("40"));

        OpenViewer();
        Assert.That(Snapshots(), Has.Length.EqualTo(40));
        _toggleButton.onClick.Invoke();
        Assert.That(Snapshots(), Is.Empty);

        Assert.That(_cards.DrawToHand(1), Is.EqualTo(1));
        Assert.That(_deckText.text, Does.Contain("39"));
        Assert.That(Snapshots(), Is.Empty);
        OpenViewer();
        Assert.That(Snapshots(), Has.Length.EqualTo(39));
    }

    [Test]
    public void Layout_UsesMultipleRowsAndReadableCardScale()
    {
        _cards.BuildDeck();
        OpenViewer();
        var snapshots = Snapshots();
        var firstRowY = ((RectTransform)snapshots[0].transform).anchoredPosition.y;
        var secondRowY = ((RectTransform)snapshots[_viewer.cardsPerRow].transform).anchoredPosition.y;

        Assert.That(secondRowY, Is.LessThan(firstRowY));
        Assert.That(((RectTransform)snapshots[0].transform).localScale.x, Is.EqualTo(0.5f).Within(0.01f));
        float left = ((RectTransform)snapshots[0].transform).anchoredPosition.x - 68f;
        float right = ((RectTransform)snapshots[_viewer.cardsPerRow - 1].transform).anchoredPosition.x + 68f;
        Assert.That(left, Is.GreaterThanOrEqualTo(-_content.rect.width * 0.5f));
        Assert.That(right, Is.LessThanOrEqualTo(_content.rect.width * 0.5f));
        Assert.That(_content.rect.height, Is.GreaterThan(300f));
    }

    void InvokeViewer(string method) => typeof(DeckViewerUI)
        .GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        .Invoke(_viewer, null);

    void OpenViewer()
    {
        if (_viewer.panel != null && _viewer.panel.activeSelf) return;
        _toggleButton.onClick.Invoke();
    }

    CardView[] Snapshots() => _content.GetComponentsInChildren<CardView>(true)
        .Where(view => view.name.StartsWith("DeckCardSnapshot_"))
        .ToArray();

    CardView CreateCardPrefab(Transform parent)
    {
        var cardObject = Create("Card Prefab", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(CardView));
        cardObject.transform.SetParent(parent, false);
        var view = cardObject.GetComponent<CardView>();
        view.face = cardObject.GetComponent<Image>();
        view.canvasGroup = cardObject.GetComponent<CanvasGroup>();
        return view;
    }

    GameObject Create(string name, params System.Type[] components)
    {
        var gameObject = new GameObject(name, components);
        _created.Add(gameObject);
        return gameObject;
    }
}
