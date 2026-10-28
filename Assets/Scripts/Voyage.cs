using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace AstralTide {
  /// <summary>Astral Tide M2: five calm legs you can't fail, all on one sea and sky shader recoloured per leg. Title with Continue
  /// and a leg select; sail toward each leg's landmark with the star compass, the drift pull and fog keeping you on course; the
  /// swell rocks the ship (same wave function on the CPU and in the sea shader). Each leg has its own mechanic (Aurora Reach:
  /// currents, Ember Drift: drifting meteors, Frost Mirror: ice floes and gust chains, The Cosmic Sea: star buoys and a boost run)
  /// and five star fragments that light its constellation. Arriving opens the next leg (saved in PlayerPrefs); after the fifth the
  /// end card. Keyboard, gamepad and touch (Salt and Glory's input code). This file: the loop, input and camera; VoyageLegs.cs:
  /// the legs and their mechanics; VoyageHud.cs: the HUD.</summary>
  public partial class Voyage : MonoBehaviour {
    // ---- scene refs (set by BuildWeb.SetupScene) ----
    public Camera cam; public Transform sea, sky, starVeil; public Boat boat;
    public Transform[] landmarks = new Transform[5], landmarkMirrors = new Transform[5], halos = new Transform[5], landmarkAnim = new Transform[5];
    public Light[] landmarkLights = new Light[5]; public float[] landmarkTop = new float[5], landmarkEm = new float[5]; public Material[] landmarkMats = new Material[5];
    public Mesh quadMesh; public Material glowMat, gustMat, haloMat, fogMat, seaMat, fragMat, constMat, lineMat, rockMat, rockMirMat, iceMat, iceMirMat, buoyMat;
    public Texture2D skyPano, starfield, compassStrip, compassMarker;   // Art Dev (optional; null = procedural)
    public Material skyboxMat; public Light boatLight; public bool realModels;
    // M2 art (Art Dev, loaded by name in BuildWeb; null = procedural fallback)
    public Texture2D[] panos = new Texture2D[5]; public Texture2D currentTex, fragIcon;
    public GameObject meteorArt, floeArt, fragArt, buoyArt; public Material propMat, propMirMat;
    public Transform[] landmarkAnimMir = new Transform[5]; public Transform clapper, clapperMir;

    enum State { Title, Sail, Arrive, LegDone, End }
    State state = State.Title; float stateT; bool paused;
    Leg leg = Leg.All[0]; int legIx;
    float legTime; int gustsCaught;
    float camYaw, lookYaw, lookPitch, lastLook = -10f; Vector3 camPos;
    // guidance
    bool lined, chimeArmed = true; float offSince; int chimes; bool pulling, turning; float pullAmt; string fogMsg; float fogMsgUntil;
    float hintT; bool sailHintDone, steerHintDone, sailPending; float sailStill = 99f; float lastSail;
    float flash;
    int menuSel; float stickLatch;

    float S => Screen.height / 720f;
    float Along => Vector3.Dot(boat.Pos - leg.start, leg.Dir);
    float Lateral { get { var d = leg.Dir; var r = boat.Pos - leg.start; return r.x * d.z - r.z * d.x; } }   // + = right of the line
    float GoalBearing { get { var d = leg.goal - boat.Pos; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; } }
    float HeadingErr => Mathf.DeltaAngle(boat.heading, GoalBearing);   // + = goal is to starboard
    float GoalDist => Vector2.Distance(new Vector2(boat.Pos.x, boat.Pos.z), new Vector2(leg.goal.x, leg.goal.z));
    float LegProgress => Mathf.Clamp01(Along / leg.Length);
    float CourseYaw => Mathf.Atan2(leg.Dir.x, leg.Dir.z) * Mathf.Rad2Deg;

    void Start(){
      Application.targetFrameRate = 60;
      if (!panos[0]) panos[0] = skyPano;
      if (starfield){ Shader.SetGlobalTexture("_AT_Starfield", starfield); Shader.SetGlobalFloat("_AT_StarfieldMix", 1f); } else Shader.SetGlobalFloat("_AT_StarfieldMix", 0f);
      Debug.Log($"[AT] art: pano={(skyPano ? 1 : 0)} starfield={(starfield ? 1 : 0)} compass={(compassStrip ? 1 : 0)} marker={(compassMarker ? 1 : 0)} models={(realModels ? 1 : 0)} wake={(boat.wakeTrail ? 1 : 0)}");
      motesPS = MakePS("WakeMotes", 400); burstPS = MakePS("ArrivalBurst", 600); sparkPS = MakePS("Sparks", 400);
      menuSel = ContinueLeg();
      LoadLeg(menuSel);
      Debug.Log($"[AT] ready unlocked={Progress.Unlocked} continue={menuSel + 1}");
    }
    static int ContinueLeg(){
      for (int i = 0; i < Progress.Unlocked; i++) if (Progress.Best(i) <= 0f) return i;
      return Progress.Unlocked - 1;
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
    bool ConfirmPress(out string src){
      var kb = Keyboard.current; src = "keyboard";
      if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) return true;
      var gp = Gamepad.current; src = "gamepad"; if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame)) return true;
      return false;
    }
    bool PointerBegan(out Vector2 at, out string src){
      foreach (var p in Pointers()) if (p.began){ at = p.p; src = p.id < 0 ? "mouse" : "touch"; return true; }
      at = Vector2.zero; src = null; return false;
    }
    bool AnyPress(out string src){
      if (ConfirmPress(out src)) return true;
      return PointerBegan(out _, out src);
    }
    /// <summary>Menu up / down from arrows, W/S, the d-pad or the left stick (with a latch so a held stick steps once).</summary>
    int MenuNav(){
      var kb = Keyboard.current; var gp = Gamepad.current; int d = 0;
      if (kb != null){ if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) d = -1; if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) d = 1; }
      if (gp != null){
        if (gp.dpad.up.wasPressedThisFrame) d = -1; if (gp.dpad.down.wasPressedThisFrame) d = 1;
        float y = gp.leftStick.y.ReadValue();
        if (Mathf.Abs(y) > 0.6f){ if (stickLatch == 0f){ d = y > 0 ? -1 : 1; stickLatch = Mathf.Sign(y); } } else if (Mathf.Abs(y) < 0.3f) stickLatch = 0f;
      }
      return d;
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
    // pause menu buttons (touch / mouse), in design units
    Rect RResume => R(Screen.width / S / 2 - 150, 360, 300, 46);
    Rect RRestart => R(Screen.width / S / 2 - 150, 414, 300, 46);
    Rect RLegs => R(Screen.width / S / 2 - 150, 468, 300, 46);
    void PausedInput(bool pauseKey){
      var kb = Keyboard.current; var gp = Gamepad.current;
      bool restart = (kb != null && kb.rKey.wasPressedThisFrame) || (gp != null && gp.buttonNorth.wasPressedThisFrame);
      bool legs = (kb != null && (kb.lKey.wasPressedThisFrame || kb.tKey.wasPressedThisFrame)) || (gp != null && (gp.buttonEast.wasPressedThisFrame || gp.selectButton.wasPressedThisFrame));
      bool resume = pauseKey || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (gp != null && gp.buttonSouth.wasPressedThisFrame);
      if (PointerBegan(out var at, out _)){
        if (RRestart.Contains(at)) restart = true; else if (RLegs.Contains(at)) legs = true; else resume = true;
      }
      if (restart){ SetPaused(false, "restart"); Restart("pause menu"); }
      else if (legs){ SetPaused(false, "leg select"); ToTitle("pause menu"); }
      else if (resume) SetPaused(false, "input");
    }

    // ---------- loop ----------
    void Update(){
      var kb = Keyboard.current; var gp = Gamepad.current;
      float dt = Time.deltaTime; stateT += dt;
      Waves.time = Time.time; Shader.SetGlobalFloat("_AT_WaveTime", Waves.time);
      bool pauseKey = (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.startButton.wasPressedThisFrame);
      if (paused){ PausedInput(pauseKey); return; }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
      DevKeys(kb);
#endif
      switch (state){
        case State.Title: TitleTick(); boat.rudder = 0f; boat.Tick(dt); break;
        case State.Sail:
          if (pauseKey){ SetPaused(true, "key"); return; }
          legTime += dt; Sail(dt); break;
        case State.Arrive: ArriveTick(dt); break;
        case State.LegDone:
        case State.End:
          boat.rudder = 0f; boat.sail = Mathf.MoveTowards(boat.sail, 0f, dt * 0.3f); boat.Tick(dt, 0f);
          if (stateT > 1.2f && AnyPress(out var s2)){
            if (state == State.LegDone){ LoadLeg(legIx + 1); BeginSail(s2); }
            else ToTitle(s2);
          }
          break;
      }
      LegTick(dt); Wake(dt);
      VoyageAudio.I?.SetWake(boat.speed / boat.maxSpeed);
      flash = Mathf.MoveTowards(flash, 0f, dt * 0.35f);
      ApplySky(state == State.Title ? 0f : LegProgress);
      Place();
    }
    void LateUpdate(){ UpdateCamera(Time.deltaTime); }

    void TitleTick(){
      int n = Progress.Unlocked, d = MenuNav();
      if (d != 0){ menuSel = Mathf.Clamp(menuSel + d, 0, n - 1); Debug.Log($"[AT] title select leg {menuSel + 1}"); }
      if (ConfirmPress(out var src)){ StartLeg(menuSel, src); return; }
      if (PointerBegan(out var at, out var psrc)){
        for (int i = 0; i < Leg.All.Length; i++) if (RowRect(i).Contains(at)){ if (i < n) StartLeg(i, psrc); return; }
        StartLeg(menuSel, psrc);
      }
    }
    Rect RowRect(int i) => R(Screen.width / S / 2 - 230, 336 + i * 42, 460, 38);
    void StartLeg(int i, string src){ if (i != legIx || legDirty) LoadLeg(i); BeginSail(src); }
    void BeginSail(string src){
      state = State.Sail; stateT = 0f; legTime = 0f; VoyageAudio.I?.SetLeg(legIx); VoyageAudio.I?.SetVoyage(true);
      Debug.Log($"[AT] voyage start by {src}: leg {legIx + 1} {leg.name}");
    }
    void ToTitle(string src){
      state = State.Title; stateT = 0f; menuSel = ContinueLeg(); LoadLeg(menuSel);
      Debug.Log($"[AT] title (leg select) by {src}: unlocked={Progress.Unlocked} continue={menuSel + 1}");
    }

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
    void ArriveTick(float dt){
      // ease to a stop facing the landmark
      float err = HeadingErr; boat.rudder = Mathf.Clamp(err / 30f, -1f, 1f); boat.sail = Mathf.MoveTowards(boat.sail, 0f, dt * 0.4f); boat.Tick(dt, 0f);
      ArrivalAnim(stateT);
      if (stateT > 4f){
        bool last = legIx == Leg.All.Length - 1;
        state = last ? State.End : State.LegDone; stateT = 0f;
        Debug.Log(last ? $"[AT] end card: the voyage is complete ({leg.name} in {FormatTime(legTime)})" : $"[AT] leg complete card: {leg.name} in {FormatTime(legTime)}; next {Leg.All[legIx + 1].name} unlocked={Progress.Unlocked}");
      }
    }
    void Restart(string src){
      LoadLeg(legIx); BeginSail("restart");
      Debug.Log($"[AT] restart by {src}");
    }
    static string FormatTime(float s) => $"{(int)(s / 60f)}:{(int)(s % 60f):00}";

    void Place(){   // sea and sky follow the camera so the horizon never ends
      var c = cam.transform.position;
      sea.position = new Vector3(Mathf.Round(c.x / 10f) * 10f, 0f, Mathf.Round(c.z / 10f) * 10f);
      if (sky) sky.position = c; if (starVeil) starVeil.position = c;
      if (constRoot) constRoot.position = c;
    }
    // chase camera (M2: pulled out ~10%), riding a little of the swell
    public const float CamBack = 10.45f, CamSide = 2.42f, CamUp = 2.53f;
    void UpdateCamera(float dt){
      camYaw = Mathf.LerpAngle(camYaw, boat.heading, 1f - Mathf.Exp(-dt * 2.2f));
      float yaw = camYaw + lookYaw;
      var rot = Quaternion.Euler(-(realModels ? 8f : 7f) - lookPitch, yaw, 0f);
      var back = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward; var side = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
      var target = realModels ? boat.Pos - back * CamBack + side * CamSide + Vector3.up * CamUp : boat.Pos - back * 15.4f + Vector3.up * 3.4f;
      target.y += boat.WaveH * 0.6f;
      if (state >= State.Arrive){   // pull back and look up at the landmark as it lights
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((state != State.Arrive ? 4f + stateT : stateT) / 4f));
        target += -back * 11f * k + Vector3.up * 3.3f * k;
        var look = Quaternion.LookRotation((leg.goal + Vector3.up * landmarkTop[legIx] * 0.7f) - target);
        rot = Quaternion.Slerp(rot, look, k * 0.6f);
      }
      camPos = Vector3.Lerp(camPos == Vector3.zero ? target : camPos, target, 1f - Mathf.Exp(-dt * 6f));
      cam.transform.SetPositionAndRotation(camPos, rot);
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    void DevKeys(Keyboard kb){
      if (kb == null) return;
      if (kb.f2Key.wasPressedThisFrame){ var p = leg.goal - leg.Dir * 260f; boat.Reset(p, GoalBearing, 1f); camYaw = boat.heading; if (state == State.Title) BeginSail("dev"); Debug.Log("[AT] dev: near the landmark"); }
      if (kb.f3Key.wasPressedThisFrame){ for (int i = 0; i < fragT.Length; i++) if (fragT[i] && fragT[i].gameObject.activeSelf){ var p = leg.fragments[i] - leg.Dir * 60f; boat.Reset(p, CourseYaw, 1f); camYaw = boat.heading; break; } Debug.Log("[AT] dev: next fragment"); }
      if (kb.f4Key.wasPressedThisFrame){ var r = new Vector3(leg.Dir.z, 0, -leg.Dir.x); boat.Reset(boat.Pos + r * 170f, boat.heading + 80f, 0.8f); camYaw = boat.heading; Debug.Log("[AT] dev: drifted to the edge"); }
      if (kb.f5Key.wasPressedThisFrame){ int n = (legIx + 1) % Leg.All.Length; LoadLeg(n); BeginSail("dev"); }
    }
