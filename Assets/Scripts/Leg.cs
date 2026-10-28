using UnityEngine;
namespace AstralTide {
  public enum Landmark { Spire, Gate, FallenStar, GlacierBell, WhiteStar }

  /// <summary>One leg of the voyage. Every leg uses the same sea and sky shaders, recoloured: the palette at the start and at
  /// the landmark (the sky shifts as you sail), the swell, sky features (aurora, shooting stars, nebula), the course, and what
  /// lies along it: star gusts, currents, drifting meteors, ice floes, star buoys and five star fragments whose constellation
  /// lights up in the sky.</summary>
  public class Leg {
    public int index; public string name, landmarkName, subtitle, musicKey, panoKey; public Landmark landmark;
    public Color top0, mid0, low0, hor0, top1, mid1, low1, hor1, glow, fog, water, seaTint = new Color(1, 1, 1, 0);
    public float stars0, stars1, aurora, shoot, nebula, gloss = 0.4f; public Color auroraA, auroraB, nebA, nebB, currentCol;
    public float waveAmp, waveLen = 30f, waveSpeed = 0.8f, waveDir = 20f, ripple = 0.06f;
    public bool usePano;
    public Vector3 start, goal; public float startHeading;
    public Vector3[] gusts = new Vector3[0], fragments = new Vector3[0], buoys = new Vector3[0];
    public Vector3[] currents = new Vector3[0];       // pairs: segment start, end (the stream flows start -> end)
    public float currentWidth = 16f, currentPush = 7f;
    public int meteors, floes; public float meteorDrift = 2.5f;
    public bool gustChains;
    public Vector2[] constellation = new Vector2[0];  // (azimuth from the course direction, elevation), degrees; one star per fragment (low enough to sit in the chase view)
    public float fogStart = 150f, fogFull = 215f, turnaround = 190f, behind = 70f, arriveDist = 75f, lanternY = 50f;
    public Vector3 Dir => (goal - start).normalized;
    public float Length => (goal - start).magnitude;

