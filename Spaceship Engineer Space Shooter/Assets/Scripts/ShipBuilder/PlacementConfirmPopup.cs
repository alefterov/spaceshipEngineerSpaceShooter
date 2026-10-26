using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Checkmark/cross popup shown once a dragged block is released over a valid cell
/// (GhostBlockController.EndGridDrag), and for a delete confirmation. The player must explicitly
/// confirm before the block is actually placed — releasing over an invalid cell never shows this,
/// the ghost is just discarded instead. The panel stays exactly where it is in the scene: it is
/// switched on, and switched off again after either button.
///
/// SETUP: a small UI panel with two buttons (confirm/cancel), placed where you want it on the
/// builder UI, inactive by default. Assign panel/confirmButton/cancelButton in the Inspector.
/// </summary>
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

    /// <summary>Shows the popup where it sits in the scene — it does not follow the drop point. It hides again after
    /// either button.</summary>
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
