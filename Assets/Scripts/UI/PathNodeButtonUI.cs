using TMPro;
using UnityEngine;

/// <summary>Applies the compact, non-spoiling label used by route-map nodes.</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "PathNodeButtonUI")]
public sealed class PathNodeButtonUI : MonoBehaviour
{
    [SerializeField] TMP_Text label;

    public void SetNode(PathNode node, RunManager run)
    {
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
            MapNodeType.Shop => "BLACK\nMARKET",
            MapNodeType.Event => "INCIDENT",
            MapNodeType.Upgrade => "UPGRADE",
            MapNodeType.Risk => scouted ? "RISK" : "?",
            MapNodeType.Headliner => "HEADLINER",
            _ => "?"
        };
    }
}
