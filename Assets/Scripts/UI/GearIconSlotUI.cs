using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One glyph-only Gear slot. The rail supplies its detail action; a click opens it.</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "ArtifactIconSlotUI")]
public sealed class GearIconSlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] TMP_Text iconLabel;

    GearData _gear;
    Action<GearData> _select;
    long _instanceId;
    Sprite _occupiedSlotSprite;
    Sprite _emptySlotSprite;
    Sprite _selectedSlotSprite;
    bool _detailSelected;

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
            outline.effectColor = DealbreakUIStyle.Theme ? DealbreakUIStyle.Theme.ochre : new Color(.78f, .94f, .19f);
            outline.enabled = _detailSelected;
        }
        RefreshSlotBackground();
    }

    public void BindEmpty()
    {
        if (TryGetComponent<Selectable>(out var selectable)) selectable.interactable = false;
        _gear = null;
        _select = null;
        _instanceId = 0;
        _detailSelected = false;
        if (TryGetComponent<Outline>(out var outline)) outline.enabled = false;
        if (iconLabel) iconLabel.gameObject.SetActive(false);
        ItemIconUI.Show(transform, null);
        var image = GetComponent<Image>();
        if (image) image.raycastTarget = false;
        RefreshSlotBackground();
    }

    public void Bind(GearData gear, Action<GearData> select, long instanceId, bool detailSelected = false)
    {
        if (TryGetComponent<Selectable>(out var selectable)) selectable.interactable = true;
        if (!TryGetComponent<GearSlotDragUI>(out _)) gameObject.AddComponent<GearSlotDragUI>();
        _gear = gear;
        _select = select;
        _instanceId = instanceId;
        _detailSelected = detailSelected;
        var outline = GetComponent<Outline>();
        if (!outline) outline = gameObject.AddComponent<Outline>();
        outline.effectColor = DealbreakUIStyle.Theme ? DealbreakUIStyle.Theme.ochre : new Color(.78f, .94f, .19f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.enabled = _detailSelected;
        var image = GetComponent<Image>();
        if (image) image.raycastTarget = true;
        if (image) image.preserveAspect = true;
        RefreshSlotBackground();
        if (!iconLabel) iconLabel = GetComponentInChildren<TMP_Text>();
        bool hasAuthoredIcon = ItemIconUI.Show(transform, gear != null ? gear.iconSprite : null);
        if (iconLabel)
        {
            iconLabel.color = _detailSelected ? new Color(0.11f, 0.13f, 0.2f) : new Color(0.96f, 0.92f, 0.78f);
            iconLabel.text = gear != null && !string.IsNullOrWhiteSpace(gear.icon)
                ? gear.icon : "?";
            if(DealbreakUIStyle.Theme&&iconLabel.text.Length>2)iconLabel.text=iconLabel.text.Substring(0,2);
            if(DealbreakUIStyle.Theme){iconLabel.fontSize=21;iconLabel.textWrappingMode=TextWrappingModes.NoWrap;iconLabel.overflowMode=TextOverflowModes.Truncate;}
            iconLabel.gameObject.SetActive(!hasAuthoredIcon);
        }
    }

    void RefreshSlotBackground()
    {
        var image = GetComponent<Image>();
        if (image == null) return;
        if(DealbreakUIStyle.Theme)
        {
            var frame=DealbreakUIStyle.Frame(transform,false,true);
            // A dark square with a hairline; the item art carries the colour. Selected: a lime rule.
            frame.fill=DealbreakUIStyle.Theme.charcoal;
            frame.rim=_detailSelected?DealbreakUIStyle.Theme.ochre:DealbreakUIStyle.Theme.slate;
            frame.border=_detailSelected?2:1;
            frame.Refresh();return;
        }
        if (CombatDetailPanelUI.IsCombatActive)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = _detailSelected ? new Color(0.49f, 0.37f, 0.19f)
                : _gear == null ? new Color(0.09f, 0.13f, 0.16f) : new Color(0.19f, 0.25f, 0.29f);
            return;
        }
        var sprite = _gear == null ? _emptySlotSprite
            : _detailSelected && _selectedSlotSprite != null ? _selectedSlotSprite : _occupiedSlotSprite;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }
        else
            image.color = _gear == null ? new Color(0.12f, 0.16f, 0.22f, 0.55f)
                : _detailSelected ? new Color(0.2f, 0.45f, 0.62f, 0.95f)
                : new Color(0.16f, 0.20f, 0.27f, 0.78f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_gear && !GameplayInputGate.IsBlocked &&
            CardManager.Instance?.IsHandEnhancementTargeting != true && CardManager.Instance?.IsEnemyTargeting != true &&
            eventData.button == PointerEventData.InputButton.Left)
            _select?.Invoke(_gear);
    }
}
