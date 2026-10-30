using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The window that appears when the player receives crew cards as a reward (the "random crew card" reward of a level or a task): it
/// shows the card(s) that were received and goes away by itself after Visible Seconds (10 by default), or earlier when the player taps
/// the Close Button. If more cards arrive while it is open they are added and the time starts over.
///
/// THE ANIMATION IS YOURS: give the Panel an Animator. When the window opens the Show Trigger is set; Hide Trigger is set when it starts to
/// close, and the panel is switched off Hide Seconds later (the length of your hide animation; 0 = at once). Without an Animator it simply
/// appears and disappears.
///
/// SETUP: the Create Crew Card Popup menu builds the window. By hand: put this on an object that is always active (the Canvas, say), NOT
/// on the window itself; assign the Panel (the window's root, hidden at start), the Cards Container (the cards are put in it), the card
/// prefab (the crew collection's card) and optionally the Close Button.
/// </summary>
public class CrewCardRewardPopup : MonoBehaviour
{
    [Header("Window")]
    [Tooltip("The window's root object. Hidden at start; it is shown when cards are received.")]
    public GameObject panel;
    [Tooltip("The cards are created inside this object (a Horizontal Layout Group puts them in a row).")]
    public RectTransform cardsContainer;
    [Tooltip("The card shown for each received member — the same prefab as the crew collection's cards.")]
    public CrewCardView cardPrefab;
    [Tooltip("Optional: closes the window at once (a button, or the dark background made a button).")]
    public Button closeButton;
    [Tooltip("How many times bigger than in the collection the cards are shown. If the cards do not fit the container (several at once), " +
             "they are made smaller to fit.")]
    [Min(1f)] public float cardScale = 2.4f;

    [Header("Time")]
    [Tooltip("The window closes by itself this many seconds after the cards appeared.")]
    [Min(0.1f)] public float visibleSeconds = 10f;

    [Header("Animation (optional)")]
    public Animator animator;
    public string showTrigger = "Show";
    public string hideTrigger = "Hide";
    [Tooltip("How long your hide animation lasts; the window is switched off after this.")]
    [Min(0f)] public float hideSeconds = 0.3f;

    [Header("Sound")]
    [Tooltip("Played when the cards appear.")]
    public AudioClip appearSound;
    [Range(0f, 1f)] public float appearVolume = 1f;

    public bool IsOpen => panel != null && panel.activeSelf;

    // An Animator with no controller yet (before you add your animation) is simply ignored.
    private bool HasAnimation => animator != null && animator.runtimeAnimatorController != null;

    private Coroutine routine;
    private bool subscribed;
    private bool closing;
    private float closeTime;
    private readonly List<GameObject> cards = new();

    private void Start() => Subscribe();

    private void OnEnable() => Subscribe();

    private void OnDisable()
    {
        var data = GameDataManager.Instance;
        if (data != null && subscribed) data.OnCrewCardsGranted -= Show;
        subscribed = false;
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    private void Subscribe()
    {
        var data = GameDataManager.Instance;
        if (data == null || subscribed) return;

        data.OnCrewCardsGranted += Show;
        subscribed = true;
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    /// <summary>Shows the received cards (called when the player gets crew cards), for Visible Seconds.</summary>
    public void Show(IReadOnlyList<CrewMemberData> members) => ShowCards(members, visibleSeconds);

    /// <summary>Shows the cards for the given number of seconds instead of Visible Seconds (the test key uses this).</summary>
    public void ShowCards(IReadOnlyList<CrewMemberData> members, float seconds)
    {
        if (panel == null || cardsContainer == null || cardPrefab == null || members == null || members.Count == 0) return;

        // New cards while the window is fading away: start it over instead of letting the old close wipe the new cards.
        if (closing && routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        bool wasOpen = panel.activeSelf && !closing;
        if (!wasOpen)
        {
            ClearCards();
            closing = false;
        }

        foreach (var member in members)
        {
            // Each card sits in a slot of its own whose size is the scaled card, so the row lays the big cards out properly.
            var slot = new GameObject("CardSlot", typeof(RectTransform)) { layer = cardsContainer.gameObject.layer };
            var slotRect = (RectTransform)slot.transform;
            slotRect.SetParent(cardsContainer, false);

            var card = Instantiate(cardPrefab, slotRect);
            card.gameObject.SetActive(true);
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;

            card.draggable = false;     // these are for looking at; the cards are in the collection already
            card.Bind(member);
            cards.Add(slot);
        }

        ScaleCards();

        closeTime = Time.unscaledTime + seconds;

        if (!wasOpen)
        {
            panel.SetActive(true);
            if (HasAnimation && !string.IsNullOrEmpty(showTrigger))
            {
                animator.ResetTrigger(hideTrigger);
                animator.SetTrigger(showTrigger);
            }

            AudioManager.PlaySfx(appearSound, appearVolume);
        }

        if (routine == null) routine = StartCoroutine(AutoClose());
    }

    /// <summary>Closes the window now (with the hide animation).</summary>
    public void Close()
    {
        if (!IsOpen || closing) return;
        closeTime = 0f; // the timer loop sees this and starts closing
    }

    private IEnumerator AutoClose()
    {
        while (Time.unscaledTime < closeTime) yield return null;

        closing = true;
        if (HasAnimation && !string.IsNullOrEmpty(hideTrigger)) animator.SetTrigger(hideTrigger);
        if (hideSeconds > 0f) yield return new WaitForSecondsRealtime(hideSeconds);

        if (panel != null) panel.SetActive(false);
        ClearCards();
        closing = false;
        routine = null;
    }

    /// <summary>Makes the cards Card Scale times bigger, or smaller if that many would not fit the container.</summary>
    private void ScaleCards()
    {
        if (cards.Count == 0) return;

        Vector2 size = ((RectTransform)cardPrefab.transform).rect.size;
        float spacing = cardsContainer.TryGetComponent<HorizontalLayoutGroup>(out var row) ? row.spacing : 0f;

        float fitWidth = (cardsContainer.rect.width - spacing * (cards.Count - 1)) / (size.x * cards.Count);
        float fitHeight = cardsContainer.rect.height / size.y;
        float scale = Mathf.Max(0.1f, Mathf.Min(cardScale, fitWidth, fitHeight));

        foreach (var slot in cards)
        {
            if (slot == null) continue;
            ((RectTransform)slot.transform).sizeDelta = size * scale;
            if (slot.transform.childCount > 0) slot.transform.GetChild(0).localScale = Vector3.one * scale;
        }
    }

    private void ClearCards()
    {
        foreach (var card in cards)
            if (card != null) Destroy(card);
        cards.Clear();
    }
}
