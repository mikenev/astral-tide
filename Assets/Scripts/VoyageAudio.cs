using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace AstralTide {
  /// <summary>Music, the two ambient beds and one-shots (Salt and Glory's GameAudio pattern): nothing plays until the first key,
  /// click, touch or gamepad button (WebGL autoplay), a per-id minimum gap, and missing clips are skipped (the chime alone has a
  /// procedural stand-in so lining up is still heard). Audio Dev's M1 set: dusk_shallows_loop (music, loops), landmark_arrival_swell
  /// (one-shot on arrival, the loop ducks under it), water_lapping_loop and wake_hiss_loop (beds; the hiss follows the speed),
  /// sail_filling, star_gust_whoosh and on_course_chime (one-shots).</summary>
  public class VoyageAudio : MonoBehaviour {
    public static VoyageAudio I;
    public AudioClip music, arrival, lapping, wakeHiss, sailFill, gust, chime;
    [Range(0, 1)] public float musicVolume = 0.55f, lappingVolume = 0.6f, wakeVolume = 0.75f, sfxVolume = 0.9f;
    public static bool Unlocked { get; private set; }
    AudioSource mus, lap, hiss, sfx;
    readonly Dictionary<string, (AudioClip clip, float vol, float gap)> rules = new(); readonly Dictionary<string, float> last = new();
    float duckUntil, wake01; bool musicOn;

    void Awake(){
      I = this;
      AudioSource Src(AudioClip c, bool loop, float v){ var s = gameObject.AddComponent<AudioSource>(); s.clip = c; s.loop = loop; s.playOnAwake = false; s.volume = v; return s; }
      mus = Src(music, true, musicVolume); lap = Src(lapping, true, lappingVolume); hiss = Src(wakeHiss, true, 0f); sfx = Src(null, false, 1f);
      if (!chime){ chime = MakeChime(); Debug.LogWarning("[Audio] on_course_chime missing: procedural chime"); }
      foreach (var (n, c) in new[]{ ("music", music), ("arrival", arrival), ("lapping", lapping), ("wake", wakeHiss), ("sail", sailFill), ("gust", gust) })
        if (!c) Debug.LogWarning($"[Audio] {n} clip missing (skipped)");
      rules["chime"] = (chime, 0.7f, 1.2f); rules["sail"] = (sailFill, 0.6f, 1.5f); rules["gust"] = (gust, 0.85f, 0.4f); rules["arrival"] = (arrival, 1f, 5f);
    }
    /// <summary>A soft rising fifth (sine partials, bell envelope): the stand-in when the chime file is missing.</summary>
    static AudioClip MakeChime(){
      int sr = 22050, n = sr * 2; var d = new float[n];
      for (int i = 0; i < n; i++){
        float t = i / (float)sr; float a = t < 0.5f ? 0f : 1f;
        float e0 = Mathf.Exp(-t * 3f) * Mathf.Min(1f, t * 200f), e1 = a * Mathf.Exp(-(t - 0.5f) * 3f) * Mathf.Min(1f, (t - 0.5f) * 200f);
        d[i] = 0.25f * (e0 * (Mathf.Sin(2 * Mathf.PI * 880f * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * 1760f * t)) + e1 * (Mathf.Sin(2 * Mathf.PI * 1318.5f * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * 2637f * t)));
      }
      var c = AudioClip.Create("chime_fallback", n, 1, sr, false); c.SetData(d, 0); return c;
    }
    static bool AnyInput(){
      var kb = Keyboard.current; if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
      var m = Mouse.current; if (m != null && m.leftButton.wasPressedThisFrame) return true;
      var t = Touchscreen.current; if (t != null && t.primaryTouch.press.wasPressedThisFrame) return true;
      var g = Gamepad.current; return g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame || g.rightTrigger.wasPressedThisFrame);
    }
    void Update(){
      if (!Unlocked && AnyInput()){ Unlocked = true; Debug.Log("[Audio] unlocked on first input"); Apply(); }
      if (!Unlocked) return;
      float dt = Time.unscaledDeltaTime;
      float duck = Time.unscaledTime < duckUntil ? 0.25f : 1f;
      mus.volume = Mathf.MoveTowards(mus.volume, musicOn ? musicVolume * duck : 0f, dt * 0.6f);
      hiss.volume = Mathf.MoveTowards(hiss.volume, wakeVolume * wake01, dt * 1.5f);
    }
    /// <summary>Start (or keep) the leg music and the beds.</summary>
    public void SetVoyage(bool on){ musicOn = on; if (Unlocked) Apply(); }
    void Apply(){
      if (mus.clip && !mus.isPlaying){ mus.volume = 0f; mus.Play(); }
      if (lap.clip && !lap.isPlaying) lap.Play();
      if (hiss.clip && !hiss.isPlaying) hiss.Play();
    }
    /// <summary>Wake hiss level from the ship's speed (0..1).</summary>
    public void SetWake(float k01) => wake01 = Mathf.Clamp01(k01);
    public void Pause(bool on){ AudioListener.pause = on; }
    /// <summary>The arrival swell, with the leg music ducked under it.</summary>
    public void Arrival(){ duckUntil = Time.unscaledTime + 6.5f; Play("arrival"); }
    public bool Play(string id){
      if (!Unlocked || !rules.TryGetValue(id, out var r) || !r.clip) return false;
      float now = Time.unscaledTime; if (last.TryGetValue(id, out var t) && now - t < r.gap) return false;
      last[id] = now; sfx.PlayOneShot(r.clip, r.vol * sfxVolume); return true;
    }
    // QA: what is playing
    public string Status() => $"unlocked={(Unlocked ? 1 : 0)} music={(mus.isPlaying ? 1 : 0)} musVol={mus.volume:F2} lap={(lap.isPlaying ? 1 : 0)} hiss={hiss.volume:F2}";
  }
}
