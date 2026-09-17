using System.Collections.Generic;
using UnityEngine;

namespace LilFoots
{
    // Player rig driver: procedural cartoon animation on the cutout parts
    // (Bude art bible s8: squash/stretch, anticipation, overshoot, exaggerated arcs)
    public class PlayerRig : MonoBehaviour
    {
        public Transform bodyT, footLT, footRT;
        public SpriteRenderer bodyR, footLR, footRR;
        [HideInInspector] public int castIndex;

        Vector3 bodyBase, flBase, frBase;
        float runPhase;
        float squash;   // 0..1 decaying
        float stretch;  // 0..1 decaying

        public void SetCast(int i)
        {
            castIndex = i;
            bodyR.sprite = RigAssets.S.bodies[i];
            footLR.sprite = RigAssets.S.feetL[i];
            footRR.sprite = RigAssets.S.feetR[i];
        }

        void Start()
        {
            bodyBase = bodyT.localPosition;
            flBase = footLT.localPosition;
            frBase = footRT.localPosition;
        }

        void Update()
        {
            squash = Mathf.Max(0f, squash - Time.deltaTime * 4.5f);
            stretch = Mathf.Max(0f, stretch - Time.deltaTime * 6.5f);
        }

        // state: 0 idle, 1 run, 2 air
        public void Drive(int state, float vy, int dir)
        {
            if (state == 1) runPhase += Time.deltaTime * 11f; else runPhase += Time.deltaTime * 1.2f;

            float bs = 1f, tilt = 0f, bob = 0f;
            float fl = 0f, fr = 0f, flLift = 0f, frLift = 0f;

            if (state == 0)
            { // idle: breathing bob + tiny sway
                bob = Mathf.Sin(runPhase) * 0.028f;
                tilt = Mathf.Sin(runPhase * 0.5f) * 1.2f;
            }
            else if (state == 1)
            { // run: alternating oversized-feet paddle + body bob at 2x + lean into it
                fl = Mathf.Sin(runPhase) * 38f;
                fr = Mathf.Sin(runPhase + Mathf.PI) * 38f;
                flLift = Mathf.Max(0f, -Mathf.Sin(runPhase)) * 0.10f;
                frLift = Mathf.Max(0f, Mathf.Sin(runPhase)) * 0.10f;
                bob = Mathf.Abs(Mathf.Sin(runPhase)) * 0.055f;
                tilt = 4.5f;
            }
            else
            { // air: legs tuck, body stretches by vertical speed (cartoon physics)
                fl = -14f; fr = -20f;
                bs = 1f + Mathf.Clamp(vy * 0.012f, -0.08f, 0.16f);
            }

            if (stretch > 0f) bs = 1f + stretch * 0.16f;          // launch stretch
            float sq = squash;                                      // landing squash
            float sy = bs * (1f - sq * 0.30f);
            float sx = (1f / bs) * (1f + sq * 0.34f);

            Vector3 rootScl = transform.localScale;
            rootScl.x = Mathf.Abs(rootScl.x) * dir;
            transform.localScale = new Vector3(rootScl.x, rootScl.y, 1f);

            bodyT.localPosition = bodyBase + new Vector3(0f, bob - sq * 0.10f, 0f);
            bodyT.localRotation = Quaternion.Euler(0f, 0f, tilt * dir);
            bodyT.localScale = new Vector3(sx, sy, 1f);

            footLT.localPosition = flBase + new Vector3(0f, flLift, 0f);
            footRT.localPosition = frBase + new Vector3(0f, frLift, 0f);
            footLT.localRotation = Quaternion.Euler(0f, 0f, fl);
            footRT.localRotation = Quaternion.Euler(0f, 0f, fr);
        }

        public void OnJump() { stretch = 1f; squash = 0f; }
        public void OnLand(float impact) { squash = Mathf.Clamp01(impact / 15f); stretch = 0f; }
    }

    // Cutout character controller: manual kinematic platformer (one-screen Unity test)
    public class PlayerController : MonoBehaviour
    {
        public List<BoxCollider2D> platforms;
        public PlayerRig rig;
        public ParticleSystem dust;
        public Camera cam;

        float vx, vy;
        bool grounded;
        int dir = -1;               // art faces left natively
        public float minX = 1f, maxX = 29f;
        float X => transform.position.x;