    static Color C(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
    /// <summary>Points along the course: (fraction of the way, metres to the right of the line).</summary>
    Vector3 At(float f, float side){ var d = (goal - start).normalized; return start + d * Length * f + new Vector3(d.z, 0, -d.x) * side; }
    Vector3[] Line(params (float f, float s)[] p){ var r = new Vector3[p.Length]; for (int i = 0; i < p.Length; i++) r[i] = At(p[i].f, p[i].s); return r; }

    public static readonly Leg[] All = Build();
    static Leg[] Build(){
      var dusk = new Leg {
        index = 0, name = "Dusk Shallows", landmarkName = "Lantern Spire", landmark = Landmark.Spire, musicKey = "dusk_shallows_loop", panoKey = "AT_DuskShallows_SkyPano", usePano = true,
        subtitle = "Lavender twilight. The first stars are waking.",
        hor0 = C(0xF6B99C), low0 = C(0xD6A2C8), mid0 = C(0x8E76BE), top0 = C(0x332C66), hor1 = C(0xD892A8), low1 = C(0x9E7CC0), mid1 = C(0x5A4A98), top1 = C(0x15123A),
        glow = C(0xFFD58A), fog = C(0xB9A9D6), water = C(0xD4CCF0), stars0 = 0.12f, stars1 = 1f,
        waveAmp = 0.12f, waveLen = 22f, waveSpeed = 0.7f, waveDir = 10f,
        start = Vector3.zero, goal = new Vector3(230f, 0f, 1900f),
        constellation = new[]{ new Vector2(-24, 17), new Vector2(-14, 25), new Vector2(-2, 28), new Vector2(10, 23), new Vector2(18, 16) },
      };
      dusk.gusts = new[]{ new Vector3(10f, 0, 330f), new Vector3(70f, 0, 620f), new Vector3(85f, 0, 900f), new Vector3(150f, 0, 1180f), new Vector3(165f, 0, 1460f), new Vector3(215f, 0, 1700f) };
      dusk.fragments = dusk.Line((0.12f, -45f), (0.3f, 40f), (0.5f, -35f), (0.68f, 50f), (0.84f, -30f));

      var aurora = new Leg {
        index = 1, name = "Aurora Reach", landmarkName = "Aurora Gate", landmark = Landmark.Gate, musicKey = "aurora_reach_loop", panoKey = "AT_AuroraReach_SkyPano",
        subtitle = "Ribbons of light overhead. Ride the glowing currents.",
        hor0 = C(0x5FB7B0), low0 = C(0x2E7C8A), mid0 = C(0x1C4E6E), top0 = C(0x0C1E3C), hor1 = C(0x4FA6A8), low1 = C(0x22637A), mid1 = C(0x143C5E), top1 = C(0x081430),
        glow = C(0x9CFFD8), fog = C(0x7FB8B8), water = C(0xB8E6E0), stars0 = 0.5f, stars1 = 0.9f, aurora = 1.1f, auroraA = C(0x3CFFA0), auroraB = C(0x38C8FF), currentCol = C(0x40FFD0) * 0.9f,
        waveAmp = 0.4f, waveLen = 34f, waveSpeed = 0.8f, waveDir = 35f,
        start = Vector3.zero, goal = new Vector3(-180f, 0f, 2000f), arriveDist = 24f, lanternY = 24f,   // sail into the gate's opening
        constellation = new[]{ new Vector2(-26, 10), new Vector2(-18, 22), new Vector2(0, 27), new Vector2(18, 22), new Vector2(26, 10) },
      };
      aurora.currents = aurora.Line((0.06f, -10f), (0.2f, 25f), (0.2f, 25f), (0.34f, -15f), (0.42f, 30f), (0.58f, 10f), (0.58f, 10f), (0.72f, -25f), (0.78f, 0f), (0.92f, 5f));
      aurora.gusts = aurora.Line((0.5f, -60f), (0.86f, 40f));
      aurora.fragments = aurora.Line((0.1f, 50f), (0.27f, -40f), (0.46f, 55f), (0.66f, -45f), (0.85f, 35f));

      var ember = new Leg {
        index = 2, name = "Ember Drift", landmarkName = "Fallen Star", landmark = Landmark.FallenStar, musicKey = "ember_drift_loop", panoKey = "AT_EmberDrift_SkyPano",
        subtitle = "Deep indigo and falling stars. Steer around the drifting meteors.",
        hor0 = C(0xB0577A), low0 = C(0x5A2F6E), mid0 = C(0x2A1C55), top0 = C(0x0E0A2A), hor1 = C(0xC8664E), low1 = C(0x5C2A5C), mid1 = C(0x221646), top1 = C(0x0A0720),
        glow = C(0xFFB070), fog = C(0x8A6A9A), water = C(0xC8B6E6), stars0 = 0.7f, stars1 = 1f, shoot = 0.85f,
        waveAmp = 0.5f, waveLen = 30f, waveSpeed = 0.85f, waveDir = -25f,
        start = Vector3.zero, goal = new Vector3(120f, 0f, 2100f), meteors = 14, meteorDrift = 2.2f,
        constellation = new[]{ new Vector2(-30, 26), new Vector2(-16, 21), new Vector2(-4, 17), new Vector2(8, 13), new Vector2(24, 9) },
      };
      ember.gusts = ember.Line((0.3f, 0f), (0.62f, -30f), (0.9f, 20f));
      ember.fragments = ember.Line((0.14f, -40f), (0.32f, 45f), (0.5f, -50f), (0.7f, 35f), (0.86f, -25f));

      var frost = new Leg {
        index = 3, name = "Frost Mirror", landmarkName = "Glacier Bell", landmark = Landmark.GlacierBell, musicKey = "frost_mirror_loop", panoKey = "AT_FrostMirror_SkyPano",
        subtitle = "Icy blue and silver. The water is perfectly still.",
        hor0 = C(0xDDE8F4), low0 = C(0xA8C4E2), mid0 = C(0x6C8CBC), top0 = C(0x26385E), hor1 = C(0xC8DCF0), low1 = C(0x8CAAD6), mid1 = C(0x4E6A9C), top1 = C(0x18264A),
        glow = C(0xCFF0FF), fog = C(0xD8E4F0), water = C(0xE8F0FA), stars0 = 0.45f, stars1 = 0.95f, gloss = 0.6f,
        waveAmp = 0f, ripple = 0.015f,
        start = Vector3.zero, goal = new Vector3(60f, 0f, 2000f), floes = 22, gustChains = true,
        constellation = new[]{ new Vector2(-12, 13), new Vector2(-14, 21), new Vector2(0, 27), new Vector2(14, 21), new Vector2(12, 13) },
      };
      frost.gusts = frost.Line((0.18f, 0f), (0.21f, 4f), (0.24f, 8f), (0.27f, 10f), (0.3f, 8f), (0.33f, 4f), (0.55f, -20f), (0.58f, -18f), (0.61f, -14f), (0.64f, -8f), (0.67f, -2f), (0.7f, 4f), (0.73f, 8f));
      frost.fragments = frost.Line((0.12f, 45f), (0.4f, -45f), (0.48f, 50f), (0.8f, -40f), (0.9f, 40f));

      var cosmic = new Leg {
        index = 4, name = "The Cosmic Sea", landmarkName = "White Star", landmark = Landmark.WhiteStar, musicKey = "cosmic_sea_loop", panoKey = "AT_CosmicSea_SkyPano",
        subtitle = "A purple nebula over a rolling cosmic ocean. Sail the star buoys.",
        hor0 = C(0x6A3CA0), low0 = C(0x3A1E6E), mid0 = C(0x1E1048), top0 = C(0x08051C), hor1 = C(0x7E46B4), low1 = C(0x40207A), mid1 = C(0x1E104E), top1 = C(0x060418),
        glow = C(0xF4F8FF), fog = C(0x6A4E9A), water = C(0xD0C0FF), seaTint = new Color(0.9f, 0.9f, 1.05f, 0.18f), stars0 = 0.9f, stars1 = 1.1f,
        nebula = 1f, nebA = C(0x7A2CC8), nebB = C(0xFF6AD0), gloss = 0.9f,
        waveAmp = 1.25f, waveLen = 48f, waveSpeed = 0.75f, waveDir = 0f, ripple = 0.04f,
        start = Vector3.zero, goal = new Vector3(0f, 0f, 2100f), lanternY = 60f,
        constellation = new[]{ new Vector2(-20, 15), new Vector2(-12, 23), new Vector2(0, 19), new Vector2(12, 23), new Vector2(20, 15) },
      };
      cosmic.buoys = cosmic.Line((0.1f, 0f), (0.18f, 12f), (0.26f, -10f), (0.34f, 8f), (0.42f, -6f), (0.6f, 0f), (0.625f, 0f), (0.65f, 0f), (0.675f, 0f), (0.7f, 0f), (0.725f, 0f), (0.75f, 0f));   // the last seven (about 52 m apart) are the boost run
      cosmic.fragments = cosmic.Line((0.14f, -45f), (0.3f, 40f), (0.48f, -45f), (0.56f, 45f), (0.9f, -35f));
      return new[]{ dusk, aurora, ember, frost, cosmic };
    }
  }

