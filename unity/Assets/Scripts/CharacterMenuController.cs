using UnityEngine;

namespace LilFoots {
    /// <summary>Character select menu runtime: pauses the game, waits for a tap,
    /// stores the pick, resumes. Native uGUI per the Unity-native doctrine.</summary>
    public class CharacterMenuController : MonoBehaviour {
        public static string Current() {
            return PlayerPrefs.GetString("selChar", "lily");
        }

        void Start() { Time.timeScale = 0f; }

        public void Select(string character) {
            PlayerPrefs.SetString("selChar", character.ToLower());
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            Destroy(gameObject);
        }
    }
}
