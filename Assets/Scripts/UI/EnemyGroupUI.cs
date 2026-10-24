using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the dynamic enemy presentation for the active encounter.
/// CombatManager remains authoritative; instantiated views bind to individual runtime enemies.
/// </summary>
public class EnemyGroupUI : MonoBehaviour
{
    [Header("Dynamic Enemy Area")]
    public RectTransform enemyContainer;
    public EnemyDisplayUI enemyViewPrefab;

    readonly List<EnemyDisplayUI> _activeViews = new();
    readonly HashSet<EnemyRuntime> _defeating = new();
    CombatManager _combat;
    RunManager _run;
    Vector2 _lastLayoutSize;
    int _lastLayoutCount = -1;

    public IReadOnlyList<EnemyDisplayUI> ActiveViews => _activeViews;
    public bool IsDefeatPresentationBusy => _defeating.Count > 0;
    public event System.Action OnDefeatPresentationCompleted;

    void OnEnable()
    {
        _combat = CombatManager.Instance;
        _run = RunManager.Instance;
        if (_combat != null)
        {
            _combat.OnEnemiesChanged += Refresh;
            _combat.OnEnemyChanged += Refresh;
            _combat.OnDamageResolved += HandleDamageResolved;
        }
        if (_run != null) _run.OnRunStarted += HandleRunStarted;
        Refresh();
    }

    void OnDisable()
    {
        if (_combat != null)
        {
            _combat.OnEnemiesChanged -= Refresh;
            _combat.OnEnemyChanged -= Refresh;
            _combat.OnDamageResolved -= HandleDamageResolved;
        }
        if (_run != null) _run.OnRunStarted -= HandleRunStarted;
        CancelDefeatPresentations(false);
        _combat = null;
        _run = null;
    }

    void HandleRunStarted(string _) => CancelDefeatPresentations();

    void CancelDefeatPresentations(bool refresh = true)
    {
        bool wasBusy = _defeating.Count > 0;
        StopAllCoroutines();
        foreach (var view in _activeViews)
            if (view != null) view.CancelDefeatCue();
        _defeating.Clear();
        if (refresh) Refresh();
        if (wasBusy) OnDefeatPresentationCompleted?.Invoke();
    }

    // Hit and defeat reactions wait until the attack card visibly lands on the enemy.
    void HandleDamageResolved(DamageResult result)
    {
        if (result.TargetAlreadyDefeated || !(result.Request.Target is EnemyRuntime enemy) ||
            _defeating.Contains(enemy)) return;
        for (int i = 0; i < _activeViews.Count; i++)
        {
            var view = _activeViews[i];
            if (view == null || !ReferenceEquals(view.DisplayedEnemy, enemy)) continue;
            var presentation = AttackCardPresentationUI.Instance;
            float delay = presentation != null ? presentation.ImpactDelay(result.Request.HitIndex) : 0f;
            view.PlayHitCue(result.ShieldConsumed || result.ActualHpLost <= 0, delay);
            if (!result.WasLethal) return;
            _defeating.Add(enemy);
            view.PlayDefeatCue(delay);
            StartCoroutine(RemoveDefeatedView(enemy, view));
            return;
        }
    }

    System.Collections.IEnumerator RemoveDefeatedView(EnemyRuntime enemy, EnemyDisplayUI view)
    {
        // Tie lane removal and the encounter-presentation barrier to the actual cue completion,
        // not a parallel timer that can finish a frame before the visual fade does.
        while (view != null && view.IsDefeatCuePlaying) yield return null;
        _defeating.Remove(enemy);
        if (view != null)
        {
            _activeViews.Remove(view);
            view.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(view.gameObject);
            else DestroyImmediate(view.gameObject);
        }
        Refresh();
        if (_defeating.Count == 0) OnDefeatPresentationCompleted?.Invoke();
    }

