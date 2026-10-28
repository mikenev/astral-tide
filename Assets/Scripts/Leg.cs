using UnityEngine;
namespace AstralTide {
  /// <summary>One leg of the voyage: its sky palette at the start and at the landmark (the sky shifts as you sail), where it
  /// starts and where the landmark stands, the star gusts along the way and the course corridor that keeps you from getting lost.
  /// M1 has Dusk Shallows only; M2 adds the other four with the same shape.</summary>
  public class Leg {
    public string name, landmark, subtitle;
    public Color top0, mid0, low0, hor0, top1, mid1, low1, hor1, glow, fog, water;
    public float stars0, stars1;
    public Vector3 start, goal; public float startHeading;
    public Vector3[] gusts;
    public float fogStart = 150f, fogFull = 215f, turnaround = 190f, behind = 70f, arriveDist = 75f;
    public Vector3 Dir => (goal - start).normalized;
    public float Length => (goal - start).magnitude;

    static Color C(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
    public static readonly Leg DuskShallows = new Leg {
      name = "Dusk Shallows", landmark = "Lantern Spire", subtitle = "Lavender twilight. The first stars are waking.",
      // warm peach horizon under lavender, deepening to violet; by the spire it is nearly night
      hor0 = C(0xF6B99C), low0 = C(0xD6A2C8), mid0 = C(0x8E76BE), top0 = C(0x332C66),
      hor1 = C(0xD892A8), low1 = C(0x9E7CC0), mid1 = C(0x5A4A98), top1 = C(0x15123A),
      glow = C(0xFFD58A), fog = C(0xB9A9D6), water = C(0xD4CCF0),
      stars0 = 0.12f, stars1 = 1f,
      start = Vector3.zero, startHeading = 0f, goal = new Vector3(230f, 0f, 1900f),
      gusts = new[]{ new Vector3(10f, 0, 330f), new Vector3(70f, 0, 620f), new Vector3(85f, 0, 900f), new Vector3(150f, 0, 1180f), new Vector3(165f, 0, 1460f), new Vector3(215f, 0, 1700f) },
    };
  }
}
