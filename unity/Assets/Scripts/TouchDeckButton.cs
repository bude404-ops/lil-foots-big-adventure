using UnityEngine;
using UnityEngine.EventSystems;

namespace LilFoots {
/// <summary>
/// MOBILE CONTROL DECK - VISUAL ONLY (Sept 25 deck fix). The deck state flags are owned
/// by TouchDeckRoot (frame-polled, drag-through safe - uGUI pointer events lose drags
/// between buttons, which is why the character "didn't always go the direction you
/// want"). This component just flashes the button art when a pointer taps it, so the
/// deck still feels alive. Native uGUI; created at scene-build time by
/// LilFootsArtPass.BuildTouchDeck. Works for touch (phone) AND mouse (web preview).
/// </summary>
public class TouchDeckButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler {
    public enum Kind { Left, Right, Jump }
    public Kind kind;

    public void OnPointerDown(PointerEventData e) { Flash(0.72f); }
    public void OnPointerUp(PointerEventData e)   { Flash(1f); }
    public void OnPointerExit(PointerEventData e) { Flash(1f); }

    void Flash(float f) {
        var img = GetComponent<UnityEngine.UI.Image>();
        if (img != null) {
            var c = img.color; c.a = f; img.color = c;
        }
    }
}
}
