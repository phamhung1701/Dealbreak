using TMPro;
using UnityEngine;
using UnityEngine.UI;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "ConsumableRewardUI")]
public sealed class ConsumableRewardUI : MonoBehaviour
{
    [Header("References")]
    public GameObject panel;
    public TMP_Text titleLabel;
    public TMP_Text detailsLabel;
    public Transform choicesContainer;
    public Transform replacementContainer;
    public GameObject itemButtonPrefab;
    public Button claimButton;
    public Button declineButton;

    int _selectedChoice = -1;
    string _shownContext;

    void OnEnable()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnConsumableRewardChanged += Refresh;
        if (CardManager.Instance != null)
            CardManager.Instance.OnConsumablesChanged += Refresh;
        if (claimButton) { claimButton.onClick.AddListener(ClaimSelected); KeyButtonBorderStyle.Apply(claimButton); }
        if (declineButton) declineButton.onClick.AddListener(Decline);
        Refresh();
    }

    void OnDisable()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnConsumableRewardChanged -= Refresh;
        if (CardManager.Instance != null)
            CardManager.Instance.OnConsumablesChanged -= Refresh;
        if (claimButton) claimButton.onClick.RemoveListener(ClaimSelected);
        if (declineButton) declineButton.onClick.RemoveListener(Decline);
        if (panel) panel.SetActive(false);
    }

    void Refresh()
    {
        var run = RunManager.Instance;
        var reward = run != null ? run.PendingConsumableReward : null;
        if (reward == null)
        {
            _shownContext = null;
            _selectedChoice = -1;
            if (panel) panel.SetActive(false);
            return;
        }
        if (_shownContext != reward.ContextId)
        {
            _shownContext = reward.ContextId;
            _selectedChoice = reward.Choices.Count == 1 ? 0 : -1;
        }
        if (panel) BoardPanelTransition.FadeIn(panel);
        if (titleLabel) titleLabel.text = reward.SourceLabel.ToUpperInvariant();
        RebuildChoices(reward);
        RebuildReplacements(reward);
        RefreshDetails(reward);
    }

    void RebuildChoices(ConsumableRewardOffer reward)
    {
        ClearChildren(choicesContainer);
        if (choicesContainer == null || itemButtonPrefab == null) return;
        for (int i = 0; i < reward.Choices.Count; i++)
        {
            int choiceIndex = i;
            var item = reward.Choices[i];
            var instance = Instantiate(itemButtonPrefab, choicesContainer);
            instance.name = $"RewardChoice_{i}_{item.id}";
            var label = instance.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = (_selectedChoice == i ? "SELECTED  " : string.Empty) +
                $"<size=125%><b>{item.displayName}</b></size>\n{item.description}";
            var detail=instance.GetComponent<ContextualInfoTrigger>()??instance.AddComponent<ContextualInfoTrigger>();
            detail.category="Trick reward";detail.title=item.displayName;detail.description=item.description;detail.allowWhileBlocked=true;detail.enabled=false;
            var surface=instance.GetComponent<EditorialSurface>();if(!surface)surface=instance.AddComponent<EditorialSurface>();surface.treatment=EditorialSurface.Treatment.Paper;
            var button = instance.GetComponent<Button>();
            if (button) button.onClick.AddListener(() => SelectChoice(choiceIndex));
        }
    }

    void RebuildReplacements(ConsumableRewardOffer reward)
    {
        ClearChildren(replacementContainer);
        var cards = CardManager.Instance;
        if (replacementContainer == null || itemButtonPrefab == null || cards == null) return;
        for (int i = 0; i < cards.BackpackSlotsUsed; i++)
        {
            int slotIndex = i;
            var current = cards.GetConsumableAtSlot(i);
            if (current == null) continue;
            int count = cards.GetConsumableStackCountAtSlot(i);
            var instance = Instantiate(itemButtonPrefab, replacementContainer);
            instance.name = $"ReplaceBackpackSlot_{i + 1}";
            var label = instance.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = $"REPLACE SLOT {i + 1}\n{current.icon} {current.displayName}" +
                (count > 1 ? $" ×{count}" : string.Empty);
            var button = instance.GetComponent<Button>();
            if (button)
            {
                button.interactable = _selectedChoice >= 0 && _selectedChoice < reward.Choices.Count;
                button.onClick.AddListener(() => ReplaceSelected(slotIndex));
            }
        }
    }

    void RefreshDetails(ConsumableRewardOffer reward)
    {
        var cards = CardManager.Instance;
        bool selected = _selectedChoice >= 0 && _selectedChoice < reward.Choices.Count;
        var item = selected ? reward.Choices[_selectedChoice] : null;
        if (detailsLabel)
        {
            if (!selected)
                detailsLabel.text = string.Empty;
            else if (cards != null && cards.CanAddConsumable(item))
                detailsLabel.text = $"{item.displayName}\n"+(ContextualTooltipUI.Instance?string.Empty:$"{item.description}\n")+"A compatible Trick slot is available.";
            else
                detailsLabel.text = $"{item.displayName}\n"+(ContextualTooltipUI.Instance?string.Empty:$"{item.description}\n")+"Trick slots full: explicitly replace a shown slot or decline.";
        }
        if (claimButton) claimButton.interactable = selected && cards != null && cards.CanAddConsumable(item);
        if (declineButton) declineButton.interactable = true;
    }

    void SelectChoice(int index)
    {
        var reward = RunManager.Instance?.PendingConsumableReward;
        if (reward == null || index < 0 || index >= reward.Choices.Count) return;
        _selectedChoice = index;
        Refresh();
    }

    void ClaimSelected()
    {
        if (RunManager.Instance != null && RunManager.Instance.ClaimPendingConsumableReward(_selectedChoice))
            Refresh();
    }

    void ReplaceSelected(int slotIndex)
    {
        if (RunManager.Instance != null &&
            RunManager.Instance.ReplacePendingConsumableReward(_selectedChoice, slotIndex))
            Refresh();
    }

    void Decline()
    {
        if (RunManager.Instance != null && RunManager.Instance.DeclinePendingConsumableReward())
            Refresh();
    }

    static void ClearChildren(Transform container)
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            var child = container.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }
}
