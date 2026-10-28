using System.Collections.Generic;
using UnityEngine;
namespace AstralTide {
  /// <summary>The legs: loading one (landmark, sky, swell, sea colour, course, fog, gusts, fragments, constellation, currents,
  /// obstacles, buoys) and running its mechanics each frame. Everything per leg lives under one root that is rebuilt on a load.</summary>
  public partial class Voyage {
    Transform legRoot, constRoot; bool legDirty;
    ParticleSystem motesPS, burstPS, sparkPS, warpPS;
    // gusts
    bool[] gustTaken = new bool[0]; Transform[] gustT = new Transform[0]; float lastGustAt = -99f; int chain, chainMax;
    // fragments and the constellation
    Transform[] fragT = new Transform[0]; Transform[] starT = new Transform[0]; LineRenderer[] edges = new LineRenderer[0]; Material constDim; bool constDone; float constAt = -99f;
    int FragCount => Progress.Count(Progress.Frags(legIx));
    bool[] starLit = new bool[0];
    int LitStars { get { int n = 0; foreach (var b in starLit) if (b) n++; return n; } }
    // currents
    bool inCurrent; int currentRides; readonly Vector4[] curArr = new Vector4[12];
    // obstacles (meteors / ice floes)
    Transform[] obsT = new Transform[0], obsMir = new Transform[0]; Vector3[] obsPos = new Vector3[0], obsBase = new Vector3[0]; float[] obsR = new float[0], obsPh = new float[0], obsCool = new float[0];
    int bumps; float creakCool; bool nearIceLogged;
    // star buoys and the boost run
    Transform[] buoyT = new Transform[0]; bool[] buoyTaken = new bool[0]; int buoysTaken, runChain; float lastBuoyAt = -99f; bool streaking;
    string CosmicWakeOn => boat.cosmicWake != null && boat.cosmicWake.Length > 0 && boat.cosmicWake[0] && boat.cosmicWake[0].gameObject.activeInHierarchy ? "1" : "0";
    float[] haloBase;

