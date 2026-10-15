using UnityEngine;
using UnityEngine.UI;

/// <summary>Anything on the crew screen that shows one crew member and can be dragged by it — a collection
/// card, a slot. CrewDragHandle reads this to know WHO is being dragged and what picture to carry.</summary>
public interface ICrewMemberProvider
{
    /// <summary>Null when empty — an empty provider can't be dragged.</summary>
    CrewMemberData Member { get; }
    /// <summary>The picture the drag "ghost" copies. May be null (the ghost is then a plain tinted box).</summary>
    Image PortraitImage { get; }
    bool CanDrag { get; }
}

/// <summary>Anything a dragged crew member can be dropped on — a slot, the collection, a merge window.
/// The drop is decided by the TOPMOST such target under the finger: if it refuses, nothing happens (the
/// drop doesn't fall through to whatever is underneath).</summary>
public interface ICrewDropTarget
{
    bool CanAccept(CrewMemberData member);
    void Drop(CrewMemberData member);
}

/// <summary>A drag source that wants to know when its member was released over NO drop target at all — a
/// crew slot uses it to let go of its member ("drag it out of the slot to take it off").</summary>
public interface ICrewReleaseHandler
{
    void OnReleasedOutside(CrewMemberData member);
}
