using System.Collections;
using TMPro;
using UnityEngine;

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