    // ---------- helpers ----------
    ParticleSystem MakePS(string n, int max, bool stretch = false){
      var go = new GameObject(n); var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var m = ps.main; m.loop = false; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = max; m.gravityModifier = 0f;
      var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
      var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
      g.SetKeys(new[]{ new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[]{ new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
      col.color = g;
      var pr = go.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = glowMat;
      if (stretch){ pr.renderMode = ParticleSystemRenderMode.Stretch; pr.velocityScale = 0.06f; pr.lengthScale = 1f; }
      ps.Play(); return ps;
    }
    static void Emit(ParticleSystem ps, Vector3 p, Vector3 v, float size, float life, Color c) =>
      ps.Emit(new ParticleSystem.EmitParams{ position = p, velocity = v, startSize = size, startLifetime = life, startColor = c, applyShapeToPosition = false }, 1);
    GameObject Billboard(string n, Vector3 p, float size, Material m, Transform parent = null){
      var go = new GameObject(n); go.transform.SetParent(parent, false); go.transform.position = p; go.transform.localScale = Vector3.one * size;
      go.AddComponent<MeshFilter>().sharedMesh = quadMesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m;
      r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; return go;
    }
    GameObject MeshObj(string n, Mesh mesh, Material m, Transform parent){
      var go = new GameObject(n); go.transform.SetParent(parent, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
      var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; return go;
    }
 /// <summary>Instantiate one of Art Dev's prop models (FBX, no materials imported) with our material on every renderer.</summary>
    Transform Prop(GameObject art, Material mat, Transform parent){
      var go = Instantiate(art, parent, false);
      foreach (var r in go.GetComponentsInChildren<Renderer>()){ var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = mat; r.sharedMaterials = ms; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
      return go.transform;
    }
    Vector3 Right => new Vector3(leg.Dir.z, 0, -leg.Dir.x);

    // ---------- loading a leg ----------
    void LoadLeg(int i){
      legIx = Mathf.Clamp(i, 0, Leg.All.Length - 1); leg = Leg.All[legIx]; legDirty = false;
      if (haloBase == null){ haloBase = new float[halos.Length]; for (int k = 0; k < halos.Length; k++) haloBase[k] = halos[k] ? halos[k].localScale.x : 30f; }
      if (legRoot) Destroy(legRoot.gameObject);
      legRoot = new GameObject("Leg" + (legIx + 1)).transform;
      boat.Reset(leg.start, leg.startHeading, 0f); camYaw = leg.startHeading; lookYaw = lookPitch = 0f; camPos = Vector3.zero;
      gustsCaught = 0; chimes = 0; chimeArmed = true; turning = pulling = false; pullAmt = 0f; legTime = 0f; flash = 0f; lined = false;
      sailHintDone = steerHintDone = false; hintT = 0f; chain = chainMax = 0; lastGustAt = -99f; bumps = 0; currentRides = 0; inCurrent = false;
      buoysTaken = runChain = 0; lastBuoyAt = -99f; streaking = false; creakCool = 0f; nearIceLogged = false;
      // the landmark: only this leg's is shown
      for (int k = 0; k < landmarks.Length; k++){
        bool on = k == legIx;
        var rot = k == 0 ? Quaternion.identity : Quaternion.LookRotation(leg.Dir);   // the new landmarks face the approach
        if (landmarks[k]){ landmarks[k].gameObject.SetActive(on); landmarks[k].SetPositionAndRotation(leg.goal, rot); }
        if (landmarkMirrors[k]){ landmarkMirrors[k].gameObject.SetActive(on); landmarkMirrors[k].SetPositionAndRotation(leg.goal, rot); }
      }
      // sky: Art Dev's panorama for the leg as the skybox, with the star veil (and live shooting stars) over it; the sea reflects
      // the same panorama. Fallback: the procedural dome recoloured per leg (with aurora / nebula drawn in the shader).
      var panoTex = panos[legIx]; bool pano = skyboxMat && panoTex;
      if (pano){ skyboxMat.SetTexture("_MainTex", panoTex); Shader.SetGlobalTexture("_AT_Pano", panoTex); }
      cam.clearFlags = pano ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor; cam.backgroundColor = leg.mid0;
      RenderSettings.skybox = pano ? skyboxMat : null;
      if (sky) sky.gameObject.SetActive(!pano); if (starVeil) starVeil.gameObject.SetActive(pano);
      Shader.SetGlobalFloat("_AT_PanoMix", pano ? 1f : 0f);
      Shader.SetGlobalFloat("_AT_Aurora", pano ? 0f : leg.aurora); Shader.SetGlobalFloat("_AT_Shoot", leg.shoot); Shader.SetGlobalFloat("_AT_Nebula", pano ? 0f : leg.nebula);
      Shader.SetGlobalColor("_AT_AuroraCol", leg.auroraA); Shader.SetGlobalColor("_AT_AuroraCol2", leg.auroraB);
      Shader.SetGlobalColor("_AT_NebulaCol", leg.nebA); Shader.SetGlobalColor("_AT_NebulaCol2", leg.nebB);
      // the swell and the sea
      Waves.amp = leg.waveAmp; Waves.len = leg.waveLen; Waves.speed = leg.waveSpeed; Waves.dir = (CourseYaw + leg.waveDir) * Mathf.Deg2Rad;
      Shader.SetGlobalVector("_AT_Wave", new Vector4(Waves.amp, Waves.len, Waves.speed, Waves.dir));
      if (seaMat){ seaMat.SetColor("_Tint", leg.water); seaMat.SetFloat("_Ripple", leg.ripple); }
      Shader.SetGlobalColor("_AT_SeaTint", leg.seaTint); Shader.SetGlobalFloat("_AT_Gloss", leg.gloss);
      var dir = leg.Dir; Shader.SetGlobalVector("_AT_Course", new Vector4(leg.start.x, leg.start.z, dir.x, dir.z));
      Shader.SetGlobalVector("_AT_FogBand", new Vector4(leg.fogStart, leg.fogFull, leg.behind, 0));
      Shader.SetGlobalColor("_AT_FogCol", leg.fog);
      if (fogMat) fogMat.SetColor("_Color", new Color(leg.fog.r, leg.fog.g, leg.fog.b, 0.07f));
      if (haloMat) haloMat.SetColor("_Color", new Color(leg.glow.r, leg.glow.g, leg.glow.b, 0.7f));
      // currents (shader array, always 12 long)
      int nc = Mathf.Min(leg.currents.Length / 2, 12);
      for (int k = 0; k < 12; k++) curArr[k] = k < nc ? new Vector4(leg.currents[k * 2].x, leg.currents[k * 2].z, leg.currents[k * 2 + 1].x, leg.currents[k * 2 + 1].z) : Vector4.zero;
      Shader.SetGlobalVectorArray("_AT_Current", curArr); Shader.SetGlobalVector("_AT_CurrentInfo", new Vector4(nc, leg.currentWidth, 0, 0)); Shader.SetGlobalColor("_AT_CurrentCol", leg.currentCol);
      if (currentTex) Shader.SetGlobalTexture("_AT_CurrentTex", currentTex); Shader.SetGlobalFloat("_AT_CurrentTexOn", currentTex ? 1f : 0f);
      VoyageAudio.I?.SetCurrent(0f);
      // twin cyan wake lines on the Cosmic Sea
      if (boat.cosmicWake != null) foreach (var t in boat.cosmicWake) if (t){ t.gameObject.SetActive(leg.landmark == Landmark.WhiteStar); t.Clear(); }
      MakeFogBanks(); MakeGusts(); MakeFragments(); MakeConstellation(); MakeObstacles(); MakeBuoys();
      VoyageAudio.I?.SetLeg(legIx);
      ApplySky(0f); Place();
      Debug.Log($"[AT] leg {legIx + 1} loaded: {leg.name} -> {leg.landmarkName} waves={leg.waveAmp:F2} currents={nc} obstacles={obsT.Length} buoys={buoyT.Length} frags={FragCount}/5 sky={(pano ? "pano" : "dome")}");
    }
    void MakeFogBanks(){   // soft fog billboards along both edges of the course and behind the start
      var root = new GameObject("FogBanks").transform; root.SetParent(legRoot, false); var d = leg.Dir; var rt = Right;
      for (int i = -2; i < 26; i++) for (int side = -1; side <= 1; side += 2){
        float a = i * leg.Length / 22f; var p = leg.start + d * a + rt * side * (leg.fogFull + 25f + Mathf.Sin(i * 1.7f) * 15f);
        Billboard("Fog", p + Vector3.up * 14f, 120f, fogMat, root);
      }
      for (int i = -4; i <= 4; i++) Billboard("Fog", leg.start - d * (leg.behind * 1.6f + 40f) + rt * i * 45f + Vector3.up * 14f, 120f, fogMat, root);
    }
    void MakeGusts(){
      int n = leg.gusts.Length; gustTaken = new bool[n]; gustT = new Transform[n];
      for (int i = 0; i < n; i++) gustT[i] = Billboard("Gust" + i, leg.gusts[i] + Vector3.up * 3f, 9f, gustMat, legRoot).transform;
    }
    void MakeFragments(){
      int n = leg.fragments.Length, mask = Progress.Frags(legIx); fragT = new Transform[n];
      for (int i = 0; i < n; i++){
        if (fragArt){   // Art Dev's star crystal, spun and bobbed, with an AT_Star glint
          fragT[i] = new GameObject("Fragment" + i).transform; fragT[i].SetParent(legRoot, false);
          var m = Prop(fragArt, propMat, fragT[i]); m.localScale = Vector3.one * 2.2f; m.name = "Crystal";
          Billboard("Glint", Vector3.zero, 4.5f, fragMat, fragT[i]).transform.localPosition = Vector3.zero;
        } else fragT[i] = Billboard("Fragment" + i, leg.fragments[i] + Vector3.up * 2.6f, 4.2f, fragMat, legRoot).transform;
        if ((mask & (1 << i)) != 0) fragT[i].gameObject.SetActive(false);
      }
    }
    Vector3 StarDir(int i){ var c = leg.constellation[i]; return Quaternion.Euler(-c.y, CourseYaw + c.x, 0f) * Vector3.forward; }
    void MakeConstellation(){
      constRoot = new GameObject("Constellation").transform; constRoot.SetParent(legRoot, false);
      if (!constDim && constMat){ constDim = new Material(constMat); constDim.name = "ConstellationDim"; var c = constMat.GetColor("_Color"); constDim.SetColor("_Color", new Color(c.r, c.g, c.b, c.a * 0.28f)); }
      int n = leg.constellation.Length, mask = Progress.Frags(legIx); starT = new Transform[n]; starLit = new bool[n]; edges = new LineRenderer[Mathf.Max(0, n - 1)];
      for (int i = 0; i < n; i++){ starT[i] = Billboard("Star" + i, Vector3.zero, StarDim, constDim, constRoot).transform; starT[i].localPosition = StarDir(i) * 3000f; }
      for (int i = 0; i < n - 1; i++){
        var lr = new GameObject("Edge" + i).AddComponent<LineRenderer>(); lr.transform.SetParent(constRoot, false); lr.useWorldSpace = false; lr.sharedMaterial = lineMat;
        lr.positionCount = 2; lr.SetPosition(0, StarDir(i) * 2990f); lr.SetPosition(1, StarDir(i + 1) * 2990f); lr.widthMultiplier = 40f; lr.alignment = LineAlignment.View; lr.textureMode = LineTextureMode.Stretch; lr.startColor = lr.endColor = new Color(1, 1, 1, 0);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false; lr.enabled = false; edges[i] = lr;
      }
      for (int i = 0; i < n; i++) if ((mask & (1 << i)) != 0) LightStar(i, false);
      constDone = n > 0 && Progress.Count(mask) >= n;
    }
    const float StarDim = 85f, StarLit = 150f;
    void LightStar(int i, bool fx){
      if (i >= starT.Length) return;
      starLit[i] = true; starT[i].GetComponent<Renderer>().sharedMaterial = constMat; starT[i].localScale = Vector3.one * StarLit;
      int mask = Progress.Frags(legIx);
      for (int e = 0; e < edges.Length; e++){ bool on = (mask & (1 << e)) != 0 && (mask & (1 << (e + 1))) != 0; if (on && !edges[e].enabled && !fx) edges[e].startColor = edges[e].endColor = Color.white; edges[e].enabled = on; }
      if (fx) for (int k = 0; k < 30; k++) Emit(sparkPS, cam.transform.position + StarDir(i) * 60f + Random.insideUnitSphere * 2f, Random.insideUnitSphere * 3f, Random.Range(0.4f, 1f), 1.5f, Color.white);
    }

    // procedural meteors and floes (runtime meshes, no primitives)
    static Mesh RockMesh(System.Random rng, float r){
      int lon = 10, lat = 7; var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
      var basalt = new Color(0.24f, 0.17f, 0.22f); var ember = new Color(1f, 0.5f, 0.2f, 0f);
      var noise = new float[(lon + 1) * (lat + 1)];
      for (int k = 0; k < noise.Length; k++) noise[k] = 0.78f + (float)rng.NextDouble() * 0.4f;
      for (int y = 0; y <= lat; y++) for (int x = 0; x <= lon; x++){
        int k = y * (lon + 1) + (x == lon ? 0 : x); if (y == 0 || y == lat) k = y * (lon + 1);
        float a = x * Mathf.PI * 2f / lon, b = y * Mathf.PI / lat; float rr = r * noise[k];
        v.Add(new Vector3(Mathf.Sin(b) * Mathf.Cos(a) * rr, Mathf.Cos(b) * rr * 0.8f, Mathf.Sin(b) * Mathf.Sin(a) * rr));
        var bc = Color.Lerp(basalt, basalt * 1.3f, noise[k] - 0.78f); bc.a = 1f; c.Add(noise[k] < 0.84f ? ember : bc);
      }
      for (int y = 0; y < lat; y++) for (int x = 0; x < lon; x++){ int i = y * (lon + 1) + x; t.AddRange(new[]{ i, i + lon + 1, i + 1, i + 1, i + lon + 1, i + lon + 2 }); }
      var m = new Mesh{ name = "Meteor" }; m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }
    static Mesh FloeMesh(System.Random rng, float r){
      int n = 9; var top = new Vector3[n]; for (int i = 0; i < n; i++){ float a = i * Mathf.PI * 2f / n + (float)rng.NextDouble() * 0.3f, rr = r * (0.72f + (float)rng.NextDouble() * 0.38f); top[i] = new Vector3(Mathf.Cos(a) * rr, 0.35f, Mathf.Sin(a) * rr); }
      var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
      void Tri(Vector3 a, Vector3 b, Vector3 d, Color col){ int i = v.Count; v.Add(a); v.Add(b); v.Add(d); c.Add(col); c.Add(col); c.Add(col); t.Add(i); t.Add(i + 1); t.Add(i + 2); }
      var snow = new Color(0.9f, 0.95f, 1f); var side = new Color(0.55f, 0.75f, 0.92f); var under = new Color(0.35f, 0.55f, 0.8f);
      var ctr = new Vector3(0, 0.42f, 0);
      for (int i = 0; i < n; i++){
        var a = top[i]; var b = top[(i + 1) % n]; var a2 = new Vector3(a.x * 0.9f, -0.6f, a.z * 0.9f); var b2 = new Vector3(b.x * 0.9f, -0.6f, b.z * 0.9f);
        Tri(ctr, b, a, Color.Lerp(snow, Color.white, (i % 3) * 0.1f)); Tri(a, b, b2, side); Tri(a, b2, a2, under);
      }
      var m = new Mesh{ name = "IceFloe" }; m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }
    void MakeObstacles(){
      int n = leg.meteors + leg.floes; bool ice = leg.floes > 0; var rng = new System.Random(1009 + legIx * 77);
      var pos = new List<Vector3>(); var rad = new List<float>();
      int guard = 0;
      while (pos.Count < n && guard++ < 2000){
        float f = 0.07f + (float)rng.NextDouble() * 0.86f, side = ((float)rng.NextDouble() * 2f - 1f) * 125f, r = ice ? 5f + (float)rng.NextDouble() * 7f : 2.2f + (float)rng.NextDouble() * 2.6f;
        var p = leg.start + leg.Dir * leg.Length * f + Right * side; bool ok = true;
        foreach (var q in leg.fragments) if ((q - p).magnitude < r + 16f) ok = false;
        foreach (var q in leg.gusts) if ((q - p).magnitude < r + 16f) ok = false;
        for (int k = 0; k < pos.Count; k++) if ((pos[k] - p).magnitude < r + rad[k] + 12f) ok = false;
        if ((leg.goal - p).magnitude < leg.arriveDist + 40f) ok = false;
        if (ok){ pos.Add(p); rad.Add(r); }
      }
      n = pos.Count; obsT = new Transform[n]; obsMir = new Transform[n]; obsPos = pos.ToArray(); obsBase = pos.ToArray(); obsR = rad.ToArray(); obsPh = new float[n]; obsCool = new float[n];
      for (int i = 0; i < n; i++){
        var art = ice ? floeArt : meteorArt;
        if (art){   // Art Dev's rock / floe (shared props atlas), scaled to the obstacle's radius
          float sc = obsR[i] / (ice ? 3.2f : 1.6f);
          obsT[i] = new GameObject(ice ? "Floe" + i : "Meteor" + i).transform; obsT[i].SetParent(legRoot, false); Prop(art, propMat, obsT[i]).localScale = Vector3.one * sc;
          obsMir[i] = new GameObject("Mirror").transform; obsMir[i].SetParent(legRoot, false); obsMir[i].localScale = new Vector3(1, -1, 1); Prop(art, propMirMat, obsMir[i]).localScale = Vector3.one * sc;
        } else {
          var mesh = ice ? FloeMesh(rng, obsR[i]) : RockMesh(rng, obsR[i]);
          obsT[i] = MeshObj(ice ? "Floe" + i : "Meteor" + i, mesh, ice ? iceMat : rockMat, legRoot).transform;
          obsMir[i] = MeshObj("Mirror", mesh, ice ? iceMirMat : rockMirMat, legRoot).transform; obsMir[i].localScale = new Vector3(1, -1, 1);
        }
        obsT[i].position = obsPos[i];
        obsPh[i] = (float)rng.NextDouble() * 6.28f; obsT[i].rotation = obsMir[i].rotation = Quaternion.Euler(0, obsPh[i] * 57f, 0);
      }
    }
    void MakeBuoys(){
      int n = leg.buoys.Length; buoyT = new Transform[n]; buoyTaken = new bool[n];
      for (int i = 0; i < n; i++){
        var root = new GameObject("StarBuoy" + i).transform; root.SetParent(legRoot, false); root.position = leg.buoys[i]; root.rotation = Quaternion.LookRotation(leg.Dir);
        if (buoyArt){ Prop(buoyArt, propMat, root).localScale = Vector3.one * 1.6f; var g = Billboard("Glint", Vector3.zero, 5f, buoyMat, root).transform; g.localPosition = new Vector3(0, 2.4f * 1.6f, 0); g.localScale = Vector3.one * 2f; }   // the hoop: a 4.8 m opening at this scale
        else {
          for (int s = -1; s <= 1; s += 2){ var b = Billboard("Post", Vector3.zero, 3.2f, buoyMat, root).transform; b.localPosition = new Vector3(s * 6f, 2.4f, 0); }
          var c = Billboard("Core", Vector3.zero, 1.6f, buoyMat, root).transform; c.localPosition = new Vector3(0, 5f, 0);
        }
        buoyT[i] = root;
      }
    }

    // ---------- per frame ----------
    void LegTick(float dt){
      bool sailing = state == State.Sail;
      // gusts (Frost Mirror: caught in quick succession they chain into a longer boost)
      for (int i = 0; i < gustT.Length; i++){
        if (gustTaken[i]) continue; var p = leg.gusts[i];
        gustT[i].localScale = Vector3.one * (8f + Mathf.Sin(Time.time * 2f + i) * 1.2f);
        if (Random.value < dt * 20f){ var o = Random.insideUnitSphere * 9f; o.y = Mathf.Abs(o.y) * 0.6f + 0.5f; Emit(sparkPS, p + o, new Vector3(-o.z, 0.8f, o.x) * 0.25f, Random.Range(0.5f, 1.3f), Random.Range(1f, 2f), Color.Lerp(leg.glow, Color.white, Random.value * 0.6f)); }
        if (sailing && Flat(boat.Pos, p) < 14f){
          gustTaken[i] = true; gustsCaught++; gustT[i].gameObject.SetActive(false);
          chain = leg.gustChains && Time.time - lastGustAt < 6f ? chain + 1 : 1; lastGustAt = Time.time; chainMax = Mathf.Max(chainMax, chain);
          boat.boost = Mathf.Min(7f + (chain - 1) * 1.5f, 13f);
          for (int k = 0; k < 40; k++) Emit(sparkPS, boat.Pos + Vector3.up * 3f + Random.insideUnitSphere * 3f, boat.Fwd * boat.speed * 0.6f + Random.insideUnitSphere * 4f, Random.Range(0.6f, 1.4f), Random.Range(0.8f, 1.6f), Color.white);
          VoyageAudio.I?.Play(chain >= 2 ? "boost" : "gust");
          Debug.Log(chain >= 2 ? $"[AT] gust chain x{chain} boost={boat.boost:F1}" : $"[AT] star gust {i + 1} caught ({gustsCaught}/{gustT.Length}) speed={boat.speed:F1}");
        }
      }
      // fragments
      for (int i = 0; i < fragT.Length; i++){
        var f = fragT[i]; if (!f.gameObject.activeSelf) continue;
        f.position = leg.fragments[i] + Vector3.up * (2.6f + Mathf.Sin(Time.time * 1.6f + i) * 0.4f + Waves.H(leg.fragments[i].x, leg.fragments[i].z));
        if (fragArt){ var cr = f.Find("Crystal"); if (cr) cr.localRotation = Quaternion.Euler(0, Time.time * 90f + i * 40f, 0); f.position += Vector3.down * 1f; }
        else f.localScale = Vector3.one * (4.2f + Mathf.Sin(Time.time * 3f + i) * 0.6f);
        if (Random.value < dt * 10f) Emit(sparkPS, f.position + Random.insideUnitSphere * 2f, Vector3.up * 0.6f, Random.Range(0.3f, 0.7f), 1.2f, new Color(0.8f, 0.95f, 1f));
        if (sailing && Flat(boat.Pos, leg.fragments[i]) < 9f){
          f.gameObject.SetActive(false); Progress.AddFrag(legIx, i); LightStar(i, true);
          for (int k = 0; k < 40; k++) Emit(sparkPS, f.position + Random.insideUnitSphere, Random.insideUnitSphere * 6f + Vector3.up * 3f, Random.Range(0.4f, 1f), Random.Range(0.8f, 1.6f), Color.white);
          VoyageAudio.I?.Play("frag");
          Debug.Log($"[AT] star fragment {i + 1} found ({FragCount}/{fragT.Length}): a star of the constellation lights");
          if (!constDone && FragCount >= leg.constellation.Length){ constDone = true; constAt = Time.time; flash = Mathf.Max(flash, 0.5f); VoyageAudio.I?.Play("constellation"); Debug.Log($"[AT] constellation complete: {leg.name}"); }
        }
      }
      if (constDone && starT.Length > 0){ float pulse = Time.time - constAt < 4f ? 1f + 0.5f * Mathf.Sin((Time.time - constAt) * 6f) * (1f - (Time.time - constAt) / 4f) : 1f; foreach (var sTr in starT) sTr.localScale = Vector3.one * StarLit * pulse; }
      foreach (var e in edges) if (e && e.enabled && e.startColor.a < 1f){ var c = e.startColor; c.a = Mathf.MoveTowards(c.a, 1f, dt * 0.8f); e.startColor = e.endColor = c; }   // lines fade in
      foreach (var sTr in starT) if (sTr) sTr.LookAt(sTr.position + (sTr.position - cam.transform.position));
      // currents: inside a stream it carries you along (you can still steer across it)
      bool inCur = false; Vector3 push = Vector3.zero;
      for (int s = 0; s * 2 + 1 < leg.currents.Length; s++){
        var a = leg.currents[s * 2]; var b = leg.currents[s * 2 + 1]; var ab = b - a; float L = ab.magnitude; if (L < 1f) continue; var d = ab / L;
        var ap = boat.Pos - a; ap.y = 0f; float u = Mathf.Clamp(Vector3.Dot(ap, d), 0f, L); float dist = (ap - d * u).magnitude;
        if (dist < leg.currentWidth){ inCur = true; push += d * leg.currentPush * (1f - (dist / leg.currentWidth) * (dist / leg.currentWidth)); }
      }
      if (inCur && sailing) boat.drift = Vector3.MoveTowards(boat.drift, push, 6f * dt);
      if (sailing && inCur != inCurrent){ inCurrent = inCur; if (inCur) currentRides++; Debug.Log($"[AT] current {(inCur ? "ride on" : "off")} (rides {currentRides}) drift={boat.drift.magnitude:F1}"); }
      VoyageAudio.I?.SetCurrent(inCurrent && sailing ? 1f : 0f);
      // obstacles: meteors drift side to side and bob on the swell, floes sit still; a bump only slows you and rocks the ship
      creakCool -= dt; bool ice = leg.floes > 0;
      for (int i = 0; i < obsT.Length; i++){
        obsCool[i] -= dt;
        var p = obsBase[i];
        if (!ice){ float A = 14f; p += Right * Mathf.Sin(Time.time * leg.meteorDrift / A + obsPh[i]) * A; }
        float h = Waves.H(p.x, p.z); var top = new Vector3(p.x, h + (ice ? 0f : obsR[i] * 0.25f), p.z);
        obsPos[i] = p; obsT[i].position = top; obsMir[i].position = new Vector3(p.x, 2f * h - top.y, p.z);
        if (!ice){ var rot = Quaternion.Euler(Time.time * 6f + obsPh[i] * 40f, obsPh[i] * 57f, Time.time * 4f); obsT[i].rotation = rot; obsMir[i].rotation = new Quaternion(-rot.x, rot.y, -rot.z, rot.w);
          if (Random.value < dt * 3f) Emit(sparkPS, top + Random.insideUnitSphere * obsR[i], Vector3.up * 1.2f, Random.Range(0.3f, 0.6f), 1.5f, new Color(1f, 0.6f, 0.3f)); }
        float dd = Flat(boat.Pos, p);
        if (sailing && dd < obsR[i] + 1.8f && obsCool[i] <= 0f){
          obsCool[i] = 1.5f; float before = boat.speed; boat.Bump(boat.Pos - p, 1f); bumps++;
          for (int k = 0; k < 30; k++) Emit(sparkPS, Vector3.Lerp(boat.Pos, top, 0.5f) + Vector3.up + Random.insideUnitSphere, Random.insideUnitSphere * 5f + Vector3.up * 2f, Random.Range(0.4f, 0.9f), 1f, ice ? new Color(0.85f, 0.95f, 1f) : new Color(1f, 0.6f, 0.3f));
          VoyageAudio.I?.Play(ice ? "creak" : "bump");
          Debug.Log($"[AT] bump {(ice ? "ice floe" : "meteor")} {i + 1}: speed {before:F1} -> {boat.speed:F1}, the ship rocks (bumps {bumps})");
        } else if (ice && sailing && dd < obsR[i] + 8f && creakCool <= 0f && boat.speed > 2f){
          creakCool = 5f; VoyageAudio.I?.Play("creak"); if (!nearIceLogged){ nearIceLogged = true; Debug.Log($"[AT] ice creak near floe {i + 1}"); }
        }
      }
      // star buoys and the boost run (sail between the posts; quick successive buoys build the boost)
      for (int i = 0; i < buoyT.Length; i++){
        if (buoyTaken[i]) continue; var b = leg.buoys[i];
        buoyT[i].position = new Vector3(b.x, Waves.H(b.x, b.z), b.z);
        if (sailing && Flat(boat.Pos, b) < 7f){
          buoyTaken[i] = true; buoysTaken++; buoyT[i].gameObject.SetActive(false);
          runChain = Time.time - lastBuoyAt < 7f ? runChain + 1 : 1; lastBuoyAt = Time.time; chainMax = Mathf.Max(chainMax, runChain);
          boat.boost = Mathf.Min(6f + (runChain - 1) * 1.3f, 14f);
          for (int k = 0; k < 40; k++) Emit(sparkPS, b + Vector3.up * 3f + Random.insideUnitSphere * 4f, boat.Fwd * boat.speed * 0.5f + Random.insideUnitSphere * 5f, Random.Range(0.5f, 1.2f), 1.2f, new Color(0.6f, 1f, 1f));
          VoyageAudio.I?.Play("boost");
          Debug.Log($"[AT] star buoy {i + 1} ({buoysTaken}/{buoyT.Length}) run x{runChain} boost={boat.boost:F1}");
        }
      }
      // boost: motes off the ship, and warp streaks rushing past when it's a big one
      if (boat.boost > 0.5f && Random.value < dt * 40f) Emit(motesPS, boat.Pos + Vector3.up * Random.Range(1f, 6f) + Random.insideUnitSphere * 2f, -boat.Fwd * 2f, Random.Range(0.4f, 0.9f), 1.2f, Color.white);
      bool st = boat.boost > 5f && sailing;
      if (st && !streaking) Debug.Log($"[AT] warp streaks on boost={boat.boost:F1}");
      streaking = st;
      if (st){
        if (!warpPS) warpPS = MakePS("WarpStreaks", 300, true);
        var cf = cam.transform.forward; cf.y = 0f; cf.Normalize(); var cr = new Vector3(cf.z, 0, -cf.x);
        for (int k = 0; k < 3; k++) if (Random.value < dt * 60f){
          var p = cam.transform.position + cf * Random.Range(25f, 55f) + cr * Random.Range(-14f, 14f) + Vector3.up * Random.Range(-1.5f, 7f);
          Emit(warpPS, p, -cf * Random.Range(70f, 110f), Random.Range(0.12f, 0.25f), 0.45f, Color.Lerp(leg.glow, new Color(0.6f, 1f, 1f), Random.value));
        }
      }
      // landmark idle motion
      var anim = landmarkAnim[legIx];
      if (anim && state <= State.Sail){
        if (leg.landmark == Landmark.FallenStar) anim.localRotation = Quaternion.Euler(0, Time.time * 12f, 0);
        if (leg.landmark == Landmark.WhiteStar){ anim.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 1.3f) * 0.04f); if (landmarkAnimMir[legIx]) landmarkAnimMir[legIx].localScale = anim.localScale; }
        if (leg.landmark == Landmark.GlacierBell){ anim.localRotation = Quaternion.identity; if (clapper) clapper.localRotation = Quaternion.identity; }
      }
    }
    static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    void Arrive(){
      Progress.Finish(legIx, legTime);
      Debug.Log($"[AT] arrived at the {leg.landmarkName} t={legTime:F1}s gusts={gustsCaught}/{leg.gusts.Length} chimes={chimes} frags={FragCount}/5 unlocked={Progress.Unlocked}");
      VoyageAudio.I?.Arrival(); flash = 1f; turning = pulling = false; VoyageAudio.I?.SetCurrent(0f);
      OnLandmarkArrive(leg.landmark);
      var top = leg.goal + Vector3.up * landmarkTop[legIx];
      for (int i = 0; i < 160; i++){ float a = i / 160f * Mathf.PI * 2f; var v = new Vector3(Mathf.Cos(a), 0.05f, Mathf.Sin(a));
        Emit(burstPS, leg.goal + Vector3.up * 1.5f + v * 6f, v * Random.Range(14f, 22f), Random.Range(2f, 4f), Random.Range(2.5f, 4f), leg.glow); }
      for (int i = 0; i < 80; i++) Emit(burstPS, top + Vector3.down * Random.Range(0f, landmarkTop[legIx] * 0.8f), new Vector3(Random.Range(-3f, 3f), Random.Range(4f, 12f), Random.Range(-3f, 3f)), Random.Range(1.5f, 3f), Random.Range(2f, 4f), Color.Lerp(leg.glow, Color.white, 0.4f));
    }
    /// <summary>Per-landmark arrival beat. The extension point for bigger arrivals (e.g. a Cosmic Sea set piece later).</summary>
    void OnLandmarkArrive(Landmark l){
      switch (l){
        case Landmark.GlacierBell: { bool pl = VoyageAudio.I != null && VoyageAudio.I.Play("bell"); Debug.Log($"[AT] the Glacier Bell rings{(pl ? "" : " (silent)")}"); break; }
        case Landmark.WhiteStar:
          for (int i = 0; i < 200; i++) Emit(burstPS, leg.goal + Vector3.up * landmarkTop[legIx], Random.onUnitSphere * Random.Range(10f, 30f), Random.Range(2f, 5f), Random.Range(2f, 4f), Color.Lerp(Color.white, new Color(0.6f, 1f, 1f), Random.value));
          Debug.Log("[AT] the White Star blazes"); break;
        case Landmark.Gate: Debug.Log("[AT] the Aurora Gate opens"); break;
        case Landmark.FallenStar: Debug.Log("[AT] the Fallen Star wakes"); break;
      }
    }
    void ArrivalAnim(float t){
      var anim = landmarkAnim[legIx]; if (!anim) return;
      var mir = landmarkAnimMir[legIx];
      switch (leg.landmark){
        case Landmark.GlacierBell: {   // the bell swings, the clapper lags behind it
          var q = Quaternion.Euler(Mathf.Sin(t * 3.2f) * 18f * Mathf.Exp(-t * 0.45f), 0, 0); anim.localRotation = q; if (mir) mir.localRotation = q;
          var qc = Quaternion.Euler(Mathf.Sin(t * 3.2f - 0.9f) * 26f * Mathf.Exp(-t * 0.45f), 0, 0); if (clapper) clapper.localRotation = qc; if (clapperMir) clapperMir.localRotation = qc;
          break; }
        case Landmark.WhiteStar: anim.localScale = Vector3.one * (1f + 0.25f * Mathf.Clamp01(t / 1.5f)); if (mir) mir.localScale = anim.localScale; break;
        case Landmark.FallenStar: anim.localRotation = Quaternion.Euler(0, Time.time * 40f, 0); if (mir) mir.localRotation = anim.localRotation; break;
        case Landmark.Gate: anim.localScale = new Vector3(1f, 1f + 0.08f * Mathf.Sin(t * 4f) * Mathf.Exp(-t * 0.6f), 1f); break;
      }
    }

    readonly Vector4[] wake = new Vector4[8]; float wakeT;
    void Wake(float dt){
      wakeT += dt; var stern = boat.Pos - boat.Fwd * 3.2f; float sp = Mathf.Clamp01(boat.speed / boat.maxSpeed);
      if (wakeT > 0.35f){ wakeT = 0f; for (int i = wake.Length - 1; i > 0; i--) wake[i] = wake[i - 1]; wake[0] = new Vector4(stern.x, 0, stern.z, sp); }
      for (int i = 0; i < wake.Length; i++){ var w = wake[i]; w.w *= Mathf.Lerp(1f, 0.78f, i / 7f); wake[i] = w; }
      var sw = new Vector4[8]; for (int i = 0; i < 8; i++){ sw[i] = wake[i]; sw[i].w = wake[i].w * (1f - i / 8f) * 0.8f; }
      sw[0] = new Vector4(stern.x, 0, stern.z, sp * 0.8f);
      Shader.SetGlobalVectorArray("_AT_Wake", sw);
      if (sp > 0.1f && Random.value < dt * 25f * sp) Emit(motesPS, stern + boat.transform.right * Random.Range(-1f, 1f) + Vector3.up * (0.3f + boat.WaveH), -boat.Fwd * 1f + Vector3.up * 0.3f, Random.Range(0.3f, 0.7f), 2.5f, Color.Lerp(leg.glow, Color.white, 0.5f));
    }

    void ApplySky(float p){
      float k = Mathf.SmoothStep(0f, 1f, p);
      Shader.SetGlobalColor("_AT_SkyTop", Color.Lerp(leg.top0, leg.top1, k)); Shader.SetGlobalColor("_AT_SkyMid", Color.Lerp(leg.mid0, leg.mid1, k));
      Shader.SetGlobalColor("_AT_SkyLow", Color.Lerp(leg.low0, leg.low1, k)); Shader.SetGlobalColor("_AT_Horizon", Color.Lerp(leg.hor0, leg.hor1, k));
      Shader.SetGlobalColor("_AT_GlowCol", leg.glow);
      bool arrived = state >= State.Arrive;
      float stars = arrived ? Mathf.Lerp(leg.stars1, 1.15f, Mathf.Clamp01((state != State.Arrive ? 3f : stateT) / 3f)) : Mathf.Lerp(leg.stars0, leg.stars1, k);
      Shader.SetGlobalFloat("_AT_StarAmount", stars);
      if (skyboxMat){ float ex = Mathf.Lerp(1f, 0.74f, k) * (1f + flash * 0.25f); skyboxMat.SetFloat("_Exposure", ex); Shader.SetGlobalFloat("_AT_PanoGain", ex); }
      float arr = arrived ? Mathf.Clamp01((state != State.Arrive ? 4f : stateT) / 1.5f) : 0f;
      float s = Mathf.Sin(Time.time * 1.4f);
      var li = landmarkLights[legIx]; if (li) li.intensity = 4f + arr * 10f + s * 0.3f;
      var m = landmarkMats[legIx];
      if (m){ if (m.HasProperty("_GlowBoost")) m.SetFloat("_GlowBoost", 1.2f + 1.5f * arr + s * 0.1f); else if (m.HasProperty("_EmissionColor")){ float b = landmarkEm[legIx] > 0f ? landmarkEm[legIx] : 4f; m.SetColor("_EmissionColor", Color.white * (b * (1f + arr) + s * 0.25f)); } }
      var top = leg.goal + Vector3.up * landmarkTop[legIx]; var gd = (top - (cam ? cam.transform.position : boat.Pos)).normalized;
      float strength = 0.6f + 0.6f * Mathf.Clamp01(1f - GoalDist / leg.Length) + (arrived ? 1f : 0f) + (lined ? 0.25f : 0f);
      Shader.SetGlobalVector("_AT_GoalDir", new Vector4(gd.x, gd.y, gd.z, strength));
      Shader.SetGlobalFloat("_AT_Flash", flash);
      var halo = halos[legIx]; if (halo){ float hs = haloBase[legIx] * (1f + arr * 1.6f) * (1f + s * 0.05f); halo.localScale = Vector3.one * hs; }
    }
  }
}
