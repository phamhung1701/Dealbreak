using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "PathScreenUI")]
public class PathScreenUI : MonoBehaviour
{
    [Header("Layout")]
    public GameObject panel;
    public Transform mapContainer;
    public GameObject nodeButtonPrefab;
    public TMP_Text headerText;

    AttackCardPresentationUI _presentation;
    bool _pendingMapShow;
    bool _pendingRunCompleted;
    bool _mapToggleEnabled;
    Button _mapVisibilityButton;
    TMP_Text _mapVisibilityLabel;

    [Header("Map Settings")]
    [Min(0f)] public float horizontalInset = 20f;
    [Min(0f)] public float verticalInset = 30f;
    [Min(1f)] public float laneSpacing = 135f;

    readonly Dictionary<int, Vector2> _nodePositions = new();
    Vector2 _lastMapSize;
    DeckViewerUI _deckViewer;

    void LateUpdate()
    {
        if (!_deckViewer) _deckViewer = GetComponent<DeckViewerUI>();
        if (_mapVisibilityButton)
        {
            bool visible = _mapToggleEnabled && !GameplayInputGate.IsBlocked &&
                !(_deckViewer && _deckViewer.panel && _deckViewer.panel.activeInHierarchy);
            if (_mapVisibilityButton.gameObject.activeSelf != visible) SetMapVisibilityControl(visible);
        }
        if (panel != null && panel.activeInHierarchy && mapContainer is RectTransform bounds &&
            (bounds.rect.size - _lastMapSize).sqrMagnitude > 1f)
            BuildMap();
    }

    readonly struct RouteEdge
    {
        public readonly Vector2 from;
        public readonly Vector2 to;
        public readonly int fromNodeId;
        public readonly int toNodeId;
        public readonly int fromLane;
        public readonly int toLane;
        public bool IsDiagonal => fromLane != toLane;

        public RouteEdge(Vector2 from, Vector2 to, int fromNodeId, int toNodeId, int fromLane, int toLane)
        {
            this.from = from;
            this.to = to;
            this.fromNodeId = fromNodeId;
            this.toNodeId = toNodeId;
            this.fromLane = fromLane;
            this.toLane = toLane;
        }
    }

