using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Names an enemy ability on that enemy's card when it takes effect: a short ochre tag that pops
/// over the portrait. Several triggers play one after another. Presentation only; it waits for the
/// committed card to land, like the hit reactions.
/// </summary>
[RequireComponent(typeof(EnemyDisplayUI))]
public sealed class EnemyAbilityCalloutUI : MonoBehaviour
{
    const float ShowTime = 1.05f;
    const float PopTime = .12f;

    EnemyDisplayUI _view;
    EnemyRuntime _subscribed;
    RectTransform _tag;
    TMP_Text _label;
    readonly Queue<string> _queued = new();
    Coroutine _playing;

    public static void Attach(EnemyDisplayUI view)
    {
        if (view && !view.TryGetComponent(out EnemyAbilityCalloutUI _))
            view.gameObject.AddComponent<EnemyAbilityCalloutUI>();
    }

    void Awake() => _view = GetComponent<EnemyDisplayUI>();

    void LateUpdate()
    {
        var enemy = _view ? _view.DisplayedEnemy : null;
        if (ReferenceEquals(enemy, _subscribed)) return;
        Unsubscribe();
        _subscribed = enemy;
        if (_subscribed != null) _subscribed.OnAbilityTriggered += HandleAbilityTriggered;
    }

    void OnDisable()
    {
        Unsubscribe();
        _queued.Clear();
        _playing = null;
        if (_tag) _tag.gameObject.SetActive(false);
    }

    void Unsubscribe()
    {
        if (_subscribed != null) _subscribed.OnAbilityTriggered -= HandleAbilityTriggered;
        _subscribed = null;
    }

    void HandleAbilityTriggered(EnemyAbility ability)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || string.IsNullOrEmpty(ability.displayName)) return;
        // One tag per ability in a burst: a two-hit Guarded check shows once, not twice.
        if (_queued.Contains(ability.displayName)) return;
        _queued.Enqueue(ability.displayName);
        if (_playing == null)
        {
            var presentation = AttackCardPresentationUI.Instance;
            _playing = StartCoroutine(Play(presentation != null ? presentation.ImpactDelay(0) : 0f));
        }
    }

    IEnumerator Play(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        EnsureTag();
        while (_queued.Count > 0)
        {
            _label.text = _queued.Peek().ToUpperInvariant();
            _tag.gameObject.SetActive(true);
            float elapsed = 0f;
            while (elapsed < ShowTime)
            {
                elapsed += Time.deltaTime;
                float pop = elapsed < PopTime ? Mathf.Lerp(1.35f, 1f, elapsed / PopTime) : 1f;
                _tag.localScale = Vector3.one * pop;
                float fade = Mathf.Clamp01((ShowTime - elapsed) / .25f);
                _label.alpha = fade;
                _tag.GetComponent<Image>().color = WithAlpha(_tag.GetComponent<Image>().color, fade);
                yield return null;
            }
            _queued.Dequeue();
        }
        _tag.gameObject.SetActive(false);
        _playing = null;
    }

    void EnsureTag()
    {
        if (_tag) return;
        var theme = DealbreakUIStyle.Theme;
        var tag = new GameObject("Ability Callout", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _tag = (RectTransform)tag.transform;
        _tag.SetParent(transform, false);
        _tag.anchorMin = new Vector2(.08f, .62f);
        _tag.anchorMax = new Vector2(.92f, .74f);
        _tag.offsetMin = _tag.offsetMax = Vector2.zero;
        var background = tag.GetComponent<Image>();
        background.raycastTarget = false;
        background.color = theme ? theme.ochre : new Color(.85f, .62f, .25f, 1f);
        var text = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var textRect = (RectTransform)text.transform;
        textRect.SetParent(_tag, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(6f, 2f);
        textRect.offsetMax = new Vector2(-6f, -2f);
        _label = text.GetComponent<TMP_Text>();
        if (theme)
        {
            DealbreakUIStyle.Text(_label, theme.headingFont, 26, theme.inkPrint);
            _label.raycastTarget = false;
        }
        _label.alignment = TextAlignmentOptions.Center;
        _label.textWrappingMode = TextWrappingModes.NoWrap;
        _label.overflowMode = TextOverflowModes.Ellipsis;
        tag.SetActive(false);
    }

    static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
}
