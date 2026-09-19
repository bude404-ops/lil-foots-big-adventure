using UnityEngine;

namespace LilFoots {
    /// <summary>Character select menu runtime: pauses the game, waits for a pick,
    /// stores the pick, resumes. Native uGUI per the Unity-native doctrine.
    /// Sept 19 pick-bug fix pass: also owns the idle-rig stage RenderTexture
    /// (built by LilFootsArtPass.BuildIdleStage) and releases it on pick.</summary>
    public class CharacterMenuController : MonoBehaviour {
        [System.NonSerialized] public RenderTexture idleStageTexture; // dies with the menu

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
            Destroy(gameObject); // menu canvas -> also kills the idle stage (its child)
        }
    }
}
