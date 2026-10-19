using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Applies the compact, non-spoiling label used by route-map nodes and reports hover.</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "PathNodeButtonUI")]
public sealed class PathNodeButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] TMP_Text label;

    PathNode _node;

    /// <summary>Raised with this node while the pointer is over it, and with null when it leaves.</summary>
    public Action<PathNode> HoverChanged;

    public void OnPointerEnter(PointerEventData eventData) => HoverChanged?.Invoke(_node);
    public void OnPointerExit(PointerEventData eventData) => HoverChanged?.Invoke(null);

    public void SetNode(PathNode node, RunManager run)
    {
        _node = node;
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (label == null) return;

        if (node == null || run == null)
        {
            label.text = "?";
            return;
        }

        if (!node.revealed && !node.completed)
        {
            label.text = "?";
            return;
        }

        // A Risk stays "?" until it is scouted; then it names its kind like other revealed nodes.
        bool scouted = run.IsScoutedNode(node);
        // Labels never break mid-word; multi-word labels carry explicit line breaks.
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.text = node.kind switch
        {
            MapNodeType.Combat => "COMBAT",
            MapNodeType.Elite => "ENFORCER",
            MapNodeType.Shop => "BLACK
MARKET",
            MapNodeType.Event => "INCIDENT",
            MapNodeType.Upgrade => "UPGRADE",
            MapNodeType.Risk => scouted ? "RISK" : "?",
            MapNodeType.Headliner => "HEADLINER",
            _ => "?"
        };
    }
}
