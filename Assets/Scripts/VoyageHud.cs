using UnityEngine;
namespace AstralTide {
  /// <summary>The HUD (IMGUI, like Salt and Glory): title with Continue and the leg select, the star compass, sail gauge, fragment
  /// counter, hints, the leg-complete card, the final end card and the pause menu.</summary>
  public partial class Voyage {
    Texture2D white; GUIStyle txt;
    Rect R(float x, float y, float w, float h) => new Rect(x * S, y * S, w * S, h * S);
    void Box(Rect r, Color c){ var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }
    void Label(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter, FontStyle f = FontStyle.Normal){
      txt.fontSize = Mathf.RoundToInt(size * S); txt.alignment = a; txt.fontStyle = f;
      var o = GUI.color; GUI.color = new Color(0.05f, 0.03f, 0.12f, c.a * 0.55f); GUI.Label(new Rect(r.x + 1.5f * S, r.y + 1.5f * S, r.width, r.height), s, txt); GUI.color = c; GUI.Label(r, s, txt); GUI.color = o;
    }
    static readonly Color Gold = new Color(1f, 0.86f, 0.62f), Ink = new Color(1f, 0.96f, 0.9f);
    void OnGUI(){
      if (!white){ white = Texture2D.whiteTexture; }
      if (txt == null) txt = new GUIStyle(GUI.skin.label){ wordWrap = true, richText = false };
      float W = Screen.width / S;
      if (state == State.Title){ TitleCard(W); return; }
      Compass(W, Ink);
      if (state == State.Sail){
        // pause button (touch / mouse) and the sail gauge
        Box(RPause, new Color(0.1f, 0.07f, 0.22f, 0.45f)); Label(RPause, "II", 22, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        var sg = R(W - 70, 470, 22, 190); Box(sg, new Color(0.1f, 0.07f, 0.22f, 0.45f));
        float f = boat.sail; Box(new Rect(sg.x + 3 * S, sg.yMax - 3 * S - (sg.height - 6 * S) * f, sg.width - 6 * S, (sg.height - 6 * S) * f), new Color(1f, 0.88f, 0.62f, 0.85f));
        Label(R(W - 110, 662, 100, 26), $"Sail {Mathf.RoundToInt(boat.sail * 100)}%", 15, Ink);
        Label(R(W - 110, 440, 100, 26), $"{boat.speed * 1.94f:F0} kn", 15, Ink);
        if (boat.boost > 0.5f) Label(R(W - 140, 410, 130, 26), chain >= 2 || runChain >= 2 ? $"Boost x{Mathf.Max(chain, runChain)}" : "Boost", 15, new Color(0.7f, 1f, 1f));
        // fragments: five little stars, lit as you find them
        int mask = Progress.Frags(legIx);
        Label(R(20, 664, 200, 22), constDone ? $"{leg.name} constellation lit" : "Star fragments", 14, new Color(Ink.r, Ink.g, Ink.b, 0.85f), TextAnchor.MiddleLeft);
        for (int i = 0; i < leg.fragments.Length; i++){ bool on = (mask & (1 << i)) != 0; var rr = R(24 + i * 26, 642, 16, 16);
          if (fragIcon){ var o = GUI.color; GUI.color = on ? new Color(1f, 0.95f, 0.8f) : new Color(1, 1, 1, 0.3f); GUI.DrawTexture(R(14 + i * 26, 630, 34, 34), fragIcon, ScaleMode.ScaleToFit, true); GUI.color = o; }
          else { Box(rr, on ? Gold : new Color(1, 1, 1, 0.25f)); } }
        // first-time hints
        hintT += Time.deltaTime; if (!sailHintDone && boat.sail > 0.35f) sailHintDone = true; if (sailHintDone && !steerHintDone && hintT > 14f) steerHintDone = true;
        string hint = !sailHintDone ? "Raise the sail: W or Up · right trigger · drag up" :
          !steerHintDone ? (legIx == 0 ? "Steer toward the light: A / D or Left / Right · left stick · drag sideways.  Follow the star on the compass." : LegHint()) : null;
        if (hint != null) Label(R(0, 594, W, 40), hint, 19, Ink);
        if (turning || Time.time < fogMsgUntil){
          float fa = turning ? 1f : Mathf.Clamp01(fogMsgUntil - Time.time);
          Box(new Rect(0, 0, Screen.width, Screen.height), new Color(leg.fog.r, leg.fog.g, leg.fog.b, 0.28f * fa));
          Label(R(0, 560, W, 36), fogMsg, 21, new Color(1f, 1f, 1f, fa), TextAnchor.MiddleCenter, FontStyle.Italic);
        }
        if (Time.time - constAt < 4f){ float a = Mathf.Clamp01(4f - (Time.time - constAt)); Label(R(0, 120, W, 40), $"The {leg.name} constellation shines", 26, new Color(Gold.r, Gold.g, Gold.b, a), TextAnchor.MiddleCenter, FontStyle.Italic); }
      }
      if (flash > 0.01f) Box(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 0.92f, 0.75f, flash * 0.3f));
      if (state == State.LegDone || state == State.End) Card(W);
      if (paused) PauseMenu(W);
    }
    string LegHint() => leg.landmark switch {
      Landmark.Gate => "Ride the glowing currents: they carry you toward the Aurora Gate.",
      Landmark.FallenStar => "Meteors drift across the sea. A bump only slows you down.",
      Landmark.GlacierBell => "Catch star gusts one after another to chain a longer boost.",
      Landmark.WhiteStar => "Sail through the star buoys. Take the run of buoys in a row for a big boost.",
      Landmark.NebulaGate => "Twice the speed, twice the distance. Ride the swell and thread the star buoys.",
      _ => null };
    void TitleCard(float W){
      Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.08f, 0.05f, 0.18f, 0.3f));
      Label(R(0, 92, W, 90), "Astral Tide", 64, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
      Label(R(0, 174, W, 34), "A calm voyage across the starlit sea", 19, new Color(0.95f, 0.9f, 1f, 0.9f), TextAnchor.MiddleCenter, FontStyle.Italic);
      int cont = ContinueLeg(); bool started = Progress.Reached > 1 || Progress.Frags(0) != 0 || Progress.Best(0) > 0f;
      Label(R(0, 250, W, 34), started ? (Progress.Complete ? "Voyage complete · sail any leg again" : $"Continue: Leg {cont + 1} · {Leg.All[cont].name}") : "Choose where to set out", 20, Gold);
      for (int i = 0; i < Leg.All.Length; i++){
        var r = RowRect(i); bool open = i < Progress.Unlocked, sel = i == menuSel;
        Box(r, sel ? new Color(1f, 0.85f, 0.55f, 0.28f) : new Color(0.1f, 0.07f, 0.22f, open ? 0.45f : 0.25f));
        if (sel) Box(new Rect(r.x, r.y, 4 * S, r.height), Gold);
        int fr = Progress.Count(Progress.Frags(i)); float best = Progress.Best(i);
        string tail = open ? $"{fr}/{Leg.All[i].fragments.Length} fragments{(best > 0f ? "  ·  " + FormatTime(best) : "")}" : "locked";
        Label(new Rect(r.x + 16 * S, r.y, r.width - 32 * S, r.height), $"{i + 1}.  {Leg.All[i].name}", 19, open ? Ink : new Color(1, 1, 1, 0.4f), TextAnchor.MiddleLeft, sel ? FontStyle.Bold : FontStyle.Normal);
        Label(new Rect(r.x + 16 * S, r.y, r.width - 32 * S, r.height), tail, 16, open ? new Color(Gold.r, Gold.g, Gold.b, 0.9f) : new Color(1, 1, 1, 0.4f), TextAnchor.MiddleRight);
      }
      Label(R(0, 556, W, 30), Leg.All[menuSel].subtitle, 17, new Color(0.95f, 0.9f, 1f, 0.9f));
      float a = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 2.5f);
      Label(R(0, 600, W, 36), Application.isMobilePlatform ? "Tap a leg to set out" : "Up / Down to choose · Enter, Space or (A) to set out · or click a leg", 20, new Color(1f, 1f, 1f, a));
    }
    void Card(float W){
      float a = Mathf.Clamp01(stateT / 1.2f); bool fin = state == State.End;
      var card = R(W / 2 - 320, 170, 640, fin ? 380 : 330); Box(card, new Color(0.09f, 0.06f, 0.2f, 0.74f * a));
      Box(new Rect(card.x, card.y, card.width, 3 * S), new Color(1f, 0.85f, 0.55f, a));
      int fr = FragCount;
      if (!fin){
        var next = Leg.All[legIx + 1];
        Label(R(W / 2 - 320, 192, 640, 36), $"Leg {legIx + 1} · {leg.name}", 22, new Color(Gold.r, Gold.g, Gold.b, a));
        Label(R(W / 2 - 320, 230, 640, 60), $"You reached the {leg.landmarkName}", 38, new Color(1f, 0.97f, 0.92f, a), TextAnchor.MiddleCenter, FontStyle.Bold);
        Label(R(W / 2 - 320, 296, 640, 30), $"Time {FormatTime(legTime)}   ·   Star fragments {fr} / {leg.fragments.Length}{(constDone ? "  ·  constellation lit" : "")}", 19, new Color(1f, 1f, 1f, a));
        Label(R(W / 2 - 320, 340, 640, 32), $"The way opens: Leg {legIx + 2} · {next.name}", 21, new Color(0.85f, 0.95f, 1f, a), TextAnchor.MiddleCenter, FontStyle.Italic);
        Label(R(W / 2 - 320, 372, 640, 28), "Progress saved", 15, new Color(1f, 1f, 1f, 0.7f * a));
        if (stateT > 1.2f) Label(R(W / 2 - 320, 418, 640, 34), Application.isMobilePlatform ? "Tap to sail on" : "Enter · click · (A) to sail on", 19, new Color(1f, 1f, 1f, a * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 2.5f))));
      } else {
        int all = 0, tot = 0; for (int i = 0; i < Leg.All.Length; i++){ all += Progress.Count(Progress.Frags(i)); tot += Leg.All[i].fragments.Length; }
        Label(R(W / 2 - 320, 192, 640, 36), $"Leg {legIx + 1} · {leg.name}", 22, new Color(Gold.r, Gold.g, Gold.b, a));
        Label(R(W / 2 - 320, 230, 640, 64), "The voyage is complete", 42, new Color(1f, 0.97f, 0.92f, a), TextAnchor.MiddleCenter, FontStyle.Bold);
        Label(R(W / 2 - 320, 300, 640, 30), $"You reached the {leg.landmarkName} in {FormatTime(legTime)}", 20, new Color(1f, 1f, 1f, a));
        Label(R(W / 2 - 320, 336, 640, 30), $"Star fragments across the voyage: {all} / {tot}", 19, new Color(1f, 1f, 1f, a));
        Label(R(W / 2 - 320, 376, 640, 56), "Thank you for sailing Astral Tide.", 20, new Color(0.85f, 0.95f, 1f, a), TextAnchor.MiddleCenter, FontStyle.Italic);
        if (stateT > 1.2f) Label(R(W / 2 - 320, 470, 640, 34), Application.isMobilePlatform ? "Tap to return to the harbour" : "Enter · click · (A) to return to the leg select", 19, new Color(1f, 1f, 1f, a * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 2.5f))));
      }
    }
    void PauseMenu(float W){
      Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.06f, 0.04f, 0.14f, 0.55f));
      Label(R(0, 270, W, 60), "Paused", 44, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
      foreach (var (r, s) in new[]{ (RResume, "Resume   (Esc · Start · A)"), (RRestart, "Restart the leg   (R · Y)"), (RLegs, "Leg select   (L · B)") }){
        Box(r, new Color(0.1f, 0.07f, 0.22f, 0.6f)); Label(r, s, 18, Ink);
      }
    }
    /// <summary>The thin star compass: +-75 degrees around the bow, a tick every 15, the goal marked by a four-point star that
    /// brightens (and chimes) when you're lined up; off the strip it waits at the edge as an arrow.</summary>
    void Compass(float W, Color ink){
      float cx = W / 2f, half = 300f, y = 40f, span = 75f;
      if (compassStrip){
        float w = span * 2f / 360f; GUI.DrawTextureWithTexCoords(R(cx - half, y - 12, half * 2, 26), compassStrip, new Rect(boat.heading / 360f - w / 2f, 0f, w, 1f), true);
      } else Box(R(cx - half, y, half * 2f, 2), new Color(1f, 1f, 1f, 0.55f));
      for (int a = -180; a < 180 && !compassStrip; a += 15){
        float d = Mathf.DeltaAngle(boat.heading, a); if (Mathf.Abs(d) > span) continue;
        float x = cx + d / span * half; bool major = a % 45 == 0;
        Box(R(x - 0.75f, y - (major ? 7 : 4), 1.5f, major ? 16 : 10), new Color(1f, 1f, 1f, major ? 0.7f : 0.4f));
      }
      Box(R(cx - 1, y + 6, 2, 10), new Color(1f, 1f, 1f, 0.9f));
      float err = HeadingErr, gx = cx + Mathf.Clamp(err, -span, span) / span * half;
      float glow = lined ? 1f : Mathf.Clamp01(1f - (Mathf.Abs(err) - 5f) / 25f) * 0.5f;
      var gold = new Color(1f, 0.85f, 0.55f);
      if (Mathf.Abs(err) <= span){
        float s = 15f + glow * 6f;
        if (compassMarker){
          float ms = 13f + glow * 5f + (lined ? Mathf.Sin(Time.unscaledTime * 6f) * 1.5f : 0f); var o = GUI.color; GUI.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * glow);
          GUI.DrawTexture(R(gx - ms, y - 12 - ms * 1.6f, ms * 2, ms * 2), compassMarker, ScaleMode.ScaleToFit, true); GUI.color = o;
        } else {
          Box(R(gx - s * 0.12f, y - s + 1, s * 0.24f, s * 2), new Color(gold.r, gold.g, gold.b, 0.95f));
          Box(R(gx - s * 0.6f, y - s * 0.12f + 1, s * 1.2f, s * 0.24f), new Color(gold.r, gold.g, gold.b, 0.95f));
          if (glow > 0.05f) Box(R(gx - s * 0.5f, y - s * 0.5f + 1, s, s), new Color(1f, 0.95f, 0.8f, 0.25f * glow));
        }
      } else Label(R(err < 0 ? cx - half - 40 : cx + half + 4, y - 14, 36, 30), err < 0 ? "<<" : ">>", 20, gold, TextAnchor.MiddleCenter, FontStyle.Bold);
      Label(R(cx - 200, y + 16, 400, 24), $"{leg.landmarkName}  ·  {(GoalDist >= 1000f ? (GoalDist / 1000f).ToString("F1") + " km" : Mathf.RoundToInt(GoalDist / 10f) * 10 + " m")}", 15, new Color(ink.r, ink.g, ink.b, 0.85f));
      if (lined && state == State.Sail) Label(R(cx - 200, y + 36, 400, 22), "on course", 14, new Color(gold.r, gold.g, gold.b, 0.9f), TextAnchor.MiddleCenter, FontStyle.Italic);
    }
  }
}
