using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One glyph-only Gear slot. The rail supplies its tooltip and detail actions.</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "ArtifactIconSlotUI")]
public sealed class GearIconSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] TMP_Text iconLabel;
    Image _authoredIcon;

    GearData _gear;
    Action<GearData> _hover;
    Action<GearData> _leave;
    Action<GearData> _select;
    Action<long, GearData> _mergeSelect;
    long _instanceId;
    Sprite _occupiedSlotSprite;
    Sprite _emptySlotSprite;
    Sprite _selectedSlotSprite;
    bool _mergeSelected;
    bool _detailSelected;
    bool _mergeMode;
    bool _mergeEligible = true;

    public GearData Gear => _gear;
    public long InstanceId => _instanceId;
    public bool IsDetailSelected => _detailSelected;

    public void SetSlotSprites(Sprite occupied, Sprite empty, Sprite selected)
    {
        _occupiedSlotSprite = occupied;
        _emptySlotSprite = empty;
        _selectedSlotSprite = selected;
        RefreshSlotBackground();
    }

    public void SetDetailSelected(bool selected)
    {
        _detailSelected = selected && _gear != null;
        var outline = GetComponent<Outline>();
        if (outline)
        {
            outline.effectColor = _mergeSelected ? new Color(1f, 0.72f, 0.2f) :
                _detailSelected ? new Color(0.35f, 0.85f, 1f) : new Color(0.4f, 0.85f, 0.65f);
            outline.enabled = _detailSelected || _mergeMode && (_mergeEligible || _mergeSelected);
        }
        RefreshSlotBackground();
    }

    public void BindEmpty()
    {
        if (TryGetComponent<Selectable>(out var selectable)) selectable.interactable = false;
        _gear = null;
        _hover = null;
        _leave = null;
        _select = null;
        _mergeSelect = null;
        _instanceId = 0;
        _mergeSelected = false;
        _detailSelected = false;
        _mergeMode = false;
        _mergeEligible = true;
        if (TryGetComponent<CanvasGroup>(out var group)) group.alpha = 1f;
        if (TryGetComponent<Outline>(out var outline)) outline.enabled = false;
        if (iconLabel) iconLabel.gameObject.SetActive(false);
        if (_authoredIcon) _authoredIcon.gameObject.SetActive(false);
        var image = GetComponent<Image>();
        if (image) image.raycastTarget = false;
        RefreshSlotBackground();
    }

    public void Bind(GearData gear, Action<GearData> hover, Action<GearData> leave,
        Action<GearData> select, long instanceId, Action<long, GearData> mergeSelect, bool mergeSelected,
        bool mergeMode = false, bool mergeEligible = true, bool detailSelected = false)
    {
        if (TryGetComponent<Selectable>(out var selectable)) selectable.interactable = true;
        if (!TryGetComponent<GearSlotDragUI>(out _)) gameObject.AddComponent<GearSlotDragUI>();
        _gear = gear;
        _hover = hover;
        _leave = leave;
        _select = select;
        _instanceId = instanceId;
        _mergeSelect = mergeSelect;
        _mergeSelected = mergeSelected;
        _detailSelected = detailSelected;
        _mergeMode = mergeMode;
        _mergeEligible = mergeEligible;
        var group = GetComponent<CanvasGroup>();
        if (!group) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = mergeMode && !mergeEligible && !mergeSelected ? 0.4f : 1f;
        var outline = GetComponent<Outline>();
        if (!outline) outline = gameObject.AddComponent<Outline>();
        outline.effectColor = _mergeSelected ? new Color(1f, 0.72f, 0.2f) :
            _detailSelected ? new Color(0.35f, 0.85f, 1f) : new Color(0.4f, 0.85f, 0.65f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.enabled = _detailSelected || mergeMode && (mergeEligible || mergeSelected);
        var image = GetComponent<Image>();
        if (image) image.raycastTarget = true;
        if (image) image.preserveAspect = true;
        RefreshSlotBackground();
        if (!iconLabel) iconLabel = GetComponentInChildren<TMP_Text>();
        bool hasAuthoredIcon = gear != null && gear.iconSprite;
        if (hasAuthoredIcon)
        {
            EnsureAuthoredIcon();
            _authoredIcon.sprite = gear.iconSprite;
            _authoredIcon.gameObject.SetActive(true);
        }
        else if (_authoredIcon)
            _authoredIcon.gameObject.SetActive(false);
        if (iconLabel)
        {
            iconLabel.color = _mergeSelected||_detailSelected ? new Color(0.11f, 0.13f, 0.2f) : new Color(0.96f, 0.92f, 0.78f);
            iconLabel.text = gear != null && !string.IsNullOrWhiteSpace(gear.icon)
                ? gear.icon : "?";
            if(DealbreakUIStyle.Theme&&iconLabel.text.Length>2)iconLabel.text=iconLabel.text.Substring(0,2);
            if(DealbreakUIStyle.Theme){iconLabel.fontSize=21;iconLabel.textWrappingMode=TextWrappingModes.NoWrap;iconLabel.overflowMode=TextOverflowModes.Truncate;}
            iconLabel.gameObject.SetActive(!hasAuthoredIcon);
        }
    }

    void EnsureAuthoredIcon()
    {
        if (_authoredIcon) return;
        var iconObject = new GameObject("Authored Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)iconObject.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(0.08f, 0.08f);
        rect.anchorMax = new Vector2(0.92f, 0.92f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        _authoredIcon = iconObject.GetComponent<Image>();
        _authoredIcon.preserveAspect = true;
        _authoredIcon.raycastTarget = false;
    }

    void RefreshSlotBackground()
    {
        var image = GetComponent<Image>();
        if (image == null) return;
        if(DealbreakUIStyle.Theme)
        {
            var frame=DealbreakUIStyle.Frame(transform,false,true);
            frame.fill=_detailSelected||_mergeSelected?DealbreakUIStyle.Theme.ochre:_gear?DealbreakUIStyle.Theme.slate:DealbreakUIStyle.Theme.charcoal;
            frame.rim=_detailSelected||_mergeSelected?DealbreakUIStyle.Theme.paper:DealbreakUIStyle.Theme.slate;
            frame.Refresh();return;
        }
        if (CombatDetailPanelUI.IsCombatActive)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = _detailSelected || _mergeSelected ? new Color(0.49f, 0.37f, 0.19f)
                : _gear == null ? new Color(0.09f, 0.13f, 0.16f) : new Color(0.19f, 0.25f, 0.29f);
            return;
        }
        var sprite = _gear == null ? _emptySlotSprite
            : (_mergeSelected || _detailSelected) && _selectedSlotSprite != null ? _selectedSlotSprite : _occupiedSlotSprite;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }
        else
            image.color = _gear == null ? new Color(0.12f, 0.16f, 0.22f, 0.55f)
                : _mergeSelected ? new Color(0.25f, 0.58f, 0.34f, 0.95f)
                : _detailSelected ? new Color(0.2f, 0.45f, 0.62f, 0.95f)
                : new Color(0.16f, 0.20f, 0.27f, 0.78f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_gear) _hover?.Invoke(_gear);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_gear) _leave?.Invoke(_gear);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_gear && !GameplayInputGate.IsBlocked &&
            CardManager.Instance?.IsHandEnhancementTargeting != true && CardManager.Instance?.IsEnemyTargeting != true &&
            eventData.button == PointerEventData.InputButton.Left)
        {
            if (_mergeMode)
            {
                if (_mergeEligible || _mergeSelected) _mergeSelect?.Invoke(_instanceId, _gear);
            }
            else _select?.Invoke(_gear);
        }
    }

    void OnDisable()
    {
        if (_gear) _leave?.Invoke(_gear);
    }
}
