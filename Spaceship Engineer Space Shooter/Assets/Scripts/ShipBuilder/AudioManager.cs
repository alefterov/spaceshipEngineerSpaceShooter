using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The game's sound: one music channel (with cross-fades between tracks) and a pool of effect voices, so many
/// sounds can play at once. It has TWO volumes, saved on the device and set by the sliders of the settings menu
/// (AudioSettingsPanel): Music Volume and Sfx Volume.
///
/// HOW SOUNDS GET HERE: every sound is an AudioClip you drop into a field in the Inspector — the field sits on the
/// thing that makes the sound (a weapon's Fire Sound, a shield's Absorb Sound, a meteor's Impact Sound, the Place
/// Sound of the builder...). Those objects just call AudioManager.PlaySfx(clip, volume); this class mixes it with the
/// Sfx Volume. Music: put a SceneMusic in each scene. Button clicks: Button Click Clip here, played for EVERY button.
///
/// SETUP: put one AudioManager on an object in the first scene that loads (the main menu). It survives scene changes.
/// (If none exists one is created on first use, but then it has no Button Click Clip.)
/// </summary>
public class AudioManager : MonoBehaviour
{
    private const string MusicKey = "audio.music";
    private const string SfxKey = "audio.sfx";

    [Header("Volumes (saved — used until the player changes them)")]
    [Range(0f, 1f)] public float defaultMusicVolume = 0.7f;
    [Range(0f, 1f)] public float defaultSfxVolume = 1f;

    [Header("Effects")]
    [Tooltip("How many sounds can play at the same moment. When all voices are busy the oldest sound is cut off.")]
    [Min(4)] public int sfxVoices = 24;
    [Tooltip("The same clip is not started again within this many seconds (two barrels firing in one frame sound as one).")]
    [Min(0f)] public float minRepeatInterval = 0.04f;
    [Tooltip("Each sound's pitch varies randomly by up to this much, so repeated shots don't sound mechanical. 0 = never.")]
    [Range(0f, 0.3f)] public float pitchVariation = 0.05f;

    [Header("Buttons")]
    [Tooltip("Played whenever the player presses ANY button (add a SilentButton to a button to leave it out).")]
    public AudioClip buttonClickClip;
    [Range(0f, 1f)] public float buttonClickVolume = 1f;

    private static AudioManager instance;
    private static bool quitting;

    public static AudioManager Instance
    {
        get
        {
            if (instance == null && !quitting)
            {
                instance = FindFirstObjectByType<AudioManager>();
                if (instance == null) instance = new GameObject("AudioManager").AddComponent<AudioManager>();
            }
            return instance;
        }
    }

    public float MusicVolume { get; private set; }
    public float SfxVolume { get; private set; }

    /// <summary>Raised when either volume changes (looping sounds follow it).</summary>
    public static event Action OnVolumesChanged;

    private AudioSource[] voices;
    private float[] voiceStarted;
    private readonly Dictionary<AudioClip, float> lastStarted = new();

    private class MusicChannel
    {
        public AudioSource source;
        public float level;       // fade level 0..1
        public float clipVolume;  // the level the track was asked to play at
    }

    private MusicChannel channelA, channelB, channelNow;
    private AudioClip currentMusic;
    private float fadeSeconds = 1f;

    // ---------- Setup ----------

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        MusicVolume = PlayerPrefs.GetFloat(MusicKey, defaultMusicVolume);
        SfxVolume = PlayerPrefs.GetFloat(SfxKey, defaultSfxVolume);

        voices = new AudioSource[sfxVoices];
        voiceStarted = new float[sfxVoices];
        for (int i = 0; i < voices.Length; i++) voices[i] = CreateSource($"Voice {i}", false);

