using UnityEngine;
using UnityEngine.EventSystems;

namespace LilFoots {
/// <summary>
/// MOBILE CONTROL DECK (the playability fix, Sept 18: Bude's "this isn't playable" — the TouchDeck
/// placeholder flags were never wired to anything). Native uGUI: each on-screen button is an Image
/// with this component; pointer down/up sets the static TouchDeck flags PlayerController already reads.
/// Works for touch (phone) AND mouse (web preview). Created at scene-build time by LilFootsArtPass.BuildTouchDeck.
/// </summary>
public class TouchDeckButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler {
    public enum Kind { Left, Right, Jump }
    public Kind kind;

    public void OnPointerDown(PointerEventData e) { Set(true); Flash(0.72f); }
    public void OnPointerUp(PointerEventData e) { Set(false); Flash(1f); }
    public void OnPointerExit(PointerEventData e) { Set(false); Flash(1f); }

    void Set(bool held) {
        switch (kind) {
            case Kind.Left:   TouchDeck.LeftHeld = held;  break;
            case Kind.Right: TouchDeck.RightHeld = held; break;
            case Kind.Jump:  TouchDeck.JumpHeld = held;  break;
        }
    }

    void Flash(float f) {
        var img = GetComponent<UnityEngine.UI.Image>();
        if (img != null) {
            var c = img.color; c.a = f; img.color = c;
        }
    }
}
}
