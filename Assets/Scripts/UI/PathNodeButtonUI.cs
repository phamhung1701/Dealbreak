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

        // A scouted Incident/Risk names its kind; elsewhere "?" stays the Incident symbol.
        bool scouted = run.IsScoutedNode(node);
        // One-word labels shrink to fit the node instead of breaking mid-word.
        label.textWrappingMode = node.kind == MapNodeType.Headliner
            ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        label.text = node.kind switch
        {
            MapNodeType.Combat => "COMBAT",
            MapNodeType.Elite => "ELITE",
            MapNodeType.Shop => "SHOP",
            MapNodeType.Event => scouted ? "INCIDENT" : "?",
            MapNodeType.Upgrade => "UPGRADE",
            MapNodeType.Risk => scouted ? "RISK" : "?",
            MapNodeType.Headliner => run.GetNodeLabel(node).ToUpperInvariant(),
            _ => "?"
        };
    }
}
