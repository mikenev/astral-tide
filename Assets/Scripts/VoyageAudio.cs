using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace AstralTide {
  /// <summary>Music, the two ambient beds and one-shots (Salt and Glory's GameAudio pattern): nothing plays until the first key,
  /// click, touch or gamepad button (WebGL autoplay), a per-id minimum gap, and missing clips are skipped (the chime alone has a
  /// procedural stand-in so lining up is still heard). Audio Dev's M1 set: dusk_shallows_loop (music, loops), landmark_arrival_swell
  /// (one-shot on arrival, the loop ducks under it), water_lapping_loop and wake_hiss_loop (beds; the hiss follows the speed),
  /// sail_filling, star_gust_whoosh and on_course_chime (one-shots). M2: one music loop per leg (crossfaded on a leg change), the
  /// current_rush_loop bed while riding a current, and meteor_bump, ice_creak, star_fragment_chime, constellation_complete,
  /// boost_whoosh and glacier_bell; the bump, bell and fragment chime have procedural stand-ins if a file is missing.</summary>
  public class VoyageAudio : MonoBehaviour {
    public static VoyageAudio I;
    public AudioClip music, arrival, lapping, wakeHiss, sailFill, gust, chime;
    public AudioClip[] legMusic = new AudioClip[6];
    public AudioClip currentRush, meteorBump, iceCreak, fragChime, constellation, boost, glacierBell;
    [Range(0, 1)] public float musicVolume = 0.55f, lappingVolume = 0.6f, wakeVolume = 0.75f, sfxVolume = 0.9f;
    public static bool Unlocked { get; private set; }
    AudioSource mus, lap, hiss, sfx, rush;
    readonly Dictionary<string, (AudioClip clip, float vol, float gap)> rules = new(); readonly Dictionary<string, float> last = new();
    float duckUntil, wake01, rush01; bool musicOn; int legNow; float swapFade = 1f; AudioClip pendingClip;

    void Awake(){
      I = this;
      AudioSource Src(AudioClip c, bool loop, float v){ var s = gameObject.AddComponent<AudioSource>(); s.clip = c; s.loop = loop; s.playOnAwake = false; s.volume = v; return s; }
      mus = Src(music, true, musicVolume); lap = Src(lapping, true, lappingVolume); hiss = Src(wakeHiss, true, 0f); sfx = Src(null, false, 1f); rush = Src(currentRush, true, 0f);
      for (int i = 0; i < legMusic.Length; i++) if (!legMusic[i]){ legMusic[i] = music; if (i > 0) Debug.LogWarning($"[Audio] leg {i + 1} music missing: using the Dusk Shallows loop"); }
      if (!meteorBump){ meteorBump = Tone("bump_fallback", 0.6f, 90f, 8f, 0.45f); Debug.LogWarning("[Audio] meteor_bump missing: procedural thud"); }
      if (!glacierBell){ glacierBell = Tone("bell_fallback", 4f, 196f, 0.9f, 0.3f, 2.76f); Debug.LogWarning("[Audio] glacier_bell missing: procedural bell"); }
      if (!fragChime){ fragChime = Tone("frag_fallback", 1.2f, 1568f, 3.5f, 0.2f, 1.5f); Debug.LogWarning("[Audio] star_fragment_chime missing: procedural glint"); }
      foreach (var (n, c) in new[]{ ("current_rush_loop", currentRush), ("ice_creak", iceCreak), ("constellation_complete", constellation), ("boost_whoosh", boost) })
        if (!c) Debug.LogWarning($"[Audio] {n} clip missing (skipped)");
      rules["bump"] = (meteorBump, 0.8f, 0.5f); rules["creak"] = (iceCreak, 0.75f, 1.2f); rules["frag"] = (fragChime, 0.8f, 0.3f); rules["constellation"] = (constellation, 0.9f, 3f);
      rules["boost"] = (boost, 0.8f, 0.4f); rules["bell"] = (glacierBell, 1f, 4f);
      if (!chime){ chime = MakeChime(); Debug.LogWarning("[Audio] on_course_chime missing: procedural chime"); }
      foreach (var (n, c) in new[]{ ("music", music), ("arrival", arrival), ("lapping", lapping), ("wake", wakeHiss), ("sail", sailFill), ("gust", gust) })
        if (!c) Debug.LogWarning($"[Audio] {n} clip missing (skipped)");
      rules["chime"] = (chime, 0.7f, 1.2f); rules["sail"] = (sailFill, 0.6f, 1.5f); rules["gust"] = (gust, 0.85f, 0.4f); rules["arrival"] = (arrival, 1f, 5f);
    }
    /// <summary>A decaying sine with an optional inharmonic partial: stand-in thud / bell / glint.</summary>
    static AudioClip Tone(string name, float len, float f, float decay, float vol, float partial = 0f){
      int sr = 22050, n = (int)(sr * len); var d = new float[n];
      for (int i = 0; i < n; i++){ float t = i / (float)sr, e = Mathf.Exp(-t * decay) * Mathf.Min(1f, t * 300f);
        d[i] = vol * e * (Mathf.Sin(2 * Mathf.PI * f * t) + (partial > 0f ? 0.45f * Mathf.Sin(2 * Mathf.PI * f * partial * t) * Mathf.Exp(-t * decay) : 0f)); }
      var c = AudioClip.Create(name, n, 1, sr, false); c.SetData(d, 0); return c;
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
      if (pendingClip){   // crossfade to the new leg's loop: fade out, swap, fade back in
        mus.volume = Mathf.MoveTowards(mus.volume, 0f, dt * 0.8f);
        if (mus.volume <= 0.001f){ mus.Stop(); mus.clip = pendingClip; pendingClip = null; if (mus.clip) mus.Play(); }
      } else mus.volume = Mathf.MoveTowards(mus.volume, musicOn ? musicVolume * duck : 0f, dt * 0.6f);
      rush.volume = Mathf.MoveTowards(rush.volume, 0.8f * rush01, dt * 1.2f);
      hiss.volume = Mathf.MoveTowards(hiss.volume, wakeVolume * wake01, dt * 1.5f);
    }
    /// <summary>Start (or keep) the leg music and the beds.</summary>
    public void SetVoyage(bool on){ musicOn = on; if (Unlocked) Apply(); }
    void Apply(){
      if (mus.clip && !mus.isPlaying){ mus.volume = 0f; mus.Play(); }
      if (lap.clip && !lap.isPlaying) lap.Play();
      if (hiss.clip && !hiss.isPlaying) hiss.Play();
      if (rush.clip && !rush.isPlaying) rush.Play();
    }
    /// <summary>Switch to a leg's music loop (crossfades if another is playing).</summary>
    public void SetLeg(int leg){
      legNow = Mathf.Clamp(leg, 0, legMusic.Length - 1); var c = legMusic[legNow];
      if (mus.clip == c) return;
      if (!Unlocked || !mus.isPlaying){ mus.clip = c; if (Unlocked && musicOn && c) mus.Play(); } else pendingClip = c;
      Debug.Log($"[Audio] leg music {(c ? c.name : "none")}");
    }
    /// <summary>The current-rush bed (0..1, while riding a current).</summary>
    public void SetCurrent(float k01) => rush01 = Mathf.Clamp01(k01);
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
    public string Status() => $"unlocked={(Unlocked ? 1 : 0)} music={(mus.isPlaying ? 1 : 0)} musVol={mus.volume:F2} lap={(lap.isPlaying ? 1 : 0)} hiss={hiss.volume:F2} rush={rush.volume:F2} track={(mus.clip ? mus.clip.name : "none")}";
  }
}
