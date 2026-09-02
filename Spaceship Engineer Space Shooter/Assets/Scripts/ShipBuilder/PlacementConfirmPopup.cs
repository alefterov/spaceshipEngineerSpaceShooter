using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Checkmark/cross popup shown at the drop point once a dragged block is released over a valid
/// cell (GhostBlockController.EndGridDrag). The player must explicitly confirm before the block
/// is actually placed — releasing over an invalid cell never shows this, the ghost is just
/// discarded instead.
///
/// SETUP: a small UI panel with two buttons (confirm/cancel), living under the same Canvas as
/// the rest of the builder UI, inactive by default. Assign panel/confirmButton/cancelButton/
/// canvasRect in the Inspector; leave uiCamera empty for a Screen Space - Overlay canvas.
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
    [Tooltip("The Canvas' RectTransform the popup lives under — needed to convert a screen-space drop point into the popup's local position.")]
    public RectTransform canvasRect;
    [Tooltip("Leave empty for a Screen Space - Overlay canvas. Required for Screen Space - Camera / World Space canvases.")]
    public Camera uiCamera;

    private Action onConfirm;
    private Action onCancel;

    private void Awake()
    {
        confirmButton.onClick.AddListener(HandleConfirm);
        cancelButton.onClick.AddListener(HandleCancel);
        Hide();
    }

    /// <summary>Shows the popup anchored at a screen-space point (e.g. the finger's last position).</summary>
    public void Show(Vector2 screenPosition, Action confirm, Action cancel)
    {
        onConfirm = confirm;
        onCancel = cancel;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out var local))
            panel.anchoredPosition = local;

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
