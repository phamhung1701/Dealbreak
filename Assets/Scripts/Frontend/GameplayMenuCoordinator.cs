using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "GameplayMenuCoordinator")]
public sealed class GameplayMenuCoordinator : MonoBehaviour
{
    sealed class Layer
    {
        public object owner;
        public GameObject panel;
        public Selectable defaultSelection;
        public GameObject previousSelection;
        public GameplayInputBlockReason reason;
    }

    public CanvasGroup gameplayCanvasGroup;
    public GraphicRaycaster dragCanvasRaycaster;

    readonly List<Layer> _layers = new();
    float _previousTimeScale = 1f;
    GameplayInputBlockReason _appliedReasons;

    public int LayerCount => _layers.Count;
    public bool HasBlockingMenu => _layers.Count > 0;
    public object TopOwner => _layers.Count > 0 ? _layers[^1].owner : null;

    public bool Contains(object owner) => _layers.Exists(layer => ReferenceEquals(layer.owner, owner));
    public bool IsTop(object owner) => _layers.Count > 0 && ReferenceEquals(_layers[^1].owner, owner);

    public bool Push(
        object owner,
        GameObject panel,
        Selectable defaultSelection,
        GameplayInputBlockReason reason = GameplayInputBlockReason.FrontendMenu)
    {
        if (owner == null || panel == null || Contains(owner)) return false;

        if (_layers.Count == 0)
            BeginBlocking();

        var layer = new Layer
        {
            owner = owner,
            panel = panel,
            defaultSelection = defaultSelection,
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null,
            reason = reason
        };
        _layers.Add(layer);
        BoardPanelTransition.FadeIn(panel);
        panel.transform.SetAsLastSibling();
        ApplyGateReasons();
        Select(defaultSelection);
        return true;
    }

    public bool Pop(object owner)
    {
        if (!IsTop(owner)) return false;

        var layer = _layers[^1];
        _layers.RemoveAt(_layers.Count - 1);
        if (layer.panel != null) layer.panel.SetActive(false);
        ApplyGateReasons();

        if (_layers.Count == 0)
        {
            EndBlocking(layer.previousSelection);
        }
        else
        {
            var next = _layers[^1];
            Select(next.defaultSelection, next.previousSelection);
        }
        return true;
    }

    public void ClearAll()
    {
        GameObject restoreSelection = _layers.Count > 0 ? _layers[0].previousSelection : null;
        foreach (var layer in _layers)
            if (layer.panel != null) layer.panel.SetActive(false);
        _layers.Clear();
        ApplyGateReasons();
        EndBlocking(restoreSelection);
    }

    void BeginBlocking()
    {
        _previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (gameplayCanvasGroup != null)
        {
            gameplayCanvasGroup.interactable = false;
            gameplayCanvasGroup.blocksRaycasts = false;
        }
        if (dragCanvasRaycaster != null) dragCanvasRaycaster.enabled = false;
        CardManager.Instance?.CancelCardInteractions();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    void EndBlocking(GameObject restoreSelection)
    {
        Time.timeScale = _previousTimeScale <= 0f ? 1f : _previousTimeScale;
        if (gameplayCanvasGroup != null)
        {
            gameplayCanvasGroup.interactable = true;
            gameplayCanvasGroup.blocksRaycasts = true;
        }
        if (dragCanvasRaycaster != null) dragCanvasRaycaster.enabled = true;
        Select(null, restoreSelection);
    }

    // Only release reasons this coordinator applied; gameplay-owned reasons (pending
    // rewards, Gear choices) must survive a menu opening and closing above them.
    void ApplyGateReasons()
    {
        var desired = GameplayInputBlockReason.None;
        foreach (var layer in _layers)
            desired |= layer.reason;
        var released = _appliedReasons & ~desired;
        if (released != GameplayInputBlockReason.None) GameplayInputGate.Set(released, false);
        if (desired != GameplayInputBlockReason.None) GameplayInputGate.Set(desired, true);
        _appliedReasons = desired;
    }

    void Select(Selectable preferred, GameObject fallback = null)
    {
        if (!isActiveAndEnabled) return;
        StartCoroutine(SelectNextFrame(preferred, fallback));
    }

    static bool CanSelect(GameObject target)
    {
        if (target == null || !target.activeInHierarchy) return false;
        var selectable = target.GetComponent<Selectable>();
        return selectable == null || selectable.IsActive() && selectable.IsInteractable();
    }

    static IEnumerator SelectNextFrame(Selectable preferred, GameObject fallback)
    {
        yield return null;
        if (EventSystem.current == null) yield break;
        GameObject target = preferred != null && preferred.IsActive() && preferred.IsInteractable()
            ? preferred.gameObject
            : CanSelect(fallback) ? fallback : null;
        EventSystem.current.SetSelectedGameObject(target);
    }

    void OnDisable()
    {
        if (_layers.Count > 0) ClearAll();
    }
}
