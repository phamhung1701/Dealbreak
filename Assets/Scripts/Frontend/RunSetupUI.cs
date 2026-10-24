using TMPro;
using UnityEngine;
using UnityEngine.UI;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "RunSetupUI")]
public sealed class RunSetupUI : MonoBehaviour
{
    public FrontendMenuNavigator navigator;
    public TMP_InputField seedInput;
    public TMP_Text seedPreviewLabel;
    public Button randomSeedButton;
    public Button startButton;
    public Button backButton;
    public CharacterPickerUI characterPicker;

    void OnEnable()
    {
        if (randomSeedButton) randomSeedButton.onClick.AddListener(RandomizeSeed);
        if (startButton) startButton.onClick.AddListener(StartRun);
        if (backButton) backButton.onClick.AddListener(Back);
        if (seedInput) seedInput.onValueChanged.AddListener(HandleSeedChanged);
    }

    void OnDisable()
    {
        if (randomSeedButton) randomSeedButton.onClick.RemoveListener(RandomizeSeed);
        if (startButton) startButton.onClick.RemoveListener(StartRun);
        if (backButton) backButton.onClick.RemoveListener(Back);
        if (seedInput) seedInput.onValueChanged.RemoveListener(HandleSeedChanged);
    }

    public void Prepare()
    {
        if (seedInput) seedInput.text = string.Empty;
        if (characterPicker) characterPicker.Refresh();
        RefreshPreview();
    }

    public void RandomizeSeed()
    {
        string seed = RunSeedUtility.GenerateSeed();
        if (seedInput) seedInput.text = seed;
        RefreshPreview();
    }

    public void StartRun()
    {
        string seed = ResolveStartSeed(seedInput != null ? seedInput.text : string.Empty);
        GameFlowService.Instance?.StartRun(seed, characterPicker ? characterPicker.SelectedId : null);
    }

    public static string ResolveStartSeed(string input) => string.IsNullOrWhiteSpace(input)
        ? RunSeedUtility.GenerateSeed()
        : RunRandomContext.NormalizeSeed(input);

    public void Back() => navigator?.ShowHome();

    void HandleSeedChanged(string _) => RefreshPreview();

    void RefreshPreview()
    {
        if (seedPreviewLabel)
            seedPreviewLabel.text = string.IsNullOrWhiteSpace(seedInput != null ? seedInput.text : string.Empty)
                ? "Leave blank for a fresh random seed"
                : $"RUN SEED  •  {RunRandomContext.NormalizeSeed(seedInput.text)}";
    }
}
