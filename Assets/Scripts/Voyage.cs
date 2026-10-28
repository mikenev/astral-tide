using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace AstralTide {
  /// <summary>Astral Tide M1: one calm leg (Dusk Shallows) you can't fail. Title card, then sail toward the Lantern Spire: steer
  /// and set the sail, a thin star compass at the top marks the goal (soft chime when lined up), a gentle pull eases you back when
  /// you drift well off course and fog past the edges of the course turns you around. Star gusts give a short burst of speed.
  /// Arriving plays a swell of light and music and shows the end card. Keyboard, gamepad and touch (Salt and Glory's input code).
  /// Everything visual is procedural until Art Dev's set is merged (art is loaded by name in BuildWeb, with fallbacks).</summary>
  public class Voyage : MonoBehaviour {
    // ---- scene refs (set by BuildWeb.SetupScene) ----
    public Camera cam; public Transform sea, sky; public Boat boat; public Transform spire, spireHalo, spireMirror; public Renderer lanternGlow;
    public Mesh quadMesh; public Material glowMat, gustMat, haloMat, fogMat, seaMat, spireMat;
    public Texture2D skyPano, starfield, compassStrip, compassMarker;   // Art Dev (optional; null = procedural)
    public Material skyboxMat; public Light spireLight, boatLight; public float lanternY = 52f; public bool realModels;

    enum State { Title, Sail, Arrive, End }
    State state = State.Title; float stateT; bool paused;
    readonly Leg leg = Leg.DuskShallows;
    float legTime; int gustsCaught; bool[] gustTaken; Transform[] gustT; ParticleSystem[] gustPS; ParticleSystem motesPS, burstPS;
    float camYaw, lookYaw, lookPitch, lastLook = -10f; Vector3 camPos;
    // guidance
    bool lined, chimeArmed = true; float offSince; int chimes; bool pulling, turning; float pullAmt; string fogMsg; float fogMsgUntil;
    float hintT; bool sailHintDone, steerHintDone, sailPending; float sailStill = 99f; float lastSail;
    readonly Vector4[] wake = new Vector4[8]; float wakeT;
    float flash; float endShownAt = -1f;

    float S => Screen.height / 720f;
    float Along => Vector3.Dot(boat.Pos - leg.start, leg.Dir);
    float Lateral { get { var d = leg.Dir; var r = boat.Pos - leg.start; return r.x * d.z - r.z * d.x; } }   // + = right of the line
    float GoalBearing { get { var d = leg.goal - boat.Pos; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; } }
    float HeadingErr => Mathf.DeltaAngle(boat.heading, GoalBearing);   // + = goal is to starboard
    float GoalDist => Vector2.Distance(new Vector2(boat.Pos.x, boat.Pos.z), new Vector2(leg.goal.x, leg.goal.z));
    float Progress => Mathf.Clamp01(Along / leg.Length);

    void Start(){
      Application.targetFrameRate = 60;
      boat.Reset(leg.start, leg.startHeading, 0f); camYaw = leg.startHeading; camPos = boat.Pos;
      spire.position = leg.goal; if (spireMirror) spireMirror.position = leg.goal;
      if (skyPano){ Shader.SetGlobalTexture("_AT_Pano", skyPano); Shader.SetGlobalFloat("_AT_PanoMix", 1f); } else Shader.SetGlobalFloat("_AT_PanoMix", 0f);
      if (starfield){ Shader.SetGlobalTexture("_AT_Starfield", starfield); Shader.SetGlobalFloat("_AT_StarfieldMix", 1f); } else Shader.SetGlobalFloat("_AT_StarfieldMix", 0f);
      Debug.Log($"[AT] art: pano={(skyPano ? 1 : 0)} starfield={(starfield ? 1 : 0)} compass={(compassStrip ? 1 : 0)} marker={(compassMarker ? 1 : 0)} models={(realModels ? 1 : 0)} wake={(boat.wakeTrail ? 1 : 0)}");
      var dir = leg.Dir; Shader.SetGlobalVector("_AT_Course", new Vector4(leg.start.x, leg.start.z, dir.x, dir.z));
      Shader.SetGlobalVector("_AT_FogBand", new Vector4(leg.fogStart, leg.fogFull, leg.behind, 0));
      Shader.SetGlobalColor("_AT_FogCol", leg.fog);
      MakeGusts(); MakeFogBanks();
      motesPS = MakePS("WakeMotes", 400); burstPS = MakePS("ArrivalBurst", 600);
      ApplySky(0f); Place();
      Debug.Log("[AT] ready");
    }

    // ---------- world dressing ----------
    ParticleSystem MakePS(string n, int max){
      var go = new GameObject(n); var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var m = ps.main; m.loop = false; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = max; m.gravityModifier = 0f;
      var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
      var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
      g.SetKeys(new[]{ new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[]{ new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
      col.color = g;
      go.GetComponent<ParticleSystemRenderer>().sharedMaterial = glowMat; ps.Play(); return ps;
    }
    static void Emit(ParticleSystem ps, Vector3 p, Vector3 v, float size, float life, Color c) =>
      ps.Emit(new ParticleSystem.EmitParams{ position = p, velocity = v, startSize = size, startLifetime = life, startColor = c, applyShapeToPosition = false }, 1);
    GameObject Billboard(string n, Vector3 p, float size, Material m, Transform parent = null){
      var go = new GameObject(n); go.transform.SetParent(parent, false); go.transform.position = p; go.transform.localScale = Vector3.one * size;
      go.AddComponent<MeshFilter>().sharedMesh = quadMesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m;
      r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; return go;
    }
    void MakeGusts(){
      int n = leg.gusts.Length; gustTaken = new bool[n]; gustT = new Transform[n]; gustPS = new ParticleSystem[n];
      for (int i = 0; i < n; i++){
        var core = Billboard("Gust" + i, leg.gusts[i] + Vector3.up * 3f, 9f, gustMat); gustT[i] = core.transform;
        gustPS[i] = MakePS("GustSparkles" + i, 200);
      }
    }
    void MakeFogBanks(){   // soft lilac fog billboards along both edges of the course and behind the start
      var root = new GameObject("FogBanks").transform; var d = leg.Dir; var rt = new Vector3(d.z, 0, -d.x);
      for (int i = -2; i < 26; i++) for (int side = -1; side <= 1; side += 2){
        float a = i * leg.Length / 22f; var p = leg.start + d * a + rt * side * (leg.fogFull + 25f + Mathf.Sin(i * 1.7f) * 15f);
        Billboard("Fog", p + Vector3.up * 14f, 120f, fogMat, root);
      }
      for (int i = -4; i <= 4; i++) Billboard("Fog", leg.start - d * (leg.behind * 1.6f + 40f) + rt * i * 45f + Vector3.up * 14f, 120f, fogMat, root);
    }

    // ---------- input (Salt and Glory's pointer cache: taps that begin and end between frames are kept) ----------
    readonly Dictionary<int, (Vector2 start, float sail0, bool pause)> ptrs = new();
    float lastTouch = -10f; Vector2 twoPrev; bool twoOn;
    Rect RPause => new Rect(18 * S, 14 * S, 64 * S, 52 * S);
    readonly List<(int id, Vector2 p, bool began, bool down)> ptrList = new(); int ptrFrame = -1; readonly double[] touchSeen = new double[16];
    List<(int id, Vector2 p, bool began, bool down)> Pointers(){
      if (ptrFrame == Time.frameCount) return ptrList;
      bool seed = ptrFrame < 0; ptrFrame = Time.frameCount; ptrList.Clear();
      var ts = Touchscreen.current;
      if (ts != null){ int i = -1; foreach (var t in ts.touches){
        i++; int tid = t.touchId.ReadValue(); bool down = t.press.isPressed, began = t.press.wasPressedThisFrame;
        double st0 = t.startTime.ReadValue();
        bool fresh = i < touchSeen.Length && st0 > 0 && st0 != touchSeen[i]; if (i < touchSeen.Length && st0 > 0) touchSeen[i] = st0;
        if (!down && !t.press.wasReleasedThisFrame){
          if (!fresh || seed) continue;
          lastTouch = Time.unscaledTime; var sp = t.startPosition.ReadValue(); var ep = t.position.ReadValue();
          ptrList.Add((tid, new Vector2(sp.x, Screen.height - sp.y), true, true)); ptrList.Add((tid, new Vector2(ep.x, Screen.height - ep.y), false, false));
          continue;
        }
        lastTouch = Time.unscaledTime;
        var p = t.position.ReadValue(); ptrList.Add((tid, new Vector2(p.x, Screen.height - p.y), began || (fresh && !seed), down));
      } }
      var ms = Mouse.current;
      if (ms != null && Time.unscaledTime - lastTouch > 0.6f && (ms.leftButton.isPressed || ms.leftButton.wasReleasedThisFrame)){
        var p = ms.position.ReadValue(); ptrList.Add((-1, new Vector2(p.x, Screen.height - p.y), ms.leftButton.wasPressedThisFrame, ms.leftButton.isPressed));
      }
      return ptrList;
    }
    bool AnyPress(out string src){
      var kb = Keyboard.current; src = "keyboard";
      if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) return true;
      var gp = Gamepad.current; src = "gamepad"; if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame)) return true;
      foreach (var p in Pointers()) if (p.began){ src = p.id < 0 ? "mouse" : "touch"; return true; }
      return false;
    }

    void HandleInput(float dt, out float steer){
      var kb = Keyboard.current; var gp = Gamepad.current; var ms = Mouse.current;
      steer = 0f; float sailDelta = 0f; float lookX = 0f, lookY = 0f;
      if (kb != null){
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) sailDelta += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) sailDelta -= 1f;
      }
      if (gp != null){
        float x = gp.leftStick.x.ReadValue(); if (Mathf.Abs(x) > 0.2f) steer += x;
        sailDelta += gp.rightTrigger.ReadValue() - gp.leftTrigger.ReadValue();
        var rs = gp.rightStick.ReadValue(); if (rs.magnitude > 0.2f){ lookX += rs.x * 90f * dt; lookY += rs.y * 40f * dt; }
      }
      if (ms != null && ms.rightButton.isPressed){ var d = ms.delta.ReadValue(); lookX += d.x * 0.15f; lookY += d.y * 0.1f; }
      // touch / left mouse: one finger drags to steer (sideways) and set the sail (up / down); two fingers look around; pause button
      var list = Pointers(); int downCount = 0; Vector2 sum = Vector2.zero; var seen = new HashSet<int>();
      foreach (var (id, p, began, down) in list){ if (down && id >= 0){ downCount++; sum += p; } }
      if (downCount >= 2){
        var mid = sum / downCount; if (twoOn){ var d = mid - twoPrev; lookX += d.x * 0.15f / S; lookY -= d.y * 0.1f / S; } twoPrev = mid;
        if (!twoOn) Debug.Log("[AT] touch look start"); twoOn = true; ptrs.Clear();
      } else {
        twoOn = false;
        foreach (var (id, p, began, down) in list){
          seen.Add(id);
          if (began){
            bool pz = RPause.Contains(p); ptrs[id] = (p, boat.sail, pz); string src = id < 0 ? "mouse" : "touch";
            if (pz){ SetPaused(true, src); return; }
            Debug.Log($"[AT] {src} drag start");
          }
          if (!ptrs.TryGetValue(id, out var st)) continue;
          if (!down){ ptrs.Remove(id); continue; }
          var dd = p - st.start;
          steer += Mathf.Clamp(dd.x / (110f * S), -1f, 1f);
          if (Mathf.Abs(dd.y) > 12f * S) boat.sail = Mathf.Clamp01(st.sail0 - (dd.y - Mathf.Sign(dd.y) * 12f * S) / (220f * S));
        }
        foreach (var k in new List<int>(ptrs.Keys)) if (!seen.Contains(k)) ptrs.Remove(k);
      }
      steer = Mathf.Clamp(steer, -1f, 1f);
      if (sailDelta != 0f) boat.sail = Mathf.Clamp01(boat.sail + sailDelta * 0.5f * dt);
      lookYaw = Mathf.Clamp(lookYaw + lookX, -110f, 110f); lookPitch = Mathf.Clamp(lookPitch + lookY, -12f, 25f);
      if (Mathf.Abs(lookX) + Mathf.Abs(lookY) > 0.001f || twoOn || (ms != null && ms.rightButton.isPressed)) lastLook = Time.unscaledTime;
      // ease back behind the ship a moment after you stop looking
      if (Time.unscaledTime - lastLook > 1.5f){ lookYaw = Mathf.MoveTowards(lookYaw, 0f, 25f * dt); lookPitch = Mathf.MoveTowards(lookPitch, 0f, 10f * dt); }
    }
    void SetPaused(bool on, string src){
      paused = on; Time.timeScale = on ? 0f : 1f; ptrs.Clear(); VoyageAudio.I?.Pause(on);
      Debug.Log($"[AT] {(on ? "paused" : "resumed")} by {src}");
    }

    // ---------- loop ----------
    void Update(){
      var kb = Keyboard.current; var gp = Gamepad.current;
      float dt = Time.deltaTime; stateT += dt;
      bool pauseKey = (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.startButton.wasPressedThisFrame);
      if (paused){
        if (kb != null && kb.rKey.wasPressedThisFrame){ SetPaused(false, "restart"); Restart("pause menu"); return; }
        if (pauseKey || AnyPress(out _)) SetPaused(false, "input"); return;
      }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
      DevKeys(kb);
#endif
      switch (state){
        case State.Title:
          if (AnyPress(out var src)){ state = State.Sail; stateT = 0f; legTime = 0f; VoyageAudio.I?.SetVoyage(true); Debug.Log($"[AT] voyage start by {src}: {leg.name}"); }
          boat.rudder = 0f; boat.Tick(dt); break;
        case State.Sail:
          if (pauseKey){ SetPaused(true, "key"); return; }
          legTime += dt; Sail(dt); break;
        case State.Arrive:
          ArriveTick(dt); break;
        case State.End:
          boat.rudder = 0f; boat.sail = Mathf.MoveTowards(boat.sail, 0f, dt * 0.3f); boat.Tick(dt, 0f);
          if (stateT > 1.2f && AnyPress(out var s2)) Restart(s2);
          break;
      }
      Gusts(dt); Wake(dt);
      VoyageAudio.I?.SetWake(boat.speed / boat.maxSpeed);
      flash = Mathf.MoveTowards(flash, 0f, dt * 0.35f);
      ApplySky(state == State.Title ? 0f : Progress);
      Place();
    }
    void LateUpdate(){ UpdateCamera(Time.deltaTime); }

    void Sail(float dt){
      HandleInput(dt, out float steer);
      if (paused) return;
      // sail filling: a puff when you raise the sail after it has been still for a moment
      if (boat.sail > lastSail + 0.0001f){
        if (sailStill > 1.2f) sailPending = true; sailStill = 0f;
        if (sailPending && boat.sail > 0.08f){ sailPending = false; bool pl = VoyageAudio.I != null && VoyageAudio.I.Play("sail"); Debug.Log($"[AT] sail filling at {boat.sail:F2}{(pl ? "" : " (silent)")}"); }
      } else sailStill += dt;
      lastSail = boat.sail;
      // guidance: fog past the edges turns you around (takes the helm), a gentle pull when well off course (you can steer against it)
      float lat = Lateral, along = Along, err = HeadingErr;
      bool outside = Mathf.Abs(lat) > leg.turnaround || along < -leg.behind;
      if (outside && !turning){ turning = true; fogMsg = "The fog turns you back toward the light"; fogMsgUntil = Time.time + 4f; Debug.Log($"[AT] fog turnaround on lat={lat:F0} along={along:F0} err={err:F0}"); }
      // the fog steers for a point on the course line ahead (not the far goal), so it really brings you back in
      var back = leg.start + leg.Dir * Mathf.Clamp(along + 160f, 60f, leg.Length); var bd = back - boat.Pos;
      float backErr = Mathf.DeltaAngle(boat.heading, Mathf.Atan2(bd.x, bd.z) * Mathf.Rad2Deg);
      if (turning && Mathf.Abs(lat) < leg.fogStart - 20f && along > -leg.behind * 0.5f && Mathf.Abs(err) < 35f){ turning = false; Debug.Log($"[AT] fog turnaround off lat={lat:F0} err={err:F0}"); }
      float assist = 0f;
      if (turning){ assist = Mathf.Clamp(backErr / 25f, -1f, 1f); steer *= 0.15f; boat.sail = Mathf.Max(boat.sail, 0.35f); }
      else {
        float off = Mathf.Abs(err); float k = Mathf.Clamp01((off - 40f) / 50f) + Mathf.Clamp01((Mathf.Abs(lat) - 90f) / 80f) * (Mathf.Sign(lat) == -Mathf.Sign(err) ? 0f : 0.5f);
        k = Mathf.Clamp01(k); pullAmt = k * 0.45f; assist = Mathf.Sign(err) * pullAmt;
        bool on = pullAmt > 0.05f;
        if (on != pulling){ pulling = on; Debug.Log($"[AT] drift pull {(on ? "on" : "off")} err={err:F0} lat={lat:F0}"); }
      }
      boat.rudder = Mathf.Clamp(steer + assist, -1f, 1f);
      boat.Tick(dt);
      // the star compass: lined up within 5 degrees chimes once; it re-arms after you've been off by 12+ for half a second
      err = HeadingErr; bool nowLined = Mathf.Abs(err) < 5f && boat.speed > 0.5f;
      if (Mathf.Abs(err) > 12f){ offSince += dt; if (offSince > 0.5f) chimeArmed = true; } else offSince = 0f;
      if (nowLined && chimeArmed){ chimeArmed = false; chimes++; bool played = VoyageAudio.I != null && VoyageAudio.I.Play("chime"); Debug.Log($"[AT] lined up err={err:F1}: chime #{chimes}{(played ? "" : " (silent: audio locked)")}"); }
      lined = nowLined;
      if (GoalDist < leg.arriveDist){ state = State.Arrive; stateT = 0f; Arrive(); }
    }
    void Arrive(){
      Debug.Log($"[AT] arrived at the {leg.landmark} t={legTime:F1}s gusts={gustsCaught}/{leg.gusts.Length} chimes={chimes}");
      VoyageAudio.I?.Arrival(); flash = 1f; turning = pulling = false;
      for (int i = 0; i < 160; i++){ float a = i / 160f * Mathf.PI * 2f; var v = new Vector3(Mathf.Cos(a), 0.05f, Mathf.Sin(a));
        Emit(burstPS, leg.goal + Vector3.up * 1.5f + v * 6f, v * Random.Range(14f, 22f), Random.Range(2f, 4f), Random.Range(2.5f, 4f), leg.glow); }
      for (int i = 0; i < 80; i++) Emit(burstPS, leg.goal + Vector3.up * Random.Range(10f, 70f), new Vector3(Random.Range(-3f, 3f), Random.Range(4f, 12f), Random.Range(-3f, 3f)), Random.Range(1.5f, 3f), Random.Range(2f, 4f), Color.Lerp(leg.glow, Color.white, 0.4f));
    }
    void ArriveTick(float dt){
      // ease to a stop facing the spire
      float err = HeadingErr; boat.rudder = Mathf.Clamp(err / 30f, -1f, 1f); boat.sail = Mathf.MoveTowards(boat.sail, 0f, dt * 0.4f); boat.Tick(dt, 0f);
      if (stateT > 4f){ state = State.End; stateT = 0f; endShownAt = Time.time; Debug.Log($"[AT] end card: {leg.name} complete in {FormatTime(legTime)}"); }
    }
    void Restart(string src){
      boat.Reset(leg.start, leg.startHeading, 0f); camYaw = leg.startHeading; lookYaw = lookPitch = 0f;
      for (int i = 0; i < gustTaken.Length; i++) gustTaken[i] = false; gustsCaught = 0; chimes = 0; chimeArmed = true; turning = pulling = false;
      legTime = 0f; state = State.Sail; stateT = 0f; sailHintDone = steerHintDone = false; hintT = 0f; endShownAt = -1f;
      Debug.Log($"[AT] restart by {src}");
    }
    static string FormatTime(float s) => $"{(int)(s / 60f)}:{(int)(s % 60f):00}";

    void Gusts(float dt){
      for (int i = 0; i < gustT.Length; i++){
        var p = leg.gusts[i];
        if (!gustTaken[i]){
          gustT[i].localScale = Vector3.one * (8f + Mathf.Sin(Time.time * 2f + i) * 1.2f);
          if (Random.value < dt * 30f){ var o = Random.insideUnitSphere * 9f; o.y = Mathf.Abs(o.y) * 0.6f + 0.5f; Emit(gustPS[i], p + o, new Vector3(-o.z, 0.8f, o.x) * 0.25f, Random.Range(0.5f, 1.3f), Random.Range(1f, 2f), Color.Lerp(leg.glow, Color.white, Random.value * 0.6f)); }
          if (state == State.Sail && Vector2.Distance(new Vector2(boat.Pos.x, boat.Pos.z), new Vector2(p.x, p.z)) < 14f){
            gustTaken[i] = true; gustsCaught++; boat.boost = 7f; gustT[i].gameObject.SetActive(false);
            for (int k = 0; k < 40; k++) Emit(gustPS[i], boat.Pos + Vector3.up * 3f + Random.insideUnitSphere * 3f, boat.Fwd * boat.speed * 0.6f + Random.insideUnitSphere * 4f, Random.Range(0.6f, 1.4f), Random.Range(0.8f, 1.6f), Color.white);
            VoyageAudio.I?.Play("gust"); Debug.Log($"[AT] star gust {i + 1} caught ({gustsCaught}/{gustT.Length}) speed={boat.speed:F1}");
          }
        }
      }
      if (boat.boost > 0.5f && Random.value < dt * 40f) Emit(motesPS, boat.Pos + Vector3.up * Random.Range(1f, 6f) + Random.insideUnitSphere * 2f, -boat.Fwd * 2f, Random.Range(0.4f, 0.9f), 1.2f, Color.white);
    }
    void Wake(float dt){
      wakeT += dt; var stern = boat.Pos - boat.Fwd * 3.2f; float sp = Mathf.Clamp01(boat.speed / boat.maxSpeed);
      if (wakeT > 0.35f){ wakeT = 0f; for (int i = wake.Length - 1; i > 0; i--) wake[i] = wake[i - 1]; wake[0] = new Vector4(stern.x, 0, stern.z, sp); }
      for (int i = 0; i < wake.Length; i++){ var w = wake[i]; w.w *= Mathf.Lerp(1f, 0.78f, i / 7f); wake[i] = w; }
      var sw = new Vector4[8]; for (int i = 0; i < 8; i++){ sw[i] = wake[i]; sw[i].w = wake[i].w * (1f - i / 8f) * 0.8f; }
      sw[0] = new Vector4(stern.x, 0, stern.z, sp * 0.8f);
      Shader.SetGlobalVectorArray("_AT_Wake", sw);
      if (sp > 0.1f && Random.value < dt * 25f * sp) Emit(motesPS, stern + boat.transform.right * Random.Range(-1f, 1f) + Vector3.up * 0.3f, -boat.Fwd * 1f + Vector3.up * 0.3f, Random.Range(0.3f, 0.7f), 2.5f, Color.Lerp(leg.glow, Color.white, 0.5f));
    }

    void ApplySky(float p){
      float k = Mathf.SmoothStep(0f, 1f, p);
      Shader.SetGlobalColor("_AT_SkyTop", Color.Lerp(leg.top0, leg.top1, k)); Shader.SetGlobalColor("_AT_SkyMid", Color.Lerp(leg.mid0, leg.mid1, k));
      Shader.SetGlobalColor("_AT_SkyLow", Color.Lerp(leg.low0, leg.low1, k)); Shader.SetGlobalColor("_AT_Horizon", Color.Lerp(leg.hor0, leg.hor1, k));
      Shader.SetGlobalColor("_AT_GlowCol", leg.glow);
      float stars = state >= State.Arrive ? Mathf.Lerp(leg.stars1, 1.15f, Mathf.Clamp01(stateT / 3f)) : Mathf.Lerp(leg.stars0, leg.stars1, k);
      Shader.SetGlobalFloat("_AT_StarAmount", stars);
      if (skyboxMat){ float ex = Mathf.Lerp(1f, 0.74f, k) * (1f + flash * 0.25f); skyboxMat.SetFloat("_Exposure", ex); Shader.SetGlobalFloat("_AT_PanoGain", ex); }
      float arr = state >= State.Arrive ? Mathf.Clamp01((state == State.End ? 4f : stateT) / 1.5f) : 0f;
      if (spireLight) spireLight.intensity = 4f + arr * 10f + Mathf.Sin(Time.time * 1.4f) * 0.3f;
      if (spireMat && realModels) spireMat.SetColor("_EmissionColor", Color.white * (4f + arr * 4f + Mathf.Sin(Time.time * 1.4f) * 0.25f));
      var top = leg.goal + Vector3.up * lanternY; var gd = (top - (cam ? cam.transform.position : boat.Pos)).normalized;
      float strength = 0.6f + 0.6f * Mathf.Clamp01(1f - GoalDist / leg.Length) + (state >= State.Arrive ? 1f : 0f) + (lined ? 0.25f : 0f);
      Shader.SetGlobalVector("_AT_GoalDir", new Vector4(gd.x, gd.y, gd.z, strength));
      Shader.SetGlobalFloat("_AT_Flash", flash);
      if (spireHalo){ float hs = 34f * (1f + (state >= State.Arrive ? Mathf.Clamp01(stateT / 1.5f) * 1.6f : 0f)) * (1f + Mathf.Sin(Time.time * 1.4f) * 0.05f); spireHalo.localScale = Vector3.one * hs; }
      if (spireMat && !realModels) spireMat.SetFloat("_GlowBoost", 1.2f + (state >= State.Arrive ? 1.5f * Mathf.Clamp01(stateT / 1.5f) : 0f) + Mathf.Sin(Time.time * 1.4f) * 0.1f);
    }
    void Place(){   // sea and sky follow the camera so the horizon never ends
      var c = cam.transform.position;
      sea.position = new Vector3(Mathf.Round(c.x / 50f) * 50f, 0f, Mathf.Round(c.z / 50f) * 50f);
      sky.position = c;
    }
    void UpdateCamera(float dt){
      camYaw = Mathf.LerpAngle(camYaw, boat.heading, 1f - Mathf.Exp(-dt * 2.2f));
      float yaw = camYaw + lookYaw;
      var rot = Quaternion.Euler(-(realModels ? 8f : 7f) - lookPitch, yaw, 0f);
      var back = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward; var side = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
      var target = realModels ? boat.Pos - back * 9.5f + side * 2.2f + Vector3.up * 2.3f : boat.Pos - back * 14f + Vector3.up * 3.1f;
      if (state >= State.Arrive){   // pull back and look up at the spire as it lights
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((state == State.End ? 4f + stateT : stateT) / 4f));
        target += -back * 10f * k + Vector3.up * 3f * k;
        var look = Quaternion.LookRotation((leg.goal + Vector3.up * lanternY * 0.7f) - target);
        rot = Quaternion.Slerp(rot, look, k * 0.6f);
      }
      camPos = Vector3.Lerp(camPos == Vector3.zero ? target : camPos, target, 1f - Mathf.Exp(-dt * 6f));
      cam.transform.SetPositionAndRotation(camPos, rot);
    }

    // ---------- HUD (IMGUI, like Salt and Glory) ----------
    Texture2D white; GUIStyle txt;
    Rect R(float x, float y, float w, float h) => new Rect(x * S, y * S, w * S, h * S);
    void Box(Rect r, Color c){ var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }
    void Label(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter, FontStyle f = FontStyle.Normal){
      txt.fontSize = Mathf.RoundToInt(size * S); txt.alignment = a; txt.fontStyle = f;
      var o = GUI.color; GUI.color = new Color(0.05f, 0.03f, 0.12f, c.a * 0.55f); GUI.Label(new Rect(r.x + 1.5f * S, r.y + 1.5f * S, r.width, r.height), s, txt); GUI.color = c; GUI.Label(r, s, txt); GUI.color = o;
    }
    void OnGUI(){
      if (!white){ white = Texture2D.whiteTexture; }
      if (txt == null) txt = new GUIStyle(GUI.skin.label){ wordWrap = true, richText = false };
      float W = Screen.width / S;
      var ink = new Color(1f, 0.96f, 0.9f);
      if (state == State.Title){
        Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.08f, 0.05f, 0.18f, 0.25f));
        Label(R(0, 170, W, 90), "Astral Tide", 64, ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        Label(R(0, 258, W, 40), $"Leg 1 · {leg.name}", 26, new Color(1f, 0.86f, 0.62f));
        Label(R(0, 296, W, 34), leg.subtitle, 18, new Color(0.95f, 0.9f, 1f, 0.9f));
        float a = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 2.5f);
        Label(R(0, 520, W, 36), Application.isMobilePlatform ? "Tap to set out" : "Press Enter, Space, click or (A) to set out", 22, new Color(1f, 1f, 1f, a));
        return;
      }
      Compass(W, ink);
      if (state == State.Sail){
        // pause button (touch / mouse) and the sail gauge
        Box(RPause, new Color(0.1f, 0.07f, 0.22f, 0.45f)); Label(RPause, "II", 22, ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        var sg = R(W - 70, 470, 22, 190); Box(sg, new Color(0.1f, 0.07f, 0.22f, 0.45f));
        float f = boat.sail; Box(new Rect(sg.x + 3 * S, sg.yMax - 3 * S - (sg.height - 6 * S) * f, sg.width - 6 * S, (sg.height - 6 * S) * f), new Color(1f, 0.88f, 0.62f, 0.85f));
        Label(R(W - 110, 662, 100, 26), $"Sail {Mathf.RoundToInt(boat.sail * 100)}%", 15, ink);
        Label(R(W - 110, 440, 100, 26), $"{boat.speed * 1.94f:F0} kn", 15, ink);
        // first-time hints
        hintT += Time.deltaTime; if (!sailHintDone && boat.sail > 0.35f) sailHintDone = true; if (sailHintDone && !steerHintDone && hintT > 14f) steerHintDone = true;
        string hint = !sailHintDone ? "Raise the sail: W or Up · right trigger · drag up" :
          !steerHintDone ? "Steer toward the light: A / D or Left / Right · left stick · drag sideways.  Follow the star on the compass." : null;
        if (hint != null) Label(R(0, 640, W, 40), hint, 19, ink);
        if (turning || Time.time < fogMsgUntil){
          float fa = turning ? 1f : Mathf.Clamp01(fogMsgUntil - Time.time);
          Box(new Rect(0, 0, Screen.width, Screen.height), new Color(leg.fog.r, leg.fog.g, leg.fog.b, 0.28f * fa));
          Label(R(0, 560, W, 36), fogMsg, 21, new Color(1f, 1f, 1f, fa), TextAnchor.MiddleCenter, FontStyle.Italic);
        }
      }
      if (flash > 0.01f) Box(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 0.92f, 0.75f, flash * 0.3f));
      if (state == State.End){
        float a = Mathf.Clamp01(stateT / 1.2f);
        var card = R(W / 2 - 300, 200, 600, 300); Box(card, new Color(0.09f, 0.06f, 0.2f, 0.72f * a));
        Box(new Rect(card.x, card.y, card.width, 3 * S), new Color(1f, 0.85f, 0.55f, a));
        Label(R(W / 2 - 300, 222, 600, 40), $"Leg 1 · {leg.name}", 22, new Color(1f, 0.86f, 0.62f, a));
        Label(R(W / 2 - 300, 262, 600, 60), $"The {leg.landmark} is lit", 40, new Color(1f, 0.97f, 0.92f, a), TextAnchor.MiddleCenter, FontStyle.Bold);
        Label(R(W / 2 - 300, 330, 600, 30), $"Voyage {FormatTime(legTime)}   ·   Star gusts {gustsCaught} / {leg.gusts.Length}", 20, new Color(1f, 1f, 1f, a));
        Label(R(W / 2 - 300, 370, 600, 30), "Next: Aurora Reach (coming in M2)", 17, new Color(0.85f, 0.85f, 1f, 0.85f * a), TextAnchor.MiddleCenter, FontStyle.Italic);
        if (stateT > 1.2f) Label(R(W / 2 - 300, 430, 600, 34), Application.isMobilePlatform ? "Tap to sail again" : "Enter · click · (A) to sail again", 19, new Color(1f, 1f, 1f, a * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 2.5f))));
      }
      if (paused){
        Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.06f, 0.04f, 0.14f, 0.55f));
        Label(R(0, 280, W, 60), "Paused", 44, ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        Label(R(0, 350, W, 30), "Esc · Start · tap to resume    ·    R to restart the leg", 19, ink);
      }
    }
    /// <summary>The thin star compass: +-75 degrees around the bow, a tick every 15, the goal marked by a four-point star that
    /// brightens (and chimes) when you're lined up; off the strip it waits at the edge as an arrow.</summary>
    void Compass(float W, Color ink){
      float cx = W / 2f, half = 300f, y = 40f, span = 75f;
      if (compassStrip){   // Art Dev's 360-degree strip (u = 0 is north = +Z), scrolled so the bow is in the middle (RawImage-style uvRect)
        float w = span * 2f / 360f; GUI.DrawTextureWithTexCoords(R(cx - half, y - 12, half * 2, 26), compassStrip, new Rect(boat.heading / 360f - w / 2f, 0f, w, 1f), true);
      } else Box(R(cx - half, y, half * 2f, 2), new Color(1f, 1f, 1f, 0.55f));
      for (int a = -180; a < 180 && !compassStrip; a += 15){
        float d = Mathf.DeltaAngle(boat.heading, a); if (Mathf.Abs(d) > span) continue;
        float x = cx + d / span * half; bool major = a % 45 == 0;
        Box(R(x - 0.75f, y - (major ? 7 : 4), 1.5f, major ? 16 : 10), new Color(1f, 1f, 1f, major ? 0.7f : 0.4f));
      }
      Box(R(cx - 1, y + 6, 2, 10), new Color(1f, 1f, 1f, 0.9f));   // the bow mark
      float err = HeadingErr, gx = cx + Mathf.Clamp(err, -span, span) / span * half;
      float glow = lined ? 1f : Mathf.Clamp01(1f - (Mathf.Abs(err) - 5f) / 25f) * 0.5f;
      var gold = new Color(1f, 0.85f, 0.55f);
      if (Mathf.Abs(err) <= span){
        float s = 15f + glow * 6f;
        if (compassMarker){   // the gold pointer above the strip, pulsing brighter when lined up
          float ms = 13f + glow * 5f + (lined ? Mathf.Sin(Time.unscaledTime * 6f) * 1.5f : 0f); var o = GUI.color; GUI.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * glow);
          GUI.DrawTexture(R(gx - ms, y - 12 - ms * 1.6f, ms * 2, ms * 2), compassMarker, ScaleMode.ScaleToFit, true); GUI.color = o;
        }
        else {
          Box(R(gx - s * 0.12f, y - s + 1, s * 0.24f, s * 2), new Color(gold.r, gold.g, gold.b, 0.95f));
          Box(R(gx - s * 0.6f, y - s * 0.12f + 1, s * 1.2f, s * 0.24f), new Color(gold.r, gold.g, gold.b, 0.95f));
          if (glow > 0.05f) Box(R(gx - s * 0.5f, y - s * 0.5f + 1, s, s), new Color(1f, 0.95f, 0.8f, 0.25f * glow));
        }
      } else Label(R(err < 0 ? cx - half - 40 : cx + half + 4, y - 14, 36, 30), err < 0 ? "<<" : ">>", 20, gold, TextAnchor.MiddleCenter, FontStyle.Bold);
      Label(R(cx - 200, y + 16, 400, 24), $"{leg.landmark}  ·  {(GoalDist >= 1000f ? (GoalDist / 1000f).ToString("F1") + " km" : Mathf.RoundToInt(GoalDist / 10f) * 10 + " m")}", 15, new Color(ink.r, ink.g, ink.b, 0.85f));
      if (lined && state == State.Sail) Label(R(cx - 200, y + 36, 400, 22), "on course", 14, new Color(gold.r, gold.g, gold.b, 0.9f), TextAnchor.MiddleCenter, FontStyle.Italic);
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    void DevKeys(Keyboard kb){
      if (kb == null) return;
      if (kb.f2Key.wasPressedThisFrame){ var p = leg.goal - leg.Dir * 260f; boat.Reset(p, GoalBearing, 1f); camYaw = boat.heading; if (state == State.Title) state = State.Sail; Debug.Log("[AT] dev: near the spire"); }
      if (kb.f3Key.wasPressedThisFrame){ for (int i = 0; i < gustTaken.Length; i++) if (!gustTaken[i]){ var p = leg.gusts[i] - leg.Dir * 80f; boat.Reset(p, Mathf.Atan2(leg.Dir.x, leg.Dir.z) * Mathf.Rad2Deg, 1f); camYaw = boat.heading; break; } Debug.Log("[AT] dev: next gust"); }
      if (kb.f4Key.wasPressedThisFrame){ var r = new Vector3(leg.Dir.z, 0, -leg.Dir.x); boat.Reset(boat.Pos + r * 170f, boat.heading + 80f, 0.8f); camYaw = boat.heading; Debug.Log("[AT] dev: drifted to the edge"); }
    }
