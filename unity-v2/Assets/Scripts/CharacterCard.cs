using UnityEngine;
using UnityEngine.EventSystems;

namespace LilFoots {
    /// <summary>
    /// Character select card (Bude, Sept 19: "It doesn't let me actually pick a character on the
    /// select"). The pick fires on POINTER DOWN - instant response for touch AND mouse, with no
    /// dependency on the full click-release cycle that uGUI Buttons require. The Button on the same
    /// GameObject stays for keyboard navigation and visible tint feedback; either path calls Select.
    /// Native uGUI per the Unity-native doctrine.
    /// </summary>
    public class CharacterCard : MonoBehaviour, IPointerDownHandler {
        public CharacterMenuController menu;
        public string character;

        public void OnPointerDown(PointerEventData e) {
            if (menu != null) menu.Select(character);
        }
    }
}
