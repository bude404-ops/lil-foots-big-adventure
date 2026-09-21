using UnityEngine;
using UnityEngine.SceneManagement;

namespace LilFoots {
/// <summary>Global constants + simple SFX/VFX stand-ins so the port runs before the full art/audio pass.</summary>
public class GameManager : MonoBehaviour {
    public static GameManager Instance { get; private set; }
    public static float GroundY = 6.2f;   // GROUND_Y 620px @ PPU 100
    public static float MapWidth = 96f;   // 9600px

    void Awake() { Instance = this; }

    public static void Win() {
        Sfx.Play(Sfx.Clip.Win);
        Debug.Log("[LilFoots] LEVEL CLEAR! tokens=" + PlayerController.Instance.tokens);
        // TODO per doctrine REWARD: objective rating screen (tokens %, secrets, no-damage) before reload
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}

public static class Sfx {
    public enum Clip { Jump, Stomp, Coin, Die, Snitch, CamSmash, DroneDie, Gate, Flag, Portal, Win, Hurt }
    public static void Play(Clip c) {
        // REAL AUDIO (BudE, Sept 20 "add sound effects and music"): routed through the
        // AudioManager's pooled AudioSources; legacy gadget/enemy clips map to the forest SFX set.
        if (AudioManager.Instance != null) {
            switch (c) {
                case Clip.Jump: AudioManager.Instance.Play("jump"); return;
                case Clip.Coin: AudioManager.Instance.Play("token"); return;
                case Clip.Die: AudioManager.Instance.Play("death"); return;
                case Clip.Gate: case Clip.Portal: case Clip.Flag: AudioManager.Instance.Play("levelcomplete"); return;
                default: return; // retired gadget/enemy clips (doctrine: enemies land at M3 with their own SFX)
            }
        }
        Debug.Log("[SFX] " + c);
    }
}

public static class Vfx {
    public static void Poof(Vector2 pos) { Debug.Log("[VFX] poof @ " + pos); }
}

public static class TouchDeck {
    public static bool JumpHeld, LeftHeld, RightHeld; // mobile control deck — wire to on-screen buttons
}
}
