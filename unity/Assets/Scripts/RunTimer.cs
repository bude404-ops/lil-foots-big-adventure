using UnityEngine;

namespace LilFoots {
/// <summary>
/// RUN TIMER + MEDALS (BudE Sept 26 ~1:54 AM ET: "Ok lets go your recommendation on the
/// incentives" - per-map timer, gold/silver/bronze medal targets, best times). Speedrun
/// law: the clock starts on the FIRST movement input, freezes at the flag. Best time per
/// course persists in PlayerPrefs. The end-of-run panel shows COURSE CLEAR, the medal,
/// time, best, and NEW RECORD before the course reloads. Medal targets are per-course
/// (public fields, tune per scene). The World Trail totem global leaderboard (backend
/// scores) lands in the next incentive cycle.
/// </summary>
public class RunTimer : MonoBehaviour {
    public static RunTimer Instance { get; private set; }

    [Header("Medal targets (seconds)")]
    public float gold = 60f;
    public float silver = 90f;
    public float bronze = 120f;

    float t;
    bool started, running, finished;
    float resultUntil = -1f;
    float shownTime;
    bool newBest;

    public static string CourseKey() {
        return "lilfoots.best." + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    }
    public float Elapsed { get { return t; } }
    public bool Running { get { return running; } }

    void Awake() { Instance = this; }

    void Update() {
        if (finished) return;
        if (running) { t += Time.deltaTime; return; }
        // clock starts on first movement input (touch, keyboard, or mouse-deck)
        float axis = Input.GetAxisRaw("Horizontal");
        bool jumpKey = Input.GetButtonDown("Jump");
        if (Mathf.Abs(axis) > 0.1f || TouchDeck.LeftHeld || TouchDeck.RightHeld || TouchDeck.JumpHeld || jumpKey) {
            started = true; running = true;
        }
    }

    /// <summary>GameManager.Win() calls this: freeze the clock, save best, show results.</summary>
    public float Finish() {
        finished = true; running = false;
        shownTime = t;
        float best = PlayerPrefs.GetFloat(CourseKey(), 0f);
        newBest = (best <= 0f || shownTime < best);
        if (newBest) PlayerPrefs.SetFloat(CourseKey(), shownTime);
        PlayerPrefs.Save();
        resultUntil = Time.unscaledTime + 4.5f;
        Debug.Log("[LilFoots] RUN TIME " + Fmt(shownTime) + " medal " + MedalFor(shownTime)
                  + (newBest ? " (NEW BEST - was " + (best > 0f ? Fmt(best) : "none") + ")" : " best " + Fmt(best)));
        return shownTime;
    }

    /// <summary>GameManager waits on this before reloading the course.</summary>
    public bool ResultsDone { get { return resultUntil > 0f && Time.unscaledTime > resultUntil; } }

    public string MedalFor(float time) {
        if (time <= gold) return "GOLD";
        if (time <= silver) return "SILVER";
        if (time <= bronze) return "BRONZE";
        return "";
    }

    string Fmt(float s) {
        int m = (int)(s / 60f);
        return m + ":" + (s - m * 60f).ToString("00.00");
    }

    void OnGUI() {
        if (started && !finished) {
            var st = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(Screen.width * 0.42f, 8f, Screen.width * 0.16f, 34f), Fmt(t), st);
        }
        if (resultUntil > 0f && Time.unscaledTime < resultUntil) {
            float w = Screen.width * 0.44f, h = Screen.height * 0.4f;
            var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(r, "");
            var big = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            var mid = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            GUILayout.BeginArea(r);
            GUILayout.FlexibleSpace();
            GUILayout.Label("COURSE CLEAR!", big);
            var medal = MedalFor(shownTime);
            GUILayout.Label(string.IsNullOrEmpty(medal) ? "no medal - push for it" : medal + " MEDAL", big);
            GUILayout.Label("time   " + Fmt(shownTime), mid);
            float best = PlayerPrefs.GetFloat(CourseKey(), 0f);
            GUILayout.Label("best   " + (best > 0f ? Fmt(best) : "-"), mid);
            if (newBest) GUILayout.Label("NEW RECORD!", big);
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }
    }
}
}