        void Update()
        {
            // input: keyboard + touch thirds
            float move = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) move -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) move += 1f;
            bool jumpHeld = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
            bool jumpPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);

            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                if (t.position.x < Screen.width * 0.33f) move -= 1f;
                else if (t.position.x > Screen.width * 0.67f) move += 1f;
                if (t.position.y > Screen.height * 45f / 100f)
                {
                    if (t.phase == TouchPhase.Began) jumpPressed = true;
                    jumpHeld = true;
                    if (t.position.x >= Screen.width * 0.33f && t.position.x <= Screen.width * 0.67f) move = 0f;
                }
            }

            // character switch (test convenience)
            if (Input.GetKeyDown(KeyCode.Alpha1)) rig.SetCast(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) rig.SetCast(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) rig.SetCast(2);

            float speed = 4.6f;
            vx = Mathf.Lerp(vx, move * speed, Time.deltaTime * 12f);
            if (move > 0.1f) dir = 1;
            if (move < -0.1f) dir = -1;

            bool wasGrounded = grounded;
            if (jumpPressed && grounded) { vy = 14.5f; grounded = false; rig.OnJump(); if (dust) dust.Emit(5); }
            if (!jumpHeld && vy > 4f) vy = 4f; // variable jump height

            vy -= 26f * Time.deltaTime;
            if (vy < -18f) vy = -18f;

            float px = X + vx * Time.deltaTime;
            px = Mathf.Clamp(px, minX, maxX);
            float py = transform.position.y + vy * Time.deltaTime;

            // AABB vs platforms (feet point)
            grounded = false;
            foreach (var p in platforms)
            {
                if (px < p.bounds.min.x - 0.05f || px > p.bounds.max.x + 0.05f) continue;
                float top = p.bounds.max.y;
                if (vy <= 0f && transform.position.y >= top - 0.06f && py <= top)
                {
                    py = top;
                    if (!wasGrounded)
                    {
                        rig.OnLand(-vy);
                        if (dust && -vy > 3f) dust.Emit(Mathf.Min(14, (int)(-vy * 1.4f)));
                    }
                    vy = 0f;
                    grounded = true;
                }
            }
            transform.position = new Vector3(px, py, 0f);

            int state = !grounded ? 2 : (Mathf.Abs(vx) > 0.6f ? 1 : 0);
            rig.Drive(state, vy, dir);

            if (cam != null)
            {
                Vector3 cp = cam.transform.position;
                cp.x = Mathf.Lerp(cp.x, Mathf.Clamp(px, minX + 4.5f, maxX - 4.5f), Time.deltaTime * 6f);
                cam.transform.position = cp;
                Parallax.px = cp.x;
            }
        }
    }

    // Parallax layer mover: bg bands at different rates (art bible s6: layered depth)
    public class Parallax : MonoBehaviour
    {
        public static float px;
        public float factor = 0.1f;
        public float spanW = 30f;
        Vector3 basePos;
        void Start() { basePos = transform.position; }
        void LateUpdate()
        {
            float off = Mathf.Repeat(px * factor, spanW);
            transform.position = new Vector3(basePos.x - off, basePos.y, basePos.z);
        }
    }

    // Token: bob + spin + pickup
    public class BigToken : MonoBehaviour
    {
        public ParticleSystem sparkle;
        float t0; bool got; Vector3 basePos;
        void Start() { t0 = Random.Range(0f, 6f); basePos = transform.localPosition; }
        void Update()
        {
            transform.rotation = Quaternion.Euler(0f, Time.time * 90f + t0 * 60f, 0f);
            transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * 3f + t0) * 0.15f);
        }
        void OnTriggerEnter2D(Collider2D c)
        {
            if (got || !c.CompareTag("Player")) return;
            got = true;
            if (sparkle) sparkle.Emit(12);
            TokenHUD.count++;
            gameObject.SetActive(false);
        }
    }

    public static class TokenHUD
    {
        public static int count;
    }

    // Simple HUD via OnGUI (test build)
    public class TestHUD : MonoBehaviour
    {
        void OnGUI()
        {
            GUIStyle st = new GUIStyle(GUI.skin.label);
            st.fontSize = Screen.height / 24;
            st.normal.textColor = new Color(0.09f, 0.25f, 0.17f, 1f);
            st.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(16, 12, Screen.width - 32, st.lineHeight * 2f),
                "LIL FOOTS x BIG ADVENTURE - Unity test  |  BIG TOKENS: " + TokenHUD.count, st);
        }
    }

    // Holds the sprite arrays for character switching
    public class RigAssets : MonoBehaviour
    {
        public static RigAssets S;
        public Sprite[] bodies, feetL, feetR;
        void Awake() { S = this; }
    }
}