        channelA = new MusicChannel { source = CreateSource("Music A", true) };
        channelB = new MusicChannel { source = CreateSource("Music B", true) };
    }

    private AudioSource CreateSource(string sourceName, bool loop)
    {
        var go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);

        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f; // 2D game — no positional sound
        return source;
    }

    private void OnApplicationQuit() => quitting = true;

    // ---------- Volumes ----------

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        OnVolumesChanged?.Invoke();
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxKey, SfxVolume);
        OnVolumesChanged?.Invoke();
    }

    // ---------- Effects ----------

    /// <summary>Plays a one-shot effect at `volume` (0-1) times the Sfx Volume. Safe with a null clip (does nothing).</summary>
    public static void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        var manager = Instance;
        if (manager != null) manager.Play(clip, volume);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (voices == null || SfxVolume <= 0f) return;

        float now = Time.unscaledTime;
        if (lastStarted.TryGetValue(clip, out float last) && now - last < minRepeatInterval) return;
        lastStarted[clip] = now;

        // A free voice, else the one that started longest ago.
        int chosen = 0;
        float oldest = float.MaxValue;
        for (int i = 0; i < voices.Length; i++)
        {
            if (!voices[i].isPlaying) { chosen = i; oldest = -1f; break; }
            if (voiceStarted[i] < oldest) { oldest = voiceStarted[i]; chosen = i; }
        }

        var voice = voices[chosen];
        voice.Stop();
        voice.clip = clip;
        voice.volume = Mathf.Clamp01(volume) * SfxVolume;
        voice.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
        voice.Play();
        voiceStarted[chosen] = now;
    }

    // ---------- Music ----------

    /// <summary>Starts a music track, cross-fading from the current one over `fade` seconds. The track that is already
    /// playing is left alone (only its level is updated).</summary>
    public void PlayMusic(AudioClip clip, float volume = 1f, float fade = 1f)
    {
        if (clip == null) return;

        if (clip == currentMusic && channelNow != null && channelNow.source.isPlaying)
        {
            channelNow.clipVolume = Mathf.Clamp01(volume);
            return;
        }

        var next = channelNow == channelA ? channelB : channelA;
        next.source.clip = clip;
        next.source.volume = 0f;
        next.source.Play();
        next.level = 0f;
        next.clipVolume = Mathf.Clamp01(volume);

        channelNow = next;
        currentMusic = clip;
        fadeSeconds = Mathf.Max(0.01f, fade);
    }

    /// <summary>Fades the music out and stops it.</summary>
    public void StopMusic(float fade = 1f)
    {
        channelNow = null;
        currentMusic = null;
        fadeSeconds = Mathf.Max(0.01f, fade);
    }

    private void Update()
    {
        if (channelA == null) return;

        UpdateChannel(channelA);
        UpdateChannel(channelB);
        DetectButtonClick();
    }

    /// <summary>The current track fades up to full, the others fade down and stop — and every level follows the music
    /// volume setting at once.</summary>
    private void UpdateChannel(MusicChannel channel)
    {
        if (!channel.source.isPlaying) return;

        float target = channel == channelNow ? 1f : 0f;
        channel.level = Mathf.MoveTowards(channel.level, target, Time.unscaledDeltaTime / fadeSeconds);
        channel.source.volume = channel.level * channel.clipVolume * MusicVolume;

        if (target <= 0f && channel.level <= 0f) channel.source.Stop();
    }

    // ---------- Buttons ----------

    /// <summary>One click sound for EVERY button, including ones created at run time: when a press lands on an enabled
    /// UI Button the sound plays. (Add SilentButton to a button to leave it out.)</summary>
    private void DetectButtonClick()
    {
        if (buttonClickClip == null) return;

        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;

        var events = EventSystem.current;
        if (events == null) return;

        var data = new PointerEventData(events) { position = pointer.position.ReadValue() };
        var hits = new List<RaycastResult>();
        events.RaycastAll(data, hits);

        foreach (var hit in hits)
        {
            // The topmost thing that wants the press decides: a button anywhere up its parents.
            var button = hit.gameObject.GetComponentInParent<Button>();
            if (button == null) continue;

            if (button.IsInteractable() && button.GetComponent<SilentButton>() == null)
                PlaySfx(buttonClickClip, buttonClickVolume);
            return;
        }
    }
}
