using TMPro;
using UnityEngine;

public class RunSeedUI : MonoBehaviour
{
    public TMP_Text seedDisplayLabel;

    void OnEnable()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnRunStarted += HandleRunStarted;
        Refresh();
    }

    void OnDisable()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnRunStarted -= HandleRunStarted;
    }

    void HandleRunStarted(string _) => Refresh();

    void Refresh()
    {
        if (seedDisplayLabel)
        {
            seedDisplayLabel.text = string.Empty;
            seedDisplayLabel.gameObject.SetActive(false);
        }
    }
}
