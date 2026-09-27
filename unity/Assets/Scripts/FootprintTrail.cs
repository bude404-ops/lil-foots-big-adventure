using UnityEngine;

namespace LilFoots {
    /// <summary>
    /// FOOTPRINT TRAIL (BudE approved Sept 27 PM: lives = Bigfoot's glowing footprint
    /// trail, not hearts). Three lit footprints in the HUD; losing a life fades one
    /// from the trail. Pure lore-native reskin of the classic 3-lives row — the
    /// system underneath (3 lives, checkpoint respawn) is unchanged.
    /// </summary>
    public class FootprintTrail : MonoBehaviour {
        public SpriteRenderer[] icons;   // wired by ArtPass (HUDPrint0..2)
        [HideInInspector] public int shown = -1;

        static readonly Color lit  = new Color(1f, 1f, 1f, 1f);
        static readonly Color dim  = new Color(0.38f, 0.36f, 0.33f, 0.55f);   // faded print = spent life

        void LateUpdate() {
            var p = PlayerController.Instance;
            if (p == null || icons == null) return;
            if (shown == p.lives) return;
            shown = p.lives;
            for (int i = 0; i < icons.Length; i++)
                if (icons[i] != null) icons[i].color = (i < p.lives) ? lit : dim;
        }
    }
}
