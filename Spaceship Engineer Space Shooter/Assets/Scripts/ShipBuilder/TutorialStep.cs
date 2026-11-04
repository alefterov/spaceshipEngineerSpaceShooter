using System;
using UnityEngine;

public enum TutorialTargetKind
{
    None = 0,
    Object = 1,
    PaletteBlock = 2,
    LevelButton = 3,
    ClaimableReward = 4,
    TechNode = 5,
}

public enum TutorialComplete
{
    Closed = 0,
    TargetTapped = 1,
    ObjectActive = 2,
    ObjectInactive = 3,
    BlockAdded = 4,
    TargetHeld = 5,
    NoClaimableRewards = 6,
    TechResearched = 7,
}

public enum TutorialStart
{
    Always = 0,
    HasClaimableRewards = 1,
    BattleStarted = 2,
    CanResearchTech = 3,
}

public enum TutorialFingerMotion
{
    None = 0,
    Tap = 1,
    Drag = 2,
    Joystick = 3,
}

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
