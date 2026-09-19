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

        void Start() { Time.timeScale = 0f; }

        public void Select(string character) {
            PlayerPrefs.SetString("selChar", character.ToLower());
            PlayerPrefs.Save();
            Time.timeScale = 1f;
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
