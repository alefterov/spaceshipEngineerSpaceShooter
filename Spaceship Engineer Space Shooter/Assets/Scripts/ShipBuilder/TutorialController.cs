using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The tutorial of one scene: a hint window with a text, a finger pointing at what to press, and (optionally) a dimmed screen that only
/// lets the player press that. Steps (TutorialStep) run in list order; each is done once its Complete When happens, and done steps are
/// remembered in the save (GameData.tutorialDoneSteps), so every step is shown until done and never again after. The main scene and the
/// battle scene each have their own TutorialController with their own steps; the ids are shared by the whole game.
///
/// SECTIONS: a step with a Start When other than Always opens a section (it and the Always steps after it) — "a task is done: claim it",
/// "enough research points: research shields". A section that cannot start yet is passed over, so it never holds up the others.
///
///  - A target inside a scroll view (the tech tree, the level map) is scrolled into view when its step begins.
///  - The hint window is never over the target: it goes to the bigger free part of the screen above or below it.
///  - Touching the target hides the hint window at once (the step still waits for its Complete When).
///  - The window's close button puts the whole tutorial away until the game is started again (it then picks up where it was).
///  - "Don't show the tutorial again" (the toggle) switches the whole tutorial off for good.
///  - A step whose Require Active object is not active (the builder is closed...) gives way to the nearest earlier step that can be shown,
///    so a game closed half-way through picks up correctly.
///
/// SETUP: Tools > Ship Builder > Create Tutorial builds this (its own canvas over everything) with the default steps of the open scene and
/// finds their targets. Restyle the window and give the finger your icon afterwards.
/// </summary>
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

    /// <summary>The close button was pressed: no tutorial for the rest of this run of the game (every scene).</summary>
    private static bool dismissedThisRun;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRun() => dismissedThisRun = false; // also when Play mode starts without a domain reload

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
        // A step that only waits to be read is done by closing it; any way, the tutorial goes away until the game starts again.
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
        if (currentTarget != null && lastTarget != currentTarget) ScrollIntoView(currentTarget); // once, when it is found
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
            HideAll(); // not on the screen yet (the palette is still filling, the list is scrolling in...)
            return;
        }

        Layout();
    }

    // ---------- steps ----------

    /// <summary>The first step not done yet — or, if it cannot be shown here and now (its Require Active is off), the nearest earlier one
    /// that can (done or not).</summary>
    private TutorialStep ResolveStep()
    {
        for (int start = 0; start < steps.Count;)
        {
            int end = start + 1;
            while (end < steps.Count && steps[end] != null && steps[end].startWhen == TutorialStart.Always) end++;

            var step = ResolveInSection(start, end);
            if (step != null) return step;
            start = end; // all done, or cannot start yet: the next section
        }

        return null;
    }

    /// <summary>The step to show in the section [start, end), or null if none can be shown now.</summary>
    private TutorialStep ResolveInSection(int start, int end)
    {
        var gate = steps[start];
        if (gate == null) return null;
        if (!Data.IsTutorialStepDone(gate.id) && !CanStart(gate) && !CanSkip(gate)) return null; // this section's time has not come

        int index = FirstNotDone(start, end);

        // A step whose result is already there is done, even if it was never shown: pressing Campaign closes the game-mode popup (this
        // step's Require Active) in the same moment it opens the campaign panel (its Complete When) — without this check it would give way
        // to the step before it ("Tap Play") instead of counting.
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

    /// <summary>Done without being shown: its result is already there, or its whole reason is gone (the tech it is about is researched).</summary>
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

    /// <summary>Whether a step that waits for a STATE (a panel open or closed, nothing left to claim) already has it. Steps that wait for an
    /// action (a tap, a hold, a new block) are never done this way.</summary>
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

    // ---------- targets ----------

    private RectTransform FindTarget(TutorialStep step)
    {
        if (step.targetKind == TutorialTargetKind.Object)
            return step.target != null && step.target.gameObject.activeInHierarchy ? step.target : null;

        // Run-time objects: searched for a few times a second, kept while they stay on the screen.
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

    // ---------- the player's finger ----------

    private bool tappedTarget;
    private RectTransform lastTarget;

    private void TrackPointer()
    {
        tappedTarget = false;
        if (!ReadPointer(out Vector2 position, out bool down, out bool pressedNow, out bool releasedNow)) return;

        bool onTarget = currentTarget != null && RectTransformUtility.RectangleContainsScreenPoint(currentTarget, position, CameraOf(currentTarget));
        // A button that hides itself (or its panel) when pressed is gone by the time the finger is lifted: that is a tap too.
        bool targetGone = lastTarget != null && currentTarget == null;

        if (pressedNow && onTarget)
        {
            pressStartedOnTarget = true;
            windowHidden = true; // touching what the hint is about puts the hint away
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

    /// <summary>The first finger (or the mouse). When it has just been lifted, the position is where it was last seen.</summary>
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

    // ---------- layout ----------

    private void Layout()
    {
        if (root == null) return;
        Rect area = new(-root.rect.size * 0.5f, root.rect.size); // origin in the root's middle, like everything placed in it

        bool hasTarget = currentTarget != null;
        Rect target = hasTarget ? TargetRectInRoot(currentTarget) : new Rect(area.center, Vector2.zero);

        // Hint window: against the screen edge on the far side of the target (above it if the target is low, below if high) — never over
        // the target, nor over the middle of the screen where a block is dragged to.
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

        // Finger
        if (finger != null)
        {
            if (!hasTarget || current.finger == TutorialFingerMotion.None) finger.Hide();
            else
            {
                Vector2 end = current.dragTo != null && current.dragTo.gameObject.activeInHierarchy ? TargetRectInRoot(current.dragTo).center : area.center;
                finger.Play(current.finger, target.center, end, Mathf.Min(target.width, target.height) * 0.35f);
            }
        }

        // Dimmed strips around the target that block every other tap.
        bool block = hasTarget && current.blockOthers;
        if (blockers != null && blockers.Length == 4)
        {
            Rect hole = new(target.xMin - targetPadding, target.yMin - targetPadding, target.width + targetPadding * 2f, target.height + targetPadding * 2f);
            SetBlocker(0, block, new Rect(area.xMin, hole.yMax, area.width, area.yMax - hole.yMax));          // top
            SetBlocker(1, block, new Rect(area.xMin, area.yMin, area.width, hole.yMin - area.yMin));          // bottom
            SetBlocker(2, block, new Rect(area.xMin, hole.yMin, hole.xMin - area.xMin, hole.height));         // left
            SetBlocker(3, block, new Rect(hole.xMax, hole.yMin, area.xMax - hole.xMax, hole.height));         // right
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

    /// <summary>The target's rectangle in the root's local space (origin in the root's middle).</summary>
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
            local -= root.rect.center; // relative to the root's middle, whatever its pivot
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    /// <summary>If the target sits in a scroll view, moves the content so the target is in the middle of the view (as far as it goes).</summary>
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
