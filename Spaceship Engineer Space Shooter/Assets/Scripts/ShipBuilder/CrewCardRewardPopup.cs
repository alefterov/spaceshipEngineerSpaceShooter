using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CrewCardRewardPopup : MonoBehaviour
{
    [Header("Window")]
    [Tooltip("The window's root object. Hidden at start; it is shown when cards are received.")]
    public GameObject panel;
    [Tooltip("The area the cards are spread across (its width is what the row must fit in). A layout group on it is ignored.")]
    public RectTransform cardsContainer;
    [Tooltip("The card shown for each received member — the same prefab as the crew collection's cards.")]
    public CrewCardView cardPrefab;
    [Tooltip("Optional: closes the window at once (a button, or the dark background made a button).")]
    public Button closeButton;
    [Tooltip("How many times bigger than in the collection the cards are shown (made smaller only if they cannot fit even when squeezed).")]
    [Min(0.1f)] public float cardScale = 2.4f;

    [Header("Fan")]
    [Tooltip("How much each card covers the one before it while there is room (0.3 = 30% of a card's width).")]
    [Range(0f, 0.9f)] public float preferredOverlap = 0.3f;
    [Tooltip("The most each card may cover the one before it when squeezing the row to fit the width (0.9 = 90%).")]
    [Range(0f, 0.95f)] public float maxOverlap = 0.9f;
    [Tooltip("Turn of each card away from the middle, in degrees per card.")]
    [Min(0f)] public float anglePerCard = 4f;
    [Tooltip("The most the outermost card is turned, in degrees.")]
    [Min(0f)] public float maxAngle = 15f;
    [Tooltip("How much lower the outermost cards sit than the middle one (in the container's units) — the arc of the fan.")]
    [Min(0f)] public float arcDrop = 40f;
    [Tooltip("Room kept free at the left and right edges of the container.")]
    [Min(0f)] public float edgePadding = 20f;

    [Header("Motion")]
    [Tooltip("Pause between two cards starting to fly to their places (seconds).")]
    [Min(0f)] public float appearInterval = 0.08f;
    [Tooltip("How quickly the cards glide to their places (higher = faster).")]
    [Min(0.1f)] public float moveSharpness = 9f;
    [Tooltip("Size a card starts at, relative to its final size, when it appears in the middle.")]
    [Range(0.1f, 1f)] public float startScale = 0.7f;

    [Header("White flash")]
    [Tooltip("How long the white over a newly shown card takes to fade away (seconds).")]
    [Min(0.01f)] public float flashSeconds = 1f;
    [Tooltip("Opacity of the white from the start (left) to the end (right) of the fade. Default: drops quickly at first, then slows down.")]
    public AnimationCurve flashCurve = new(
        new Keyframe(0f, 1f, -3f, -3f),
        new Keyframe(1f, 0f, 0f, 0f));

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

    private bool HasAnimation => animator != null && animator.runtimeAnimatorController != null;

    private class ShownCard
    {
        public RectTransform slot;
        public CanvasGroup group;
        public Image flash;
        public float appearTime;
        public Vector2 targetPosition;
        public float targetAngle;
    }

    private Coroutine routine;
    private bool subscribed;
    private bool closing;
    private float closeTime;
    private readonly List<ShownCard> cards = new();

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

    public void Show(IReadOnlyList<CrewMemberData> members) => ShowCards(members, visibleSeconds);

    public void ShowCards(IReadOnlyList<CrewMemberData> members, float seconds)
    {
        if (panel == null || cardsContainer == null || cardPrefab == null || members == null || members.Count == 0) return;

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
            panel.SetActive(true);
        }

        float nextAppear = Time.unscaledTime;
        foreach (var shown in cards) nextAppear = Mathf.Max(nextAppear, shown.appearTime + appearInterval);

        foreach (var member in members)
        {
            var shown = CreateCard(member);
            if (shown == null) continue;

            shown.appearTime = nextAppear;
            nextAppear += appearInterval;
            cards.Add(shown);
        }

        Layout(snapNew: true);

        closeTime = Time.unscaledTime + seconds;

        if (!wasOpen)
        {
            if (HasAnimation && !string.IsNullOrEmpty(showTrigger))
            {
                animator.ResetTrigger(hideTrigger);
                animator.SetTrigger(showTrigger);
            }

            AudioManager.PlaySfx(appearSound, appearVolume);
        }

        if (routine == null) routine = StartCoroutine(AutoClose());
    }

    public void Close()
    {
        if (!IsOpen || closing) return;
        closeTime = 0f;
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

    private ShownCard CreateCard(CrewMemberData member)
    {
        var slotObject = new GameObject("CardSlot", typeof(RectTransform), typeof(CanvasGroup), typeof(LayoutElement)) { layer = cardsContainer.gameObject.layer };
        slotObject.GetComponent<LayoutElement>().ignoreLayout = true;
        var slot = (RectTransform)slotObject.transform;
        slot.SetParent(cardsContainer, false);
        slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0.5f, 0.5f);
        slot.anchoredPosition = Vector2.zero;

        var group = slotObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        var card = Instantiate(cardPrefab, slot);
        card.gameObject.SetActive(true);
        var cardRect = (RectTransform)card.transform;
        cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        card.draggable = false;
        card.Bind(member);

        return new ShownCard { slot = slot, group = group, flash = CreateFlash(cardRect) };
    }

    private static Image CreateFlash(RectTransform card)
    {
        var flashObject = new GameObject("WhiteFlash", typeof(RectTransform), typeof(Image), typeof(LayoutElement)) { layer = card.gameObject.layer };
        flashObject.GetComponent<LayoutElement>().ignoreLayout = true;
        var rect = (RectTransform)flashObject.transform;
        rect.SetParent(card, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.SetAsLastSibling();

        var flash = flashObject.GetComponent<Image>();
        if (card.TryGetComponent<Image>(out var background) && background.sprite != null)
        {
            flash.sprite = background.sprite;
            flash.type = background.type;
            flash.pixelsPerUnitMultiplier = background.pixelsPerUnitMultiplier;
            flash.preserveAspect = background.preserveAspect;
        }

        flash.color = Color.white;
        flash.raycastTarget = false;
        return flash;
    }

    private void Layout(bool snapNew)
    {
        int count = cards.Count;
        if (count == 0) return;

        Vector2 size = ((RectTransform)cardPrefab.transform).rect.size;
        Rect area = cardsContainer.rect;
        float width = Mathf.Max(1f, area.width - edgePadding * 2f);

        float rowUnits = 1f + (count - 1) * (1f - maxOverlap);
        float scale = Mathf.Min(cardScale, width / (size.x * rowUnits), area.height / Mathf.Max(1f, size.y));
        scale = Mathf.Max(0.05f, scale);

        float cardWidth = size.x * scale;
        float step = 0f;
        if (count > 1)
        {
            float preferredStep = cardWidth * (1f - preferredOverlap);
            float fitStep = (width - cardWidth) / (count - 1);
            float minStep = cardWidth * (1f - maxOverlap);
            step = Mathf.Max(minStep, Mathf.Min(preferredStep, fitStep));
        }

        float half = (count - 1) * 0.5f;
        float angleStep = half > 0f ? Mathf.Min(anglePerCard, maxAngle / half) : 0f;

        for (int i = 0; i < count; i++)
        {
            var shown = cards[i];
            float offset = i - half;
            float edge = half > 0f ? offset / half : 0f;

            shown.targetPosition = new Vector2(offset * step, -arcDrop * edge * edge);
            shown.targetAngle = -offset * angleStep;
            shown.slot.SetSiblingIndex(i);

            var card = shown.slot.childCount > 0 ? shown.slot.GetChild(0) : null;
            if (card != null) card.localScale = Vector3.one * scale;
            shown.slot.sizeDelta = size * scale;

            if (snapNew && Time.unscaledTime < shown.appearTime + 0.0001f && shown.group.alpha <= 0f)
            {
                shown.slot.anchoredPosition = Vector2.zero;
                shown.slot.localRotation = Quaternion.identity;
                shown.slot.localScale = Vector3.one * startScale;
            }
        }
    }

    private void Update()
    {
        if (cards.Count == 0) return;

        float now = Time.unscaledTime;
        float blend = 1f - Mathf.Exp(-moveSharpness * Time.unscaledDeltaTime);

        foreach (var shown in cards)
        {
            if (shown.slot == null) continue;
            if (now < shown.appearTime) continue;

            shown.group.alpha = 1f;
            shown.slot.anchoredPosition = Vector2.Lerp(shown.slot.anchoredPosition, shown.targetPosition, blend);
            shown.slot.localRotation = Quaternion.Slerp(shown.slot.localRotation, Quaternion.Euler(0f, 0f, shown.targetAngle), blend);
            shown.slot.localScale = Vector3.Lerp(shown.slot.localScale, Vector3.one, blend);

            if (shown.flash != null)
            {
                float t = (now - shown.appearTime) / flashSeconds;
                if (t >= 1f)
                {
                    Destroy(shown.flash.gameObject);
                    shown.flash = null;
                }
                else
                {
                    var color = shown.flash.color;
                    color.a = Mathf.Clamp01(flashCurve.Evaluate(t));
                    shown.flash.color = color;
                }
            }
        }
    }

    private void ClearCards()
    {
        foreach (var shown in cards)
            if (shown.slot != null) Destroy(shown.slot.gameObject);
        cards.Clear();
    }
}
