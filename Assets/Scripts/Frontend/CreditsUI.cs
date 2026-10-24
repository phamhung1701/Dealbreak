using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CreditsUI : MonoBehaviour
{
    public FrontendMenuNavigator navigator;
    public TMP_Text bodyLabel;
    public Button backButton;
    // Every bundled font (each ships its OFL licence) and the painted item and character art pack.
    public const string DefaultText =
        "Design & development • Built with Unity\n\nTypography • SIL Open Font License 1.1\nAnton · Barlow Condensed\nSpace Grotesk · IBM Plex Mono\n\n" +
        "Item and character art • Tricks & Gears painted pack,\nreprinted as halftone cut-outs\n\nInspired by card-based roguelikes\nand tabletop card combat.";
    [TextArea(6, 14)] public string creditsText = DefaultText;

    void OnEnable()
    {
        if (bodyLabel) bodyLabel.text = creditsText;
        if (backButton) backButton.onClick.AddListener(Back);
    }

    void OnDisable()
    {
        if (backButton) backButton.onClick.RemoveListener(Back);
    }

    public void Back() => navigator?.ShowHome();
}
