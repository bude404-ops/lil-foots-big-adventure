// Kinematic run/jump controller driving the rig + FX triggers.
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    PlayerRig rig; FX fx;
    public RectTransform jumpButtonRect;

    public float vx = 0, vy = 0;
    public const float RUN = 6.2f, GRAV = 26f, JUMPV = 10.5f;
    public bool onGround = true;
    public int face = 1;
    float coyote = 0, jumpBuf = 0;
    bool jumpHeld = false, jumpWas = false;

    public void Init(PlayerRig r, FX f) { rig = r; fx = f; }

    void Update()
    {
        float dt = Time.deltaTime;
        int ax = 0;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) ax = -1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) ax = 1;

        // touch: hold lower-left / lower-right screen halves to run, JUMP button to jump
        bool touchRun = false;
        foreach (var tch in Input.touches)
        {
            if (jumpButtonRect != null && RectTransformUtility.RectangleContainsScreenPoint(jumpButtonRect, tch.position, null))
            {
                if (tch.phase == TouchPhase.Began) jumpBuf = 0.14f;
                jumpHeld = tch.phase != TouchPhase.Ended && tch.phase != TouchPhase.Canceled;
                continue;
            }
            if (tch.position.y < Screen.height * 0.45f)
            {
                ax = (tch.position.x < Screen.width * 0.5f) ? -1 : 1;
                touchRun = true;
            }
        }

        bool jump = Input.GetKey(KeyCode.Space) || jumpHeld;
        if (jump && !jumpWas) jumpBuf = 0.14f;
        jumpWas = jump;

        float target = ax * RUN;
        vx = Mathf.MoveTowards(vx, target, (ax != 0 ? 34f : 26f) * dt);
        if (ax != 0) face = ax;

        if (onGround) coyote = 0.12f; else coyote -= dt;
        if (jumpBuf > 0 && (onGround || coyote > 0))
        {
            vy = JUMPV; onGround = false; coyote = 0; jumpBuf = 0;
            rig.launchStretch = 1f;
            fx.DustBurst(transform.position + Vector3.down * 0.1f, 7, 0.9f);
        }
        jumpBuf -= dt;

        if (!onGround) vy -= GRAV * dt;
        float prevVy = vy;
        transform.position += new Vector3(vx * dt, vy * dt, 0);
        float x = Mathf.Clamp(transform.position.x, -28f, 28f);

        // ground at y=0
        if (transform.position.y < 0f)
        {
            bool wasAir = !onGround;
            transform.position = new Vector3(x, 0f, 0);
            vy = 0; onGround = true;
            if (wasAir)
            {
                rig.landSquash = 1f;
                fx.DustBurst(new Vector3(x, 0.05f, 0), 10, 1.2f);
            }
        }
        else transform.position = new Vector3(x, transform.position.y, 0);

        rig.vy = vy;
        rig.airborne = !onGround;
        rig.runSpeed01 = Mathf.Clamp01(Mathf.Abs(vx) / RUN);
        rig.groundY = onGround ? 0f : rig.groundY;
        if (onGround) rig.groundY = 0f;
        transform.localScale = new Vector3(face, 1, 1);

        if (onGround && Mathf.Abs(vx) > 3.5f && Time.frameCount % 8 == 0)
            fx.RunKick(new Vector3(x - face * 0.4f, 0.05f, 0), face);
    }
}
