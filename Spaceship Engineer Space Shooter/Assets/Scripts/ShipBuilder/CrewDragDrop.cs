using UnityEngine;
using UnityEngine.UI;

public interface ICrewMemberProvider
{
    CrewMemberData Member { get; }
    Image PortraitImage { get; }
    bool CanDrag { get; }
}

public interface ICrewDropTarget
{
    bool CanAccept(CrewMemberData member);
    void Drop(CrewMemberData member);
}

public interface ICrewReleaseHandler
{
    void OnReleasedOutside(CrewMemberData member);
}