#endif
#if DEVELOPMENT_BUILD || UNITY_EDITOR || AT_QA
    // QA hook for the self-check (window.atUnity.SendMessage('Voyage', ...)); compiled out of the release build
    public void QAStatus(){
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      Debug.Log($"[AT] status state={state} paused={(paused ? 1 : 0)} leg={legIx + 1} x={boat.Pos.x:F1} z={boat.Pos.z:F1} heading={boat.heading:F1} speed={boat.speed:F2} sail={boat.sail:F2} rudder={boat.rudder:F2} " +
        $"err={HeadingErr:F1} lat={Lateral:F1} along={Along:F1} dist={GoalDist:F0} lined={(lined ? 1 : 0)} chimes={chimes} pull={pullAmt:F2} pulling={(pulling ? 1 : 0)} turning={(turning ? 1 : 0)} " +
        $"gusts={gustsCaught} chain={chainMax} boost={boat.boost:F1} look={lookYaw:F1} camYaw={camYaw:F1} t={legTime:F1} " +
        $"waveH={boat.WaveH.ToString("F3", ci)} pitch={boat.Pitch.ToString("F2", ci)} roll={boat.Roll.ToString("F2", ci)} rock={boat.Rock.ToString("F2", ci)} modelY={(boat.model ? boat.model.position.y : 0f).ToString("F3", ci)} " +
        $"waveAmp={Waves.amp:F2} current={(inCurrent ? 1 : 0)} rides={currentRides} drift={boat.drift.magnitude:F2} bumps={bumps} buoys={buoysTaken} streaks={(streaking ? 1 : 0)} " +
        $"frags={FragCount} fragMask={Progress.Frags(legIx)} lit={LitStars} const={(constDone ? 1 : 0)} unlocked={Progress.Unlocked} complete={(Progress.Complete ? 1 : 0)} menu={menuSel + 1} " +
        $"camDist={Vector3.Distance(new Vector3(cam.transform.position.x, 0, cam.transform.position.z), new Vector3(boat.Pos.x, 0, boat.Pos.z)):F2} sky={(sky && sky.gameObject.activeSelf ? "dome" : "pano")} cosmicWake={CosmicWakeOn} {(VoyageAudio.I ? VoyageAudio.I.Status() : "")}");
    }
    /// <summary>"x,z,heading,sail": put the ship somewhere (keeps the state; from the title it starts the voyage).</summary>
    public void QAPlace(string a){
      var v = a.Split(','); var ci = System.Globalization.CultureInfo.InvariantCulture;
      float x = float.Parse(v[0], ci), z = float.Parse(v[1], ci), h = float.Parse(v[2], ci), s = v.Length > 3 ? float.Parse(v[3], ci) : boat.sail;
      boat.Reset(new Vector3(x, 0, z), h, s); boat.speed = s * boat.maxSpeed * 0.8f; camYaw = h; camPos = Vector3.zero; turning = pulling = false;
      if (state == State.Title) BeginSail("qa");
      Debug.Log($"[AT] qa: placed at {x:F0},{z:F0} heading {h:F0} sail {s:F2}");
    }
    public void QAGoalBearing(){ Debug.Log($"[AT] qa: goal bearing {GoalBearing:F1}"); }
    /// <summary>Jump to leg n (1-based) and start sailing it (QA only: ignores the unlock).</summary>
    public void QALeg(string n){ int i = Mathf.Clamp(int.Parse(n) - 1, 0, Leg.All.Length - 1); if (paused) SetPaused(false, "qa"); LoadLeg(i); BeginSail("qa"); }
    public void QAClearSave(){ Progress.Clear(); Debug.Log("[AT] qa: save cleared"); }
    public void QATitle(){ if (paused) SetPaused(false, "qa"); ToTitle("qa"); }
    /// <summary>Put the ship 30 m before something, heading for it at full sail: "frag i", "obstacle i", "current i", "buoy i", "gust i", "goal".</summary>
    public void QAApproach(string what){
      var w = what.Split(' '); int i = w.Length > 1 ? int.Parse(w[1]) : 0; Vector3 p = leg.goal, dir = leg.Dir; float back = 30f;
      switch (w[0]){
        case "frag": p = leg.fragments[i]; break;
        case "obstacle": p = obsPos[i]; back = obsR[i] + 22f; dir = (p - (p - leg.Dir * 40f)).normalized; break;
        case "current": p = leg.currents[i * 2]; dir = (leg.currents[i * 2 + 1] - p).normalized; back = -8f; break;
        case "buoy": p = leg.buoys[i]; back = 40f; break;
        case "gust": p = leg.gusts[i]; break;
        case "goal": back = leg.arriveDist + 30f; dir = leg.Dir; break;
      }
      var start = p - dir * back; float h = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
      QAPlace($"{start.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{start.z.ToString(System.Globalization.CultureInfo.InvariantCulture)},{h.ToString(System.Globalization.CultureInfo.InvariantCulture)},1");
      Debug.Log($"[AT] qa: approach {what}");
    }
#endif
  }
}
