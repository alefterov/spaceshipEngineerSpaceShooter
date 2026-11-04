using System;
using UnityEngine;
using UnityEngine.UI;

public class PlacementConfirmPopup : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The popup's own root — toggled active/inactive. Usually the same object this script is on.")]
    public RectTransform panel;
    [Tooltip("Checkmark button — confirms the placement.")]
    public Button confirmButton;
    [Tooltip("Cross button — discards the ghost without placing it.")]
    public Button cancelButton;

    private Action onConfirm;
    private Action onCancel;

    private void Awake()
    {
        confirmButton.onClick.AddListener(HandleConfirm);
        cancelButton.onClick.AddListener(HandleCancel);
        Hide();
    }

    public void Show(Action confirm, Action cancel)
    {
        onConfirm = confirm;
        onCancel = cancel;

        panel.gameObject.SetActive(true);
    }

    public void Hide()
    {
        onConfirm = null;
        onCancel = null;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    private void HandleConfirm()
    {
        var callback = onConfirm;
        Hide();
        callback?.Invoke();
    }

    private void HandleCancel()
    {
        var callback = onCancel;
        Hide();
        callback?.Invoke();
    }
}