  /// <summary>The swell, on the CPU: exactly the sum in AstralWaves.hlsl, so the ship rides the waves you see.</summary>
  public static class Waves {
    public static float amp, len = 30f, speed = 1f, dir, time;
    static readonly Vector4[] C = { new(1f, 1f, 0f, 0f), new(0.55f, 0.62f, 0.61f, 1.7f), new(0.35f, 0.41f, -0.87f, 4.1f), new(0.18f, 0.27f, 1.4f, 2.3f) };
    public static float H(float x, float z) => H(x, z, out _);
    public static float H(float x, float z, out Vector2 grad){
      float h = 0f; grad = Vector2.zero; if (amp <= 0.0001f) return 0f;
      foreach (var c in C){
        float a = dir + c.z; var d = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        float k = 2f * Mathf.PI / (len * c.y), w = Mathf.Sqrt(9.81f * k) * speed;
        float ph = k * (d.x * x + d.y * z) + w * time + c.w; float A = amp * c.x;
        h += A * Mathf.Sin(ph); grad += A * k * Mathf.Cos(ph) * d;
      }
      return h;
    }
  }

  /// <summary>Progress in PlayerPrefs: legs unlocked, fragments found per leg (bit mask), best times; the voyage-complete flag.</summary>
  public static class Progress {
    public static int Unlocked { get => Mathf.Clamp(PlayerPrefs.GetInt("at.unlocked", 1), 1, Leg.All.Length); set { PlayerPrefs.SetInt("at.unlocked", Mathf.Clamp(value, 1, Leg.All.Length)); PlayerPrefs.Save(); } }
    public static int Frags(int leg) => PlayerPrefs.GetInt("at.frag." + leg, 0);
    public static void AddFrag(int leg, int i){ PlayerPrefs.SetInt("at.frag." + leg, Frags(leg) | (1 << i)); PlayerPrefs.Save(); }
    public static float Best(int leg) => PlayerPrefs.GetFloat("at.best." + leg, 0f);
    public static void Finish(int leg, float t){ float b = Best(leg); if (b <= 0f || t < b) PlayerPrefs.SetFloat("at.best." + leg, t); if (leg + 2 > Unlocked) Unlocked = Mathf.Min(leg + 2, Leg.All.Length); if (leg == Leg.All.Length - 1) PlayerPrefs.SetInt("at.complete", 1); PlayerPrefs.Save(); }
    public static bool Complete => PlayerPrefs.GetInt("at.complete", 0) == 1;
    public static int Count(int mask){ int n = 0; for (; mask != 0; mask &= mask - 1) n++; return n; }
    public static void Clear(){ PlayerPrefs.DeleteKey("at.unlocked"); PlayerPrefs.DeleteKey("at.complete"); for (int i = 0; i < Leg.All.Length; i++){ PlayerPrefs.DeleteKey("at.frag." + i); PlayerPrefs.DeleteKey("at.best." + i); } PlayerPrefs.Save(); }
  }
}
