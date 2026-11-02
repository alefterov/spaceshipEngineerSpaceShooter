using System;
using UnityEngine;

/// <summary>What a tutorial step points at.</summary>
public enum TutorialTargetKind
{
    /// <summary>Nothing: just a message in the middle of the screen.</summary>
    None = 0,
    /// <summary>The UI object in Target.</summary>
    Object = 1,
    /// <summary>The builder palette's button of the block with id Block Id (created at run time).</summary>
    PaletteBlock = 2,
    /// <summary>The level map's button of Level (created in the scene, found at run time).</summary>
    LevelButton = 3,
    /// <summary>The Claim button of the first task (or level reward) that can be taken now.</summary>
    ClaimableReward = 4,
    /// <summary>The tech tree's research button of Tech.</summary>
    TechNode = 5,
}

/// <summary>When a tutorial step counts as done.</summary>
public enum TutorialComplete
{
    /// <summary>Just a message: done when the player closes it (closing also puts the tutorial away until the next start of the game).</summary>
    Closed = 0,
    /// <summary>The player taps the target.</summary>
    TargetTapped = 1,
    /// <summary>Watch Object becomes active (a panel opened).</summary>
    ObjectActive = 2,
    /// <summary>Watch Object becomes inactive (a panel closed).</summary>
    ObjectInactive = 3,
    /// <summary>A block was added to the player's ship (main scene).</summary>
    BlockAdded = 4,
    /// <summary>The player has held a finger on the target for Hold Seconds in total (the joystick).</summary>
    TargetHeld = 5,
    /// <summary>Nothing left to claim in the rewards panel.</summary>
    NoClaimableRewards = 6,
    /// <summary>Tech has been researched.</summary>
    TechResearched = 7,
}

/// <summary>When a tutorial step may appear at all.</summary>
public enum TutorialStart
{
    Always = 0,
    /// <summary>Once a task or a level reward can be claimed.</summary>
    HasClaimableRewards = 1,
    /// <summary>Once the battle has started (after the countdown).</summary>
    BattleStarted = 2,
    /// <summary>Once Tech can be researched (enough research points, prerequisite done). A Tech already researched skips the section.</summary>
    CanResearchTech = 3,
}

/// <summary>How the pointing finger moves.</summary>
public enum TutorialFingerMotion
{
    None = 0,
    /// <summary>Taps the target again and again.</summary>
    Tap = 1,
    /// <summary>Drags from the target to Drag To (the middle of the screen if empty) again and again.</summary>
    Drag = 2,
    /// <summary>Moves right, left, up and down over the target, like a thumb on a joystick.</summary>
    Joystick = 3,
}

/// <summary>One step of the tutorial: a hint text, what it points at, and what the player has to do. See TutorialController.</summary>
[Serializable]
public class TutorialStep
{
    [Tooltip("Unique id, kept in the save once the step is done. Never change it after release.")]
    public string id = "step";
    [TextArea(2, 5)] public string text = "";

    [Header("Target")]
    public TutorialTargetKind targetKind = TutorialTargetKind.None;
    public RectTransform target;
    [Tooltip("Palette Block: the block's id (BlockDefinition.id).")]
    public string blockId;
    [Tooltip("Level Button: the level.")]
    public LevelDefinition level;
    [Tooltip("Tech Node / Can Research Tech / Tech Researched: the technology.")]
    public TechDefinition tech;

    [Header("When")]
    [Tooltip("Anything but Always starts a SECTION: this step and the Always steps after it. A section whose first step cannot start yet is " +
             "passed over (it does not hold the ones after it up) and comes back once it can.")]
    public TutorialStart startWhen = TutorialStart.Always;
    [Tooltip("The step only makes sense while this object is active (the builder, a popup). If it is not, the tutorial goes back to the " +
             "nearest earlier step that can be shown (e.g. \"open the builder\") — so a game closed half-way picks up again correctly.")]
    public GameObject requireActive;
    public TutorialComplete completeWhen = TutorialComplete.TargetTapped;
    [Tooltip("Object Active / Object Inactive: the object watched.")]
    public GameObject watchObject;
    [Tooltip("Target Held: seconds of holding needed.")]
    [Min(0f)] public float holdSeconds = 1.5f;

    [Header("Look")]
    public TutorialFingerMotion finger = TutorialFingerMotion.Tap;
    [Tooltip("Drag: where the finger drags to. Empty = the middle of the screen.")]
    public RectTransform dragTo;
    [Tooltip("Dims the screen around the target and blocks every other tap, so the player has to do this step.")]
    public bool blockOthers = true;
}
