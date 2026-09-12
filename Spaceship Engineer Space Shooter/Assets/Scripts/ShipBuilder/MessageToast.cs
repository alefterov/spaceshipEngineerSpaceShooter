using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Generic short-lived text message — shows a string, auto-hides after Display Duration seconds.
/// Reusable for any validation/error feedback (e.g. "cockpit required to save"), not tied to one
/// specific case — call Show(string) from wherever a message needs to reach the player.
/// </summary>
public class MessageToast : MonoBehaviour
{
    public TMP_Text label;
    public float displayDuration = 2f;

    private Coroutine hideRoutine;

    private void Awake()
    {
        if (label != null) label.gameObject.SetActive(false);
    }

    public void Show(string message)
    {
        if (label == null) return;

        label.text = message;
        label.gameObject.SetActive(true);

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        label.gameObject.SetActive(false);
        hideRoutine = null;
    }
}