    void OnEnable()
    {
        EnsurePresentationSubscription();
        EnsureMapVisibilityControl();
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowPathScreen += Show;
            RunManager.Instance.OnHidePathScreen += Hide;
            RunManager.Instance.OnRunCompleted += HandleRunCompleted;
            RunManager.Instance.OnRunStarted += HandleRunStarted;
            RunManager.Instance.OnCycleStarted += HandleCycleStarted;
            RunManager.Instance.OnMapRevealChanged += BuildMap;
        }
    }

    void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnShowPathScreen -= Show;
            RunManager.Instance.OnHidePathScreen -= Hide;
            RunManager.Instance.OnRunCompleted -= HandleRunCompleted;
            RunManager.Instance.OnRunStarted -= HandleRunStarted;
            RunManager.Instance.OnCycleStarted -= HandleCycleStarted;
            RunManager.Instance.OnMapRevealChanged -= BuildMap;
        }
        UnsubscribeFromPresentation();
        _pendingMapShow = _pendingRunCompleted = false;
        _mapToggleEnabled = false;
        if (_mapVisibilityButton != null) _mapVisibilityButton.gameObject.SetActive(false);
    }

    void EnsurePresentationSubscription()
    {
        var current = AttackCardPresentationUI.Instance;
        if (_presentation == current) return;
        UnsubscribeFromPresentation();
        _presentation = current;
        if (_presentation != null)
        {
            _presentation.OnPresentationCompleted += HandlePresentationCompleted;
            _presentation.OnPresentationCancelled += HandlePresentationCancelled;
        }
    }

    void UnsubscribeFromPresentation()
    {
        if (_presentation != null)
        {
            _presentation.OnPresentationCompleted -= HandlePresentationCompleted;
            _presentation.OnPresentationCancelled -= HandlePresentationCancelled;
            _presentation = null;
        }
    }

    void OnDestroy()
    {
        if (_mapVisibilityButton == null) return;
        if (Application.isPlaying) Destroy(_mapVisibilityButton.gameObject);
        else DestroyImmediate(_mapVisibilityButton.gameObject);
        _mapVisibilityButton = null;
        _mapVisibilityLabel = null;
    }

    void HandlePresentationCompleted()
    {
        if (_pendingRunCompleted)
        {
            _pendingRunCompleted = false;
            ShowRunCompleted();
        }
        else if (_pendingMapShow)
        {
            // Let the cleared board breathe before the route slides in. Hide, a new run or a
            // cancelled presentation clear the pending flag and so cancel the beat.
            if (Application.isPlaying && isActiveAndEnabled) StartCoroutine(ShowAfterClearedBeat());
            else
            {
                _pendingMapShow = false;
                ShowImmediately();
            }
        }
    }

    const float ClearedBeatSeconds = 0.35f;

    System.Collections.IEnumerator ShowAfterClearedBeat()
    {
        yield return new WaitForSecondsRealtime(ClearedBeatSeconds);
        if (!_pendingMapShow) yield break;
        _pendingMapShow = false;
        ShowImmediately();
    }

    void HandlePresentationCancelled()
    {
        _pendingMapShow = _pendingRunCompleted = false;
        _mapToggleEnabled = false;
        BoardPanelTransition.Hide(panel);
        SetMapVisibilityControl(false);
    }

    void HandleRunStarted(string _)
    {
        _pendingMapShow = _pendingRunCompleted = false;
        _mapToggleEnabled = false;
        BoardPanelTransition.Hide(panel);
        SetMapVisibilityControl(false);
    }

    void HandleCycleStarted(string header)
    {
        var run = RunManager.Instance;
        if (headerText) headerText.text = header;
        BuildMap();
    }

    void HandleRunCompleted()
    {
        if (headerText) headerText.text = "VICTORY!";
        EnsurePresentationSubscription();
        if (_presentation != null && _presentation.IsBusy)
        {
            _pendingRunCompleted = true;
            _pendingMapShow = false;
            _mapToggleEnabled = false;
            BoardPanelTransition.Hide(panel);
            SetMapVisibilityControl(false);
            return;
        }
        ShowRunCompleted();
    }

    void ShowRunCompleted()
    {
        _mapToggleEnabled = false;
        if (headerText) headerText.text = "VICTORY!";
        BoardPanelTransition.Show(panel);
        SetMapVisibilityControl(false);
    }

    void Show()
    {
        EnsurePresentationSubscription();
        if (_presentation != null && _presentation.IsBusy)
        {
            _pendingMapShow = true;
            BoardPanelTransition.Hide(panel);
            return;
        }
        ShowImmediately();
    }

    void ShowImmediately()
    {
        _pendingMapShow = false;
        BuildMap();
        BoardPanelTransition.Show(panel);
        var run = RunManager.Instance;
        // A reward modal may temporarily own the input gate while the route is shown.
        // Keep route availability separate from whether a click is allowed this frame.
        _mapToggleEnabled = run != null && !run.IsRunCompleted && run.ActiveNode == null;
        SetMapVisibilityControl(_mapToggleEnabled);
    }

    void Hide()
    {
        _pendingMapShow = _pendingRunCompleted = false;
        _mapToggleEnabled = false;
        BoardPanelTransition.Hide(panel);
        SetMapVisibilityControl(false);
    }

    void BuildMap()
    {
        if (mapContainer is not RectTransform bounds || nodeButtonPrefab == null) return;
        _lastMapSize = bounds.rect.size;

        foreach (Transform child in mapContainer)
            Destroy(child.gameObject);
        _nodePositions.Clear();

        var run = RunManager.Instance;
        if (run == null || run.currentPath == null || run.currentPath.Count == 0) return;

        Vector2 nodeSize = new Vector2(Mathf.Clamp(bounds.rect.width / 12f, 62f, DealbreakUIStyle.Theme?72f:88f), 62f);
        float halfH = nodeSize.y * 0.5f;
        float usableWidth = Mathf.Max(0f, bounds.rect.width - nodeSize.x - 2f * horizontalInset);
        var displayColumns = run.currentPath.Select(node => node.col).Distinct().OrderBy(value => value).ToList();
        float xStep = usableWidth / displayColumns.Count;
        float laneGap = Mathf.Min(laneSpacing, Mathf.Max(0f, bounds.rect.height - 2f * (verticalInset + halfH)) * 0.5f);
        Vector2 center = bounds.rect.center;

        Vector2 PositionFor(int column, int row)
        {
            float x = center.x - usableWidth * 0.5f + column * xStep;
            float y = center.y + (1 - row) * laneGap;
            return new Vector2(x, y);
        }

        Vector2 startPosition = PositionFor(0, 1);
        Vector2 headlinerPosition = PositionFor(displayColumns.Count, 1);
        foreach (var node in run.currentPath)
            _nodePositions[node.id] = PositionFor(displayColumns.IndexOf(node.col) + 1,
                Mathf.Clamp(Mathf.RoundToInt(node.row), 0, RunMapGenerator.RowCount - 1));

        var routeEdges = new List<RouteEdge>();
        foreach (var node in run.currentPath)
        {
            foreach (int nextId in node.next)
            {
                var next = run.currentPath.Find(candidate => candidate.id == nextId);
                if (next == null || next.kind == MapNodeType.Headliner) continue;
                routeEdges.Add(ClipConnection(_nodePositions[node.id], _nodePositions[next.id], nodeSize,
                    node.id, next.id, Mathf.RoundToInt(node.row), Mathf.RoundToInt(next.row)));
            }
        }

        foreach (var node in run.currentPath.Where(value => Mathf.Approximately(value.col, 0f)))
            routeEdges.Add(ClipConnection(startPosition, _nodePositions[node.id], nodeSize,
                int.MinValue, node.id, 1, Mathf.RoundToInt(node.row)));
        var headlinerNode = run.currentPath.FirstOrDefault(value => value.kind == MapNodeType.Headliner);
        if (headlinerNode != null)
        {
            _nodePositions[headlinerNode.id] = headlinerPosition;
            foreach (var predecessor in run.currentPath.Where(value => value.next.Contains(headlinerNode.id)))
                routeEdges.Add(ClipConnection(_nodePositions[predecessor.id], headlinerPosition, nodeSize,
                    predecessor.id, headlinerNode.id, Mathf.RoundToInt(predecessor.row), 1));
        }

        // Keep same-lane trunks behind branches, then make genuine crossings read as overpasses.
        foreach (var edge in routeEdges.OrderBy(value => value.IsDiagonal ? 1 : 0))
            CreateConnection(edge, nodeSize);
        DrawCrossingBridges(routeEdges, bounds, nodeSize);

        CreateStartEndpoint(startPosition, nodeSize);
        foreach (var node in run.currentPath)
        {
            var buttonObject = Instantiate(nodeButtonPrefab, mapContainer);
            buttonObject.name = $"Node_{node.id}_{node.kind}";
            var nodeButtonUI = buttonObject.GetComponent<PathNodeButtonUI>();
            if (nodeButtonUI == null) nodeButtonUI = buttonObject.AddComponent<PathNodeButtonUI>();
            nodeButtonUI.SetNode(node, run);
            AttachNodeDetail(buttonObject, node, run);
            MapNodePresentation.Apply(buttonObject, node, run.IsScoutedNode(node));
            DealbreakUIStyle.ApplyHierarchy(buttonObject.transform);
            PlaceNode(buttonObject.GetComponent<RectTransform>(), _nodePositions[node.id], nodeSize);

            var button = buttonObject.GetComponent<Button>();
            if (button)
            {
                button.interactable = !node.completed && node.accessible;
                var image = buttonObject.GetComponent<Image>();
                if (image) image.color = GetNodeColor(node);
                var label = buttonObject.GetComponentInChildren<TMP_Text>();
                if (label) label.color = node.accessible && !node.completed
                    ? new Color(1f, 0.94f, 0.76f) : new Color(0.64f, 0.71f, 0.73f);
                buttonObject.transform.localScale = Vector3.one * (node.accessible && !node.completed ? 1.08f : 1f);
                int id = node.id;
                button.onClick.AddListener(() => run.OnPathChosen(id));
            }
            MapNodePresentation.Apply(buttonObject, node, run.IsScoutedNode(node));
        }
    }

    // Hovering a node opens the shared detail beside it: the enemies and totals of a fight, or the
    // Incident, once the node is revealed. Unknown nodes say only that.
    static void AttachNodeDetail(GameObject buttonObject, PathNode node, RunManager run)
    {
        if (!buttonObject.TryGetComponent<ContextualInfoTrigger>(out var detail))
            detail = buttonObject.AddComponent<ContextualInfoTrigger>();
        detail.describe = () =>
        {
            if (run == null || node == null) return null;
            if (!node.revealed && !node.completed) return new ContextualInfo("Route", "Unknown", "Not yet scouted.");
            string label = run.GetNodeLabel(node);
            string body = run.GetNodeDetail(node);
            if (body.StartsWith(label + " • ")) body = body.Substring(label.Length + 3);
            else if (body == label) body = string.Empty;
            return new ContextualInfo(run.IsScoutedNode(node) ? "Scouted" : "Route", label, body);
        };
    }

    void CreateStartEndpoint(Vector2 position, Vector2 nodeSize)
    {
        var endpoint = Instantiate(nodeButtonPrefab, mapContainer);
        endpoint.name = "RouteStart";
        var button = endpoint.GetComponent<Button>();
        if (button) button.interactable = false;
        var image = endpoint.GetComponent<Image>();
        if (image) image.color = new Color(0.28f, 0.34f, 0.44f);
        var text = endpoint.GetComponentInChildren<TMP_Text>();
        if (text) text.text = "START";
        var surface=endpoint.GetComponent<EditorialSurface>();if(!surface)surface=endpoint.AddComponent<EditorialSurface>();surface.treatment=EditorialSurface.Treatment.Open;
        DealbreakUIStyle.Frame(endpoint.transform,false,true);
        if(text){text.fontSize=18;text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Overflow;}
        PlaceNode(endpoint.GetComponent<RectTransform>(), position, nodeSize);
    }

    static void PlaceNode(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static RouteEdge ClipConnection(Vector2 from, Vector2 to, Vector2 nodeSize,
        int fromNodeId, int toNodeId, int fromLane, int toLane)
    {
        Vector2 delta = to - from;
        if (delta.sqrMagnitude < 0.001f)
            return new RouteEdge(from, to, fromNodeId, toNodeId, fromLane, toLane);
        Vector2 direction = delta.normalized;
        float sourceExtent = Mathf.Min(nodeSize.x * 0.5f / Mathf.Max(0.001f, Mathf.Abs(direction.x)),
            nodeSize.y * 0.5f / Mathf.Max(0.001f, Mathf.Abs(direction.y)));
        return new RouteEdge(from + direction * sourceExtent, to - direction * sourceExtent,
            fromNodeId, toNodeId, fromLane, toLane);
    }

    void CreateConnection(RouteEdge edge, Vector2 nodeSize)
    {
        CreateLineSegment(edge.from, edge.to, 3.5f,
            new Color(0.71f, 0.68f, 0.57f, 0.65f), "RouteConnection");
    }

    void DrawCrossingBridges(IReadOnlyList<RouteEdge> edges, RectTransform bounds, Vector2 nodeSize)
    {
        var cardImage = bounds.parent != null ? bounds.parent.GetComponent<Image>() : null;
        if (cardImage == null) return;

        var crossings = new List<Vector2>();
        for (int i = 0; i < edges.Count; i++)
        {
            for (int j = i + 1; j < edges.Count; j++)
            {
                var first = edges[i];
                var second = edges[j];
                if (first.fromNodeId == second.fromNodeId || first.fromNodeId == second.toNodeId ||
                    first.toNodeId == second.fromNodeId || first.toNodeId == second.toNodeId ||
                    !TryGetInteriorIntersection(first.from, first.to, second.from, second.to, out var crossing) ||
                    crossings.Any(existing => Vector2.SqrMagnitude(existing - crossing) < 1f))
                    continue;

                crossings.Add(crossing);
                RouteEdge overpass = SelectOverpass(first, second);
                var gapObject = new GameObject("RouteCrossingGap", typeof(RectTransform), typeof(Image));
                gapObject.layer = gameObject.layer;
                gapObject.transform.SetParent(mapContainer, false);
                var gapRect = gapObject.GetComponent<RectTransform>();
                gapRect.anchorMin = gapRect.anchorMax = new Vector2(0.5f, 0.5f);
                gapRect.anchoredPosition = crossing;
                float gapSize = nodeSize.x * 0.20f;
                gapRect.sizeDelta = new Vector2(gapSize, gapSize);
                var gapImage = gapObject.GetComponent<Image>();
                gapImage.sprite = cardImage.sprite;
                gapImage.type = cardImage.type;
                gapImage.pixelsPerUnitMultiplier = cardImage.pixelsPerUnitMultiplier;
                // With an unframed map, crossings mask against the tabletop rather than a panel.
                var boardImage = transform.Find("Background")?.GetComponent<Image>();
                gapImage.color = boardImage != null ? boardImage.color : cardImage.color;
                gapImage.raycastTarget = false;

                DrawBridgeArc(crossing, overpass, nodeSize);
            }
        }
    }

    static RouteEdge SelectOverpass(RouteEdge first, RouteEdge second)
    {
        int laneOrder = first.fromLane.CompareTo(second.fromLane);
        if (laneOrder != 0) return laneOrder < 0 ? first : second;
        int targetOrder = first.toLane.CompareTo(second.toLane);
        if (targetOrder != 0) return targetOrder < 0 ? first : second;
        return first.fromNodeId <= second.fromNodeId ? first : second;
    }

    void DrawBridgeArc(Vector2 crossing, RouteEdge edge, Vector2 nodeSize)
    {
        Vector2 direction = (edge.to - edge.from).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x);
        float halfSpan = nodeSize.x * 0.14f;
        Vector2 start = crossing - direction * halfSpan;
        Vector2 end = crossing + direction * halfSpan;
        Vector2 control = crossing + normal * (nodeSize.y * 0.28f);
        Vector2 previous = start;
        Color lineColor = new Color(0.43f, 0.57f, 0.59f, 0.65f);
        for (int i = 1; i <= 4; i++)
        {
            float t = i / 4f;
            float inverse = 1f - t;
            Vector2 point = inverse * inverse * start + 2f * inverse * t * control + t * t * end;
            CreateLineSegment(previous, point, 2.5f, lineColor, "RouteCrossingBridge");
            previous = point;
        }
    }

    void CreateLineSegment(Vector2 from, Vector2 to, float width, Color color, string objectName)
    {
        var connection = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        connection.layer = gameObject.layer;
        connection.transform.SetParent(mapContainer, false);
        var rect = connection.GetComponent<RectTransform>();
        Vector2 delta = to - from;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = (from + to) * 0.5f;
        rect.sizeDelta = new Vector2(delta.magnitude, width);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        var image = connection.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    static bool TryGetInteriorIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 intersection)
    {
        Vector2 ab = b - a;
        Vector2 cd = d - c;
        float denominator = Cross(ab, cd);
        if (Mathf.Abs(denominator) <= 0.001f)
        {
            intersection = default;
            return false;
        }

        Vector2 offset = c - a;
        float alongFirst = Cross(offset, cd) / denominator;
        float alongSecond = Cross(offset, ab) / denominator;
        if (alongFirst <= 0.08f || alongFirst >= 0.92f || alongSecond <= 0.08f || alongSecond >= 0.92f)
        {
            intersection = default;
            return false;
        }

        intersection = a + ab * alongFirst;
        return true;
    }

    static float Cross(Vector2 first, Vector2 second) => first.x * second.y - first.y * second.x;

    void EnsureMapVisibilityControl()
    {
        if (_mapVisibilityButton != null || panel == null || panel.transform.parent == null) return;

        var control = new GameObject("MapVisibilityButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Canvas), typeof(GraphicRaycaster));
        control.transform.SetParent(panel.transform.parent, false);
        control.transform.SetAsLastSibling();
        var rect = control.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -100f);
        rect.sizeDelta = new Vector2(150f, 42f);

        var image = control.GetComponent<Image>();
        image.color = new Color(0.12f, 0.17f, 0.19f, 0.3f);
        var controlCanvas = control.GetComponent<Canvas>();
        controlCanvas.overrideSorting = true;
        controlCanvas.sortingOrder = 45;
        _mapVisibilityButton = control.GetComponent<Button>();
        _mapVisibilityButton.targetGraphic = image;
        _mapVisibilityButton.onClick.AddListener(ToggleMapVisibility);

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(control.transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(5f, 2f);
        labelRect.offsetMax = new Vector2(-5f, -2f);
        _mapVisibilityLabel = labelObject.GetComponent<TextMeshProUGUI>();
        _mapVisibilityLabel.alignment = TextAlignmentOptions.Center;
        _mapVisibilityLabel.raycastTarget = false;
        _mapVisibilityLabel.fontSize = 18f;
        _mapVisibilityLabel.text = "CLOSE MAP";
        control.SetActive(false);
    }

    void SetMapVisibilityControl(bool visible)
    {
        if (_mapVisibilityButton == null) EnsureMapVisibilityControl();
        if (_mapVisibilityButton == null) return;
        _mapVisibilityButton.gameObject.SetActive(visible);
        if (_mapVisibilityLabel != null) _mapVisibilityLabel.text = panel != null && panel.activeSelf ? "CLOSE MAP" : "OPEN MAP";
    }

    bool CanToggleMap()
    {
        var run = RunManager.Instance;
        return run != null && !run.IsRunCompleted && run.ActiveNode == null && !GameplayInputGate.IsBlocked;
    }

    void ToggleMapVisibility()
    {
        if (panel == null || !_mapToggleEnabled || !CanToggleMap()) return;
        panel.SetActive(!panel.activeSelf);
        SetMapVisibilityControl(true);
    }

    static string GetNodeHeading(PathNode node, RunManager run)
    {
        string label = run.GetNodeLabel(node);
        return node.kind == MapNodeType.Combat || node.kind == MapNodeType.Elite
            ? label
            : $"{GetNodeSymbol(node.kind)}  {label}";
    }

    static string GetNodeSymbol(MapNodeType kind) => kind switch
    {
        MapNodeType.Combat => "FIGHT",
        MapNodeType.Elite => "ELITE",
        MapNodeType.Shop => "SHOP",
        MapNodeType.Event => "EVENT",
        MapNodeType.Upgrade => "UP",
        MapNodeType.Risk => "RISK",
        MapNodeType.Headliner => "BOSS",
        _ => "NODE"
    };

    static Color GetNodeColor(PathNode node)
    {
        if (node.completed) return new Color(0.19f, 0.32f, 0.27f, 0.6f);
        if (!node.accessible) return node.kind == MapNodeType.Elite
            ? new Color(0.32f, 0.15f, 0.13f, 0.65f) : new Color(0.16f, 0.23f, 0.25f, 0.65f);
        if (!node.revealed) return new Color(0.4f, 0.36f, 0.5f);

        return node.kind switch
        {
            MapNodeType.Elite => new Color(0.64f, 0.3f, 0.26f),
            MapNodeType.Shop => new Color(0.5f, 0.4f, 0.18f),
            MapNodeType.Event => new Color(0.28f, 0.45f, 0.58f),
            MapNodeType.Upgrade => new Color(0.28f, 0.56f, 0.42f),
            MapNodeType.Risk => new Color(0.54f, 0.28f, 0.48f),
            MapNodeType.Headliner => new Color(0.64f, 0.24f, 0.28f),
            _ => new Color(0.26f, 0.43f, 0.68f)
        };
    }

}