    public void Refresh()
    {
        var enemies = CombatManager.Instance?.Enemies;
        int enemyCount = enemies?.Count ?? 0;
        if (enemyContainer == null || enemyViewPrefab == null)
        {
            if (enemyCount > 0)
                Debug.LogError("EnemyGroupUI requires an Enemy Area and Enemy View prefab.", this);
            return;
        }

        var desired = new List<EnemyRuntime>(enemyCount + _defeating.Count);
        for (int i = 0; i < enemyCount; i++) desired.Add(enemies[i]);
        // Keep each fading view in its previous lane while survivors are laid out around it.
        // Appending defeated enemies to the desired list makes a killed middle/left target slide
        // to the rightmost slot as soon as RemoveEnemy raises OnEnemiesChanged.
        for (int i = 0; i < _activeViews.Count; i++)
        {
            var view = _activeViews[i];
            if (view != null && _defeating.Contains(view.DisplayedEnemy) &&
                !desired.Contains(view.DisplayedEnemy))
                desired.Insert(Mathf.Clamp(i, 0, desired.Count), view.DisplayedEnemy);
        }

        // Reconcile by runtime identity so simultaneous kills never animate the wrong recycled panel.
        var orderedViews = new List<EnemyDisplayUI>(desired.Count);
        var used = new HashSet<EnemyDisplayUI>();
        int entering = 0;
        for (int i = 0; i < desired.Count; i++)
        {
            EnemyDisplayUI view = null;
            for (int j = 0; j < _activeViews.Count; j++)
                if (!used.Contains(_activeViews[j]) && _activeViews[j] != null &&
                    ReferenceEquals(_activeViews[j].DisplayedEnemy, desired[i])) { view = _activeViews[j]; break; }
            if (view == null)
                for (int j = 0; j < _activeViews.Count; j++)
                    if (!used.Contains(_activeViews[j]) && _activeViews[j] != null && !_defeating.Contains(_activeViews[j].DisplayedEnemy)) { view = _activeViews[j]; break; }
            if (view == null) view = Instantiate(enemyViewPrefab, enemyContainer, false);
            used.Add(view);
            orderedViews.Add(view);
            view.gameObject.SetActive(true);
            if (!_defeating.Contains(desired[i]))
            {
                // Newly arriving enemies fade in left to right.
                bool arriving = !view.IsBoundTo(desired[i]);
                view.Bind(desired[i]);
                if (arriving) view.PlayEntrance(entering++ * 0.08f);
            }
            view.transform.SetSiblingIndex(i);
        }
        for (int i = 0; i < _activeViews.Count; i++)
        {
            if (used.Contains(_activeViews[i]) || _activeViews[i] == null) continue;
            if (Application.isPlaying) Destroy(_activeViews[i].gameObject);
            else DestroyImmediate(_activeViews[i].gameObject);
        }
        _activeViews.Clear();
        _activeViews.AddRange(orderedViews);
        FitViewsToContainer();
    }

    void LateUpdate()
    {
        if (enemyContainer == null) return;
        if (_lastLayoutCount != _activeViews.Count ||
            (_lastLayoutSize - enemyContainer.rect.size).sqrMagnitude > 0.25f)
            FitViewsToContainer();
    }

    void FitViewsToContainer()
    {
        if (enemyContainer == null || enemyViewPrefab == null || _activeViews.Count == 0) return;
        var prefabRect = enemyViewPrefab.transform as RectTransform;
        var prefabElement = enemyViewPrefab.GetComponent<LayoutElement>();
        float baseWidth = prefabElement != null && prefabElement.preferredWidth > 0f
            ? prefabElement.preferredWidth : prefabRect != null ? prefabRect.rect.width : 440f;
        float baseHeight = prefabElement != null && prefabElement.preferredHeight > 0f
            ? prefabElement.preferredHeight : prefabRect != null ? prefabRect.rect.height : 300f;
        var layout = enemyContainer.GetComponent<HorizontalLayoutGroup>();
        float spacing = layout != null ? layout.spacing : 0f;
        float padding = layout != null ? layout.padding.left + layout.padding.right : 0f;
        float available = Mathf.Max(1f, enemyContainer.rect.width - padding - spacing * (_activeViews.Count - 1));
        float width = Mathf.Min(baseWidth, available / _activeViews.Count);
        float scale = width / Mathf.Max(1f, baseWidth);
        float height = baseHeight * scale;
        float baseMinWidth = prefabElement != null ? Mathf.Max(0f, prefabElement.minWidth) : 0f;
        float baseMinHeight = prefabElement != null ? Mathf.Max(0f, prefabElement.minHeight) : 0f;

        for (int i = 0; i < _activeViews.Count; i++)
        {
            var view = _activeViews[i];
            if (view == null) continue;
            var rect = view.transform as RectTransform;
            if (rect != null)
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }
            var element = view.GetComponent<LayoutElement>();
            if (element != null)
            {
                element.minWidth = Mathf.Min(baseMinWidth, width);
                element.preferredWidth = width;
                element.minHeight = Mathf.Min(baseMinHeight, height);
                element.preferredHeight = height;
            }
        }
        _lastLayoutSize = enemyContainer.rect.size;
        _lastLayoutCount = _activeViews.Count;
    }

    void EnsureViewCount(int requiredCount)
    {
        while (_activeViews.Count < requiredCount)
        {
            var view = Instantiate(enemyViewPrefab, enemyContainer, false);
            view.name = $"EnemyView {_activeViews.Count + 1}";
            _activeViews.Add(view);
        }

        for (int i = _activeViews.Count - 1; i >= requiredCount; i--)
        {
            var view = _activeViews[i];
            _activeViews.RemoveAt(i);
            if (view == null) continue;
            view.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(view.gameObject);
            else
                DestroyImmediate(view.gameObject);
        }
    }
}
