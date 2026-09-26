using UnityEngine;

namespace LilFoots {
/// <summary>
/// FRAME-POLLED CONTROL DECK (BudE, Sept 25 ~11:45 PM ET: "the left and right motion
/// button they are too close or for some reason the character doesnt always go that
/// direction you want"). Root cause of the dead direction: uGUI pointer events lose
/// drags BETWEEN buttons - OnPointerExit releases the old button and nothing ever
/// re-captures the pointer on the new one, so a sliding thumb parks the character.
///
/// This component OWNS the TouchDeck flags. Every frame it scans every active touch
/// (and the mouse for the web preview) and maps each to the nearest control region -
/// PADDED well beyond the visible art and extended down to the screen bottom, so a
/// thumb can never fall in a dead zone and a drag from LEFT to RIGHT switches the
/// direction mid-slide. TouchDeckButton is now visual-only (tap flash).
/// </summary>
public class TouchDeckRoot : MonoBehaviour {
    public RectTransform canvasRect;
    public RectTransform leftRect, rightRect, jumpRect;
    public float padX = 55f;     // hit zone extends this far past the visible art, each side
    public float padTop = 45f;   // ... and this far above it

    void LateUpdate() {
        bool l = false, r = false, j = false;
        for (int i = 0; i < Input.touchCount; i++) {
            var t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
            Hit(t.position, ref l, ref r, ref j);
        }
        // web preview: mouse drives the deck the same way (only when no touch device is active)
        if (Input.touchCount == 0 && Input.GetMouseButton(0))
            Hit(Input.mousePosition, ref l, ref r, ref j);
        TouchDeck.LeftHeld = l;
        TouchDeck.RightHeld = r;
        TouchDeck.JumpHeld = j;
    }

    /// <summary>Map one pointer to a control. Padded regions overlap slightly in the gap
    /// between LEFT/RIGHT - nearest button center wins, so there is NEVER a dead zone.</summary>
    void Hit(Vector3 screenPos, ref bool l, ref bool r, ref bool j) {
        if (canvasRect == null) return;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out local))
            return;
        // anchored space: canvas local is pivot-centered; buttons are anchored bottom-left
        Vector2 p = local + new Vector2(canvasRect.rect.width / 2f, canvasRect.rect.height / 2f);

        float jL, jR, jT;
        Region(jumpRect, out jL, out jR, out jT);
        if (p.x >= jL && p.x <= jR && p.y <= jT) { j = true; return; }   // jump first: right corner is its turf
        if (p.y > jT) return;                                            // above the deck: gameplay taps, not deck

        float lL, lR, lT, rL, rR, rT;
        Region(leftRect,  out lL, out lR, out lT);
        Region(rightRect, out rL, out rR, out rT);
        bool inL = p.x >= lL && p.x <= lR && p.y <= lT;
        bool inR = p.x >= rL && p.x <= rR && p.y <= rT;
        if (inL && inR) {
            float dl = Mathf.Abs(p.x - leftRect.anchoredPosition.x);
            float dr = Mathf.Abs(p.x - rightRect.anchoredPosition.x);
            if (dl <= dr) l = true; else r = true;
        } else if (inL) l = true;
        else if (inR) r = true;
    }

    void Region(RectTransform rt, out float x0, out float x1, out float yTop) {
        var pos = rt.anchoredPosition;
        var size = rt.sizeDelta;
        x0 = pos.x - size.x / 2f - padX;
        x1 = pos.x + size.x / 2f + padX;
        yTop = pos.y + size.y / 2f + padTop;
        // JUMP sits near the screen edge - extend its region to the canvas edge
        if (rt == jumpRect) x1 = canvasRect.rect.width + padX;
    }
}
}
