using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ConfirmationDialogUI : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text titleLabel;
    public TMP_Text bodyLabel;
    public Button confirmButton;
    public Button cancelButton;

    GameplayMenuCoordinator _coordinator;
    Action _confirmAction;
    Action _cancelAction;

    public bool IsOpen => panel != null && panel.activeSelf;

    void OnEnable()
    {
        if (confirmButton) { confirmButton.onClick.AddListener(Confirm); KeyButtonBorderStyle.Apply(confirmButton); }
        if (cancelButton) cancelButton.onClick.AddListener(Cancel);
        if (panel) panel.SetActive(false);
    }

    void OnDisable()
    {
        if (confirmButton) confirmButton.onClick.RemoveListener(Confirm);
        if (cancelButton) cancelButton.onClick.RemoveListener(Cancel);
        _confirmAction = null;
        _cancelAction = null;
    }

    public void Open(
        GameplayMenuCoordinator coordinator,
        string title,
        string body,
        string confirmLabel,
        Action onConfirm,
        Action onCancel = null)
    {
        if (coordinator == null || panel == null) return;
        _coordinator = coordinator;
        _confirmAction = onConfirm;
        _cancelAction = onCancel;
        if (titleLabel) titleLabel.text = title;
        if (bodyLabel) bodyLabel.text = body;
        if (confirmButton)
        {
            var label = confirmButton.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = confirmLabel;
        }
        coordinator.Push(this, panel, cancelButton);
    }

    public void Confirm()
    {
        var action = _confirmAction;
        Close(false);
        action?.Invoke();
    }

    public void Cancel() => Close(true);

    void Close(bool invokeCancel)
    {
        var cancel = _cancelAction;
        _confirmAction = null;
        _cancelAction = null;
        if (_coordinator != null && _coordinator.IsTop(this)) _coordinator.Pop(this);
        else if (panel) panel.SetActive(false);
        _coordinator = null;
        if (invokeCancel) cancel?.Invoke();
    }
}
