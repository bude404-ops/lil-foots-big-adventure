using UnityEngine;

namespace LilFoots {
    /// <summary>Character select menu runtime: pauses the game, waits for a pick,
    /// stores the pick, resumes. Native uGUI per the Unity-native doctrine.
    /// Sept 19 pick-bug fix pass: also owns the idle-rig stage RenderTexture
    /// (built by LilFootsArtPass.BuildIdleStage) and releases it on pick.</summary>
    public class CharacterMenuController : MonoBehaviour {
        [System.NonSerialized] public RenderTexture idleStageTexture; // released on pick
        [System.NonSerialized] public GameObject idleStageRoot;      // SCENE-ROOT rig stage (Sept 19:
        // parented under a plain root, not this canvas - destroyed here so nothing leaks after the pick)

        public static string Current() {
            return PlayerPrefs.GetString("selChar", "lily");
        }

        float upFor;

        void Start() { Time.timeScale = 0f; upFor = 0f; }

        void Update() {
            // MENU UN-STICK (Bude, Sept 20: live build report "the foreground is covering the
            // middle layer so can't even see the character and the jump button isnt working").
            // This modal menu is ALSO the thing that freezes the game (timeScale 0 + a 55%-black
            // backdrop over the gameplay). If uGUI pointer events die on phone WebGL, the pick
            // never fires and the game strands HERE - which reads exactly like Bude's report.
            // Two EventSystem-independent exits:
            //  1) a raw touch/mouse press picks the card under that screen third (the cards are
            //     laid out as three columns, so thirds match the visible layout)
            //  2) 12s idle -> auto-start as the current character
            // Either way the game can never strand on the select screen.
            upFor += Time.unscaledDeltaTime;
            if (upFor < 0.6f) return; // ignore the tap that got us here (splash skip)
            bool pressed = Input.touchCount > 0 || Input.GetMouseButtonDown(0);
            if (pressed) {
                float u = 0.5f;
                if (Input.touchCount > 0) u = Input.GetTouch(0).position.x / (float)Screen.width;
                else u = Input.mousePosition.x / (float)Screen.width;
                Select(u < 1f / 3f ? "lily" : u < 2f / 3f ? "buddy" : "emma");
            } else if (upFor > 12f) {
                Select(Current()); // idle safety net - the game always starts
            }
        }

        public void Select(string character) {
            PlayerPrefs.SetString("selChar", character.ToLower());
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            // GAMEPLAY RIG SWAP: three rigged Lil Foots are pre-built under the player; the
            // pick enables the chosen one so the in-game character matches the select (Sept 20).
            // find the PLAYER by its controller, not by name - the select stage also has
            // "Lily"-named copies (off-map band), and Find can return those instead.
            var pctrl = UnityEngine.Object.FindObjectOfType<PlayerController>();
            var player = pctrl != null ? pctrl.gameObject : null;
            if (player != null) {
                string cap = character.ToLower();
                cap = char.ToUpper(cap[0]) + cap.Substring(1);
                foreach (Transform t in player.transform) {
                    bool match = t.name == cap + "Rig";
                    if (t.name.EndsWith("Rig")) t.gameObject.SetActive(match);
                }
            }
            if (idleStageTexture != null) {
                Destroy(idleStageTexture);
                idleStageTexture = null;
            }
            if (idleStageRoot != null) {
                Destroy(idleStageRoot);
                idleStageRoot = null;
            }
            Destroy(gameObject); // menu canvas -> also kills the idle stage (its child)
        }
    }
}