#endif
#if DEVELOPMENT_BUILD || UNITY_EDITOR || AT_QA
    // QA hook for the self-check (window.atUnity.SendMessage('Voyage', ...)); compiled out of the release build
    public void QAStatus(){
      Debug.Log($"[AT] status state={state} paused={(paused ? 1 : 0)} x={boat.Pos.x:F1} z={boat.Pos.z:F1} heading={boat.heading:F1} speed={boat.speed:F2} sail={boat.sail:F2} rudder={boat.rudder:F2} " +
        $"err={HeadingErr:F1} lat={Lateral:F1} along={Along:F1} dist={GoalDist:F0} lined={(lined ? 1 : 0)} chimes={chimes} pull={pullAmt:F2} pulling={(pulling ? 1 : 0)} turning={(turning ? 1 : 0)} " +
        $"gusts={gustsCaught} boost={boat.boost:F1} look={lookYaw:F1} camYaw={camYaw:F1} t={legTime:F1} {(VoyageAudio.I ? VoyageAudio.I.Status() : "")}");
    }
    /// <summary>"x,z,heading,sail": put the ship somewhere (keeps the state; from the title it starts the voyage).</summary>
    public void QAPlace(string a){
      var v = a.Split(','); var ci = System.Globalization.CultureInfo.InvariantCulture;
      float x = float.Parse(v[0], ci), z = float.Parse(v[1], ci), h = float.Parse(v[2], ci), s = v.Length > 3 ? float.Parse(v[3], ci) : boat.sail;
      boat.Reset(new Vector3(x, 0, z), h, s); boat.speed = s * boat.maxSpeed * 0.8f; camYaw = h; camPos = Vector3.zero; turning = pulling = false;
      if (state == State.Title){ state = State.Sail; stateT = 0f; VoyageAudio.I?.SetVoyage(true); }
      Debug.Log($"[AT] qa: placed at {x:F0},{z:F0} heading {h:F0} sail {s:F2}");
    }
    public void QAGoalBearing(){ Debug.Log($"[AT] qa: goal bearing {GoalBearing:F1}"); }
#endif
  }
}
