using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TutorialController : MonoBehaviour
{
    public List<TutorialStep> steps = new();

    [Header("UI")]
    [Tooltip("Full-screen RectTransform of the tutorial's own canvas (pivot in the middle) — everything below lives in it.")]
    public RectTransform root;
    public RectTransform window;
    public TMP_Text textLabel;
    public Button closeButton;
    [Tooltip("\"Don't show the tutorial again\".")]
    public Toggle dontShowToggle;
    public TutorialFinger finger;
    [Tooltip("Four dimmed full-screen strips around the target (top, bottom, left, right) that block every other tap.")]
    public RectTransform[] blockers = new RectTransform[4];
    [Tooltip("Room kept between the hint window and the target / the screen edges.")]
    public float windowMargin = 40f;
    [Tooltip("Extra room around the target left un-dimmed.")]
    public float targetPadding = 12f;

    [Header("Scene (found by themselves if empty)")]
    [Tooltip("Main scene: the player's ship, for Block Added.")]
    public ShipGrid playerShip;

    private TutorialStep current;
    private RectTransform currentTarget;
    private bool windowHidden;
    private bool pressStartedOnTarget;
    private float heldSeconds;
    private int blockCountAtStart;
    private bool battleStarted;
    private float nextTargetSearch;
    private Canvas rootCanvas;

    private static GameDataManager Data => GameDataManager.Instance;

    private static bool dismissedThisRun;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRun() => dismissedThisRun = false;

    private void Start()
    {
        rootCanvas = root != null ? root.GetComponentInParent<Canvas>().rootCanvas : null;

        if (playerShip == null)
        {
            var flow = FindAnyObjectByType<MainMenuFlowController>(FindObjectsInactive.Include);
            if (flow != null) playerShip = flow.playerShip;
        }

        var battle = FindAnyObjectByType<BattleSequenceController>();
        if (battle != null) battle.OnBattleStart += () => battleStarted = true;

        if (closeButton != null) closeButton.onClick.AddListener(OnClosePressed);
        if (dontShowToggle != null)
        {
            dontShowToggle.SetIsOnWithoutNotify(false);
            dontShowToggle.onValueChanged.AddListener(OnDontShowChanged);
        }

        HideAll();
    }

    private void OnClosePressed()
    {
        if (current != null && current.completeWhen == TutorialComplete.Closed && Data != null) Data.MarkTutorialStepDone(current.id);
        dismissedThisRun = true;
        HideAll();
    }

    private void OnDontShowChanged(bool off)
    {
        if (!off || Data == null) return;
        Data.SetTutorialOff(true);
        HideAll();
    }

    private void Update()
    {
        if (Data == null || Data.Current == null || Data.TutorialOff || dismissedThisRun)
        {
            HideAll();
            return;
        }

        var step = ResolveStep();
        if (step != current) BeginStep(step);
        if (current == null || !CanStart(current))
        {
            HideAll();
            return;
        }

        currentTarget = FindTarget(current);
        if (currentTarget != null && lastTarget != currentTarget) ScrollIntoView(currentTarget);
        if (currentTarget != null) lastTarget = currentTarget;
        TrackPointer();

        if (IsComplete(current))
        {
            Data.MarkTutorialStepDone(current.id);
            BeginStep(null);
            HideAll();
            return;
        }

        if (current.targetKind != TutorialTargetKind.None && currentTarget == null)
        {
            HideAll();
            return;
        }

        Layout();
    }

    private TutorialStep ResolveStep()
    {
        for (int start = 0; start < steps.Count;)
        {
            int end = start + 1;
            while (end < steps.Count && steps[end] != null && steps[end].startWhen == TutorialStart.Always) end++;

            var step = ResolveInSection(start, end);
            if (step != null) return step;
            start = end;
        }

        return null;
    }

    private TutorialStep ResolveInSection(int start, int end)
    {
        var gate = steps[start];
        if (gate == null) return null;
        if (!Data.IsTutorialStepDone(gate.id) && !CanStart(gate) && !CanSkip(gate)) return null;

        int index = FirstNotDone(start, end);

        while (index >= 0 && CanSkip(steps[index]))
        {
            Data.MarkTutorialStepDone(steps[index].id);
            index = FirstNotDone(start, end);
        }

        if (index < 0) return null;

        while (index >= start && steps[index].requireActive != null && !steps[index].requireActive.activeInHierarchy) index--;
        return index >= start && CanStart(steps[index]) ? steps[index] : null;
    }

    private int FirstNotDone(int start, int end)
    {
        for (int i = start; i < end; i++)
            if (steps[i] != null && !Data.IsTutorialStepDone(steps[i].id)) return i;
        return -1;
    }

    private bool CanSkip(TutorialStep step)
    {
        if (step.startWhen == TutorialStart.CanResearchTech && step.tech != null && Data.IsTechResearched(step.tech)) return true;
        return CanStart(step) && IsAlreadyDone(step);
    }

    private void BeginStep(TutorialStep step)
    {
        current = step;
        currentTarget = null;
        lastTarget = null;
        windowHidden = false;
        pressStartedOnTarget = false;
        heldSeconds = 0f;
        nextTargetSearch = 0f;
        blockCountAtStart = playerShip != null ? playerShip.Blocks.Count() : 0;
        if (textLabel != null && step != null) textLabel.text = step.text;
        if (dontShowToggle != null) dontShowToggle.SetIsOnWithoutNotify(false);
    }

    private bool IsAlreadyDone(TutorialStep step) => step.completeWhen switch
    {
        TutorialComplete.ObjectActive => step.watchObject != null && step.watchObject.activeInHierarchy,
        TutorialComplete.ObjectInactive => step.watchObject != null && !step.watchObject.activeInHierarchy,
        TutorialComplete.NoClaimableRewards => !Data.HasClaimableRewards(),
        TutorialComplete.TechResearched => step.tech != null && Data.IsTechResearched(step.tech),
        _ => false,
    };

    private bool CanStart(TutorialStep step) => step.startWhen switch
    {
        TutorialStart.HasClaimableRewards => Data.HasClaimableRewards(),
        TutorialStart.BattleStarted => battleStarted,
        TutorialStart.CanResearchTech => step.tech != null && Data.CanResearch(step.tech),
        _ => true,
    };

    private bool IsComplete(TutorialStep step)
    {
        return step.completeWhen switch
        {
            TutorialComplete.TargetTapped => tappedTarget,
            TutorialComplete.ObjectActive => step.watchObject != null && step.watchObject.activeInHierarchy,
            TutorialComplete.ObjectInactive => step.watchObject != null && !step.watchObject.activeInHierarchy,
            TutorialComplete.BlockAdded => playerShip != null && playerShip.Blocks.Count() > blockCountAtStart,
            TutorialComplete.TargetHeld => heldSeconds >= step.holdSeconds,
            TutorialComplete.NoClaimableRewards => !Data.HasClaimableRewards(),
            TutorialComplete.TechResearched => step.tech != null && Data.IsTechResearched(step.tech),
            _ => false,
        };
    }

    private RectTransform FindTarget(TutorialStep step)
    {
        if (step.targetKind == TutorialTargetKind.Object)
            return step.target != null && step.target.gameObject.activeInHierarchy ? step.target : null;

        if (currentTarget != null && currentTarget.gameObject.activeInHierarchy) return currentTarget;
        if (Time.unscaledTime < nextTargetSearch) return null;
        nextTargetSearch = Time.unscaledTime + 0.25f;

        switch (step.targetKind)
        {
            case TutorialTargetKind.PaletteBlock:
                foreach (var view in FindObjectsByType<BlockButtonView>())
                    if (view.Block != null && view.Block.id == step.blockId) return (RectTransform)view.transform;
                break;
            case TutorialTargetKind.LevelButton:
                foreach (var view in FindObjectsByType<LevelButtonView>())
                    if (view.level == step.level) return (RectTransform)view.transform;
                break;
            case TutorialTargetKind.TechNode:
                foreach (var node in FindObjectsByType<TechNodeView>())
                    if (node.tech == step.tech) return (RectTransform)(node.researchButton != null ? node.researchButton.transform : node.transform);
                break;
            case TutorialTargetKind.ClaimableReward:
                foreach (var task in FindObjectsByType<TaskItemView>())
                    if (task.claimButton != null && task.claimButton.interactable) return (RectTransform)task.claimButton.transform;
                foreach (var reward in FindObjectsByType<LevelRewardButtonView>())
                    if (reward.button != null && reward.button.interactable) return (RectTransform)reward.button.transform;
                break;
        }

        return null;
    }

    private bool tappedTarget;
    private RectTransform lastTarget;

    private void TrackPointer()
    {
        tappedTarget = false;
        if (!ReadPointer(out Vector2 position, out bool down, out bool pressedNow, out bool releasedNow)) return;

        bool onTarget = currentTarget != null && RectTransformUtility.RectangleContainsScreenPoint(currentTarget, position, CameraOf(currentTarget));
        bool targetGone = lastTarget != null && currentTarget == null;

        if (pressedNow && onTarget)
        {
            pressStartedOnTarget = true;
            windowHidden = true;
        }

        if (down && pressStartedOnTarget) heldSeconds += Time.unscaledDeltaTime;

        if (releasedNow)
        {
            tappedTarget = pressStartedOnTarget && (onTarget || targetGone);
            pressStartedOnTarget = false;
        }
    }

    private bool wasDown;
    private Vector2 lastPointer;

    private bool ReadPointer(out Vector2 position, out bool down, out bool pressedNow, out bool releasedNow)
    {
        down = false;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            lastPointer = Touchscreen.current.primaryTouch.position.ReadValue();
            down = true;
        }
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            lastPointer = Mouse.current.position.ReadValue();
            down = true;
        }

        position = lastPointer;
        pressedNow = down && !wasDown;
        releasedNow = !down && wasDown;
        wasDown = down;
        return Touchscreen.current != null || Mouse.current != null;
    }

    private void Layout()
    {
        if (root == null) return;
        Rect area = new(-root.rect.size * 0.5f, root.rect.size);

        bool hasTarget = currentTarget != null;
        Rect target = hasTarget ? TargetRectInRoot(currentTarget) : new Rect(area.center, Vector2.zero);

        if (window != null)
        {
            window.gameObject.SetActive(!windowHidden);
            if (!windowHidden)
            {
                Vector2 size = window.rect.size;
                float y = 0f;
                if (hasTarget)
                {
                    bool above = area.yMax - target.yMax >= target.yMin - area.yMin;
                    y = above ? area.yMax - windowMargin - size.y * 0.5f : area.yMin + windowMargin + size.y * 0.5f;
                }

                window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
                window.anchoredPosition = new Vector2(0f, y);
            }
        }

        if (finger != null)
        {
            if (!hasTarget || current.finger == TutorialFingerMotion.None) finger.Hide();
            else
            {
                Vector2 end = current.dragTo != null && current.dragTo.gameObject.activeInHierarchy ? TargetRectInRoot(current.dragTo).center : area.center;
                finger.Play(current.finger, target.center, end, Mathf.Min(target.width, target.height) * 0.35f);
            }
        }

        bool block = hasTarget && current.blockOthers;
        if (blockers != null && blockers.Length == 4)
        {
            Rect hole = new(target.xMin - targetPadding, target.yMin - targetPadding, target.width + targetPadding * 2f, target.height + targetPadding * 2f);
            SetBlocker(0, block, new Rect(area.xMin, hole.yMax, area.width, area.yMax - hole.yMax));
            SetBlocker(1, block, new Rect(area.xMin, area.yMin, area.width, hole.yMin - area.yMin));
            SetBlocker(2, block, new Rect(area.xMin, hole.yMin, hole.xMin - area.xMin, hole.height));
            SetBlocker(3, block, new Rect(hole.xMax, hole.yMin, area.xMax - hole.xMax, hole.height));
        }
    }

    private void SetBlocker(int index, bool show, Rect rect)
    {
        var blocker = blockers[index];
        if (blocker == null) return;
        blocker.gameObject.SetActive(show && rect.width > 0f && rect.height > 0f);
        if (!blocker.gameObject.activeSelf) return;

        blocker.anchorMin = blocker.anchorMax = new Vector2(0.5f, 0.5f);
        blocker.pivot = Vector2.zero;
        blocker.anchoredPosition = rect.position;
        blocker.sizeDelta = rect.size;
    }

    private Rect TargetRectInRoot(RectTransform target)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        var cam = CameraOf(target);
        var rootCam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;

        Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
        foreach (var corner in corners)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, corner);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, rootCam, out var local);
            local -= root.rect.center;
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static void ScrollIntoView(RectTransform target)
    {
        var scroll = target.GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.content == null) return;

        var view = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        Vector3 targetCenter = target.TransformPoint(target.rect.center);
        Vector3 viewCenter = view.TransformPoint(view.rect.center);
        Vector2 shift = scroll.content.parent.InverseTransformVector(viewCenter - targetCenter);

        var position = scroll.content.anchoredPosition;
        if (scroll.horizontal) position.x += shift.x;
        if (scroll.vertical) position.y += shift.y;
        scroll.content.anchoredPosition = position;
        scroll.velocity = Vector2.zero;
    }

    private static Camera CameraOf(RectTransform target)
    {
        var canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    private void HideAll()
    {
        if (window != null) window.gameObject.SetActive(false);
        if (finger != null) finger.Hide();
        if (blockers != null)
            foreach (var blocker in blockers)
                if (blocker != null) blocker.gameObject.SetActive(false);
    }
}
