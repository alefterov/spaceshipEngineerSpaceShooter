using UnityEngine;

/// <summary>
/// Open/close panel for the tech-tree screen. Wire the "Technologies" button's onClick (wherever it
/// lives — main menu, HUD, etc.) to Open(), and this popup's own close button's onClick to Close().
///
/// Both methods are plain SetActive calls with no dependency on this component's own Awake/OnEnable
/// having run — safe even though this GameObject should start INACTIVE in the scene (so it isn't
/// visible before the player opens it). Each TechNodeView inside re-subscribes and refreshes itself
/// automatically whenever this panel is (re)activated — see TechNodeView.OnEnable.
/// </summary>
public class TechTreePopup : MonoBehaviour
{
    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);
    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);
}
