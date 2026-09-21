using UnityEngine;

/// AUDIO MANAGER (BudE, Sept 20: "add sound effects and music") - Unity-native audio:
/// pooled AudioSources + one looping music source (seamless ~23s forest theme).
public class AudioManager : MonoBehaviour {
    public static AudioManager Instance;

    public AudioClip musicForestLoop;
    public AudioClip sfxJump, sfxToken, sfxCheckpoint, sfxDeath, sfxLevelComplete, sfxHeart;

    AudioSource music;
    AudioSource[] sfx;
    int sfxIdx = 0;

    void Awake() {
        Instance = this;
        music = gameObject.AddComponent<AudioSource>();
        music.clip = musicForestLoop;
        music.loop = true;
        music.volume = 0.32f;
        music.playOnAwake = false;

        sfx = new AudioSource[6];
        for (int i = 0; i < sfx.Length; i++) {
            sfx[i] = gameObject.AddComponent<AudioSource>();
            sfx[i].playOnAwake = false;
            sfx[i].volume = 0.8f;
        }
    }

    void Start() {
        if (music.clip != null) music.Play();
    }

    /// Play a one-shot effect: "jump", "token", "checkpoint", "death", "levelcomplete", "heart".
    public void Play(string name) {
        AudioClip clip = null;
        if (name == "jump") clip = sfxJump;
        else if (name == "token") clip = sfxToken;
        else if (name == "checkpoint") clip = sfxCheckpoint;
        else if (name == "death") clip = sfxDeath;
        else if (name == "levelcomplete") clip = sfxLevelComplete;
        else if (name == "heart") clip = sfxHeart;
        if (clip == null) return;
        var src = sfx[sfxIdx]; sfxIdx = (sfxIdx + 1) % sfx.Length;
        src.PlayOneShot(clip);
    }
}
