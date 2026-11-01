using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using AstralTide;

/// <summary>Headless scene setup + WebGL build (the Neon Hive / Salt and Glory pattern). Usage:
/// Unity -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod BuildWeb.Build -logFile build.log
/// (no -quit; this calls EditorApplication.Exit). AT_DEV=1: development build in Builds/WebGLDev. AT_QA=1: release-like build
/// with the QA hook (window.atUnity + Voyage.QA*) in Builds/WebGLQA. Plain: release into docs/ with neither.
/// M1 art is procedural (meshes with vertex colours built here, a shader sky); Art Dev's files are picked up by name when they
/// land (AT_DuskShallows_SkyPano, AT_StarCompass_Strip / _GoalMarker), everything else falls back.</summary>
public static class BuildWeb {
  const string ScenePath = "Assets/Scenes/Main.unity", Gen = "Assets/Generated";

  static T Load<T>(string path) where T : Object { var a = AssetDatabase.LoadAssetAtPath<T>(path); if (!a) Debug.LogWarning("[Setup] missing " + path); return a; }
  /// <summary>First asset of a type under a folder whose file name contains the key (Art / Audio Dev's files drop in by name), or null.</summary>
  static T Find<T>(string folder, string key) where T : Object {
    if (!AssetDatabase.IsValidFolder(folder)){ Debug.LogWarning($"[Setup] no {folder} for {key} (fallback)"); return null; }
    var paths = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[]{ folder }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
    var hit = paths.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == key) ?? paths.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).Contains(key));
    if (hit != null){ Debug.Log($"[Setup] art: {key} <- {hit}"); return AssetDatabase.LoadAssetAtPath<T>(hit); }
    Debug.LogWarning($"[Setup] no {typeof(T).Name} {key} in {folder} (fallback)"); return null;
  }

  static UniversalRenderPipelineAsset EnsureURP(){
    var path = Gen + "/URP_Asset.asset";
    var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
    if (!asset){
      var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
      AssetDatabase.CreateAsset(rd, Gen + "/URP_Renderer.asset");
      asset = UniversalRenderPipelineAsset.Create(rd);
      AssetDatabase.CreateAsset(asset, path);
    }
    asset.supportsHDR = true; asset.msaaSampleCount = 2;   // HDR for the emissive lanterns under bloom
    var rdata = asset.rendererDataList.Length > 0 ? asset.rendererDataList[0] as UniversalRendererData : null;
    if (rdata && !rdata.postProcessData){
      rdata.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
      Debug.Log("[Setup] renderer post-process data " + (rdata.postProcessData ? "set" : "MISSING")); EditorUtility.SetDirty(rdata);
    }
    EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
    GraphicsSettings.defaultRenderPipeline = asset;
    for (int i = 0; i < QualitySettings.names.Length; i++){ QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = asset; }
    return asset;
  }
  static Material Mat(string name, string shader, int queue, System.Action<Material> set = null){
    var path = $"{Gen}/{name}.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path); var sh = Shader.Find(shader);
    if (!sh) throw new System.Exception("shader missing: " + shader);
    if (!m){ m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
    m.shader = sh; set?.Invoke(m); m.renderQueue = queue; EditorUtility.SetDirty(m); return m;
  }
  static Mesh SaveMesh(string name, Mesh mesh){
    var path = $"{Gen}/{name}.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
    if (old){ old.Clear(); EditorUtility.CopySerialized(mesh, old); EditorUtility.SetDirty(old); return old; }
    AssetDatabase.CreateAsset(mesh, path); return mesh;
  }

  // ---------- procedural meshes (no runtime CreatePrimitive) ----------
  class MB {   // tiny mesh builder: flat-shaded triangles with vertex colours (alpha < 1 = emissive in AstralTide/Lit)
    public readonly List<Vector3> v = new(); public readonly List<Color> c = new(); public readonly List<int> t = new(); public readonly List<Vector2> uv = new();
    public void Tri(Vector3 a, Vector3 b, Vector3 d, Color col){ int i = v.Count; v.Add(a); v.Add(b); v.Add(d); c.Add(col); c.Add(col); c.Add(col); uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.up); t.Add(i); t.Add(i + 1); t.Add(i + 2); }
    public void Quad(Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color col){ Tri(a, b, d, col); Tri(a, d, e, col); }
    public void Box(Vector3 lo, Vector3 hi, Color col){
      var p = new Vector3[8]; for (int i = 0; i < 8; i++) p[i] = new Vector3((i & 1) == 0 ? lo.x : hi.x, (i & 2) == 0 ? lo.y : hi.y, (i & 4) == 0 ? lo.z : hi.z);
      Quad(p[0], p[2], p[3], p[1], col); Quad(p[4], p[5], p[7], p[6], col); Quad(p[0], p[1], p[5], p[4], col); Quad(p[2], p[6], p[7], p[3], col); Quad(p[0], p[4], p[6], p[2], col); Quad(p[1], p[3], p[7], p[5], col);
    }
    /// <summary>A ring band between two heights (n sides, radii r0 at y0 and r1 at y1).</summary>
    public void Band(int n, float y0, float r0, float y1, float r1, System.Func<int, Color> col, float jitter = 0f, int seed = 0){
      var rnd = new System.Random(seed);
      float J() => jitter <= 0f ? 0f : (float)(rnd.NextDouble() - 0.5) * jitter;
      var a0 = new Vector3[n]; var a1 = new Vector3[n];
      for (int i = 0; i < n; i++){ float a = i * Mathf.PI * 2f / n; a0[i] = new Vector3(Mathf.Cos(a) * (r0 + J()), y0, Mathf.Sin(a) * (r0 + J())); a1[i] = new Vector3(Mathf.Cos(a) * (r1 + J()), y1, Mathf.Sin(a) * (r1 + J())); }
      for (int i = 0; i < n; i++){ int j = (i + 1) % n; Quad(a0[i], a1[i], a1[j], a0[j], col(i)); }
    }
    public Mesh Build(string name){
      var m = new Mesh{ name = name }; m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }
  }
  static Color Col(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

  /// <summary>The small original ship: a rounded hull with a gold rail, a deck, one mast with a yard, a lantern on a bow post
  /// (emissive). The sail is its own mesh (pivot at the yard) so it can shorten.</summary>
  static Mesh ShipHull(){
    var b = new MB(); int N = 14; var hull = Col(0.36f, 0.24f, 0.34f); var rail = Col(0.85f, 0.68f, 0.4f); var deck = Col(0.62f, 0.47f, 0.38f);
    Vector3[] Sec(float z){
      float u = (z + 3.6f) / 7.9f;                                     // 0 stern .. 1 bow
      float w = 1.45f * Mathf.Sqrt(Mathf.Clamp01(1f - Mathf.Pow((u - 0.42f) / 0.6f, 2f))) + (u < 0.1f ? 0.25f : 0f) * (1f - u / 0.1f);
      w = Mathf.Max(w, 0.04f); float top = 1.0f + Mathf.Pow(u, 3f) * 0.6f + (u < 0.15f ? (0.15f - u) * 2f : 0f), keel = -0.55f * Mathf.Sin(Mathf.Clamp01(u * 1.1f) * Mathf.PI * 0.95f) - 0.05f;
      return new[]{ new Vector3(-w, top, z), new Vector3(-w * 0.92f, 0.25f, z), new Vector3(-w * 0.45f, keel * 0.8f, z), new Vector3(0, keel, z), new Vector3(w * 0.45f, keel * 0.8f, z), new Vector3(w * 0.92f, 0.25f, z), new Vector3(w, top, z) };
    }
    var secs = new Vector3[N + 1][]; for (int i = 0; i <= N; i++) secs[i] = Sec(-3.6f + 7.9f * i / N);
    for (int i = 0; i < N; i++) for (int k = 0; k < 6; k++){
      var c = k == 0 || k == 5 ? Color.Lerp(hull, rail, 0.0f) : hull;
      b.Quad(secs[i][k], secs[i + 1][k], secs[i + 1][k + 1], secs[i][k + 1], c);
    }
    for (int i = 0; i < N; i++){   // deck and rail strips
      var l0 = secs[i][0]; var l1 = secs[i + 1][0]; var r0 = secs[i][6]; var r1 = secs[i + 1][6];
      b.Quad(l0 + Vector3.down * 0.15f, r0 + Vector3.down * 0.15f, r1 + Vector3.down * 0.15f, l1 + Vector3.down * 0.15f, deck);
      b.Quad(l0, l1, l1 + Vector3.up * 0.12f, l0 + Vector3.up * 0.12f, rail); b.Quad(r0, r0 + Vector3.up * 0.12f, r1 + Vector3.up * 0.12f, r1, rail);
    }
    var s = secs[0]; for (int k = 0; k < 6; k++) b.Tri(s[k], s[k + 1], new Vector3(0, 0.6f, -3.6f), hull);   // transom
    var wood = Col(0.3f, 0.22f, 0.2f);
    b.Box(new Vector3(-0.11f, 0.8f, 0.3f), new Vector3(0.11f, 9.6f, 0.52f), wood);           // mast
    b.Box(new Vector3(-2.3f, 8.95f, 0.5f), new Vector3(2.3f, 9.1f, 0.65f), wood);             // yard
    b.Box(new Vector3(-0.06f, 1.4f, 3.95f), new Vector3(0.06f, 2.6f, 4.07f), wood);            // bow post
    b.Box(new Vector3(-0.22f, 2.6f, 3.8f), new Vector3(0.22f, 3.15f, 4.24f), Col(1f, 0.85f, 0.55f, 0f));   // lantern glass (emissive)
    b.Box(new Vector3(-0.26f, 3.15f, 3.76f), new Vector3(0.26f, 3.25f, 4.28f), rail);
    b.Box(new Vector3(-0.6f, 0.85f, -2.8f), new Vector3(0.6f, 1.6f, -1.6f), Col(0.5f, 0.36f, 0.4f));        // little cabin
    b.Box(new Vector3(-0.2f, 1.1f, -1.62f), new Vector3(0.2f, 1.4f, -1.58f), Col(1f, 0.8f, 0.5f, 0.2f));    // its lit window
    return b.Build("ShipHull");
  }
  static Mesh ShipSail(){   // a gently billowed square sail hanging from y = 0 (the yard) down 7 m; cream, warmer at the foot
    var b = new MB(); int nx = 6, ny = 8;
    Vector3 P(int i, int j){ float u = i / (float)nx, v = j / (float)ny; float w = Mathf.Lerp(2.15f, 2.45f, v);
      return new Vector3((u - 0.5f) * 2f * w, -v * 7f, 0.2f + Mathf.Sin(u * Mathf.PI) * Mathf.Sin(Mathf.Clamp01(v * 1.1f) * Mathf.PI * 0.9f) * 0.75f); }
    for (int j = 0; j < ny; j++) for (int i = 0; i < nx; i++){
      var c = Color.Lerp(Col(0.97f, 0.93f, 0.88f), Col(0.98f, 0.86f, 0.78f), j / (float)ny);
      if (j == ny / 2 && i % 2 == 0) c *= 0.96f;
      b.Quad(P(i, j), P(i, j + 1), P(i + 1, j + 1), P(i + 1, j), c);
    }
    return b.Build("ShipSail");
  }
  /// <summary>The Lantern Spire: a rocky islet, a tall tapering eight-sided stone tower with glowing window bands, a gallery,
  /// the lantern room (emissive) and a pointed cap. About 66 m tall.</summary>
  static Mesh Spire(){
    var b = new MB(); var rock = Col(0.3f, 0.26f, 0.38f); var stone = Col(0.66f, 0.6f, 0.76f); var dark = Col(0.24f, 0.2f, 0.34f); var lit = Col(1f, 0.85f, 0.55f, 0f);
    b.Band(11, -3f, 30f, 2.5f, 22f, i => rock, 6f, 1); b.Band(11, 2.5f, 22f, 6f, 12f, i => Color.Lerp(rock, stone, 0.2f), 4f, 2); b.Band(11, 6f, 12f, 7f, 7.5f, i => rock, 1f, 3);
    float[] ys = { 7, 18, 19.5f, 30, 31.5f, 42, 47 }; float R(float y) => Mathf.Lerp(7f, 4.4f, (y - 7f) / 40f);
    for (int k = 0; k < ys.Length - 1; k++){
      bool band = k == 1 || k == 3; float y0 = ys[k], y1 = ys[k + 1];
      b.Band(8, y0, R(y0), y1, R(y1), i => band ? (i % 2 == 0 ? lit : stone) : stone);
    }
    b.Band(8, 47f, 4.4f, 48f, 6.6f, i => dark); b.Band(8, 48f, 6.6f, 48.6f, 6.6f, i => dark); b.Band(8, 48.6f, 6.6f, 48.6f, 4.8f, i => dark);
    b.Band(8, 48.6f, 4.8f, 56f, 4.6f, i => i % 2 == 0 ? lit : Color.Lerp(lit, Col(1f, 0.95f, 0.8f, 0f), 0.5f));
    b.Band(8, 56f, 5.6f, 57f, 5.6f, i => dark); b.Band(8, 57f, 5.6f, 67f, 0.05f, i => Color.Lerp(dark, stone, 0.25f));
    b.Box(new Vector3(-0.15f, 66f, -0.15f), new Vector3(0.15f, 70f, 0.15f), lit);
    return b.Build("LanternSpire");
  }
  static Mesh Quad(){
    var m = new Mesh{ name = "GlowQuad" };
    m.vertices = new[]{ new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0) };
    m.uv = new[]{ new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
    m.colors = new[]{ Color.white, Color.white, Color.white, Color.white };
    m.triangles = new[]{ 0, 1, 2, 0, 2, 3 }; m.RecalculateBounds(); m.bounds = new Bounds(Vector3.zero, Vector3.one * 2f); return m;
  }
  static Mesh SeaQuad(float size){
    var m = new Mesh{ name = "SeaQuad" }; float h = size / 2f;
    m.vertices = new[]{ new Vector3(-h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(h, 0, -h) };
    m.triangles = new[]{ 0, 1, 2, 0, 2, 3 }; m.RecalculateBounds(); m.bounds = new Bounds(Vector3.zero, new Vector3(size, 2, size)); return m;
  }
  /// <summary>M2 sea: concentric rings around the camera, dense near the centre (about 1 m) and coarse toward the horizon
  /// (~4 km), so the swell can be displaced in the vertex shader. Bounds are padded for the displacement.</summary>
  static Mesh SeaRings(int rings, int segs){
    var m = new Mesh{ name = "SeaRings" }; var v = new List<Vector3>{ Vector3.zero }; var t = new List<int>();
    float r = 0f, step = 0.8f;
    for (int i = 1; i <= rings; i++){
      r += step; step *= 1.035f; float rr = i == rings ? Mathf.Max(r, 4000f) : r;
      for (int k = 0; k < segs; k++){ float a = k * Mathf.PI * 2f / segs + (i % 2) * Mathf.PI / segs; v.Add(new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr)); }
    }
    for (int k = 0; k < segs; k++) t.AddRange(new[]{ 0, 1 + (k + 1) % segs, 1 + k });
    for (int i = 1; i < rings; i++){
      int b0 = 1 + (i - 1) * segs, b1 = 1 + i * segs;
      for (int k = 0; k < segs; k++){ int k1 = (k + 1) % segs; t.AddRange(new[]{ b0 + k, b0 + k1, b1 + k1, b0 + k, b1 + k1, b1 + k }); }
    }
    m.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
    m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateBounds(); m.bounds = new Bounds(Vector3.zero, new Vector3(8200, 40, 8200)); return m;
  }
  /// <summary>The water mask lid: the convex outline (XZ) of the hull's vertices around the waterline, shrunk a little, as a flat
  /// polygon at lidY. Drawn depth-only between the ship and the sea, so no water shows inside the hull.</summary>
  static Mesh HullLid(Transform model, float lidY){
    var pts = new List<Vector2>();
    foreach (var mf in model.GetComponentsInChildren<MeshFilter>()){
      if (!mf.sharedMesh || mf.GetComponentInParent<Transform>().name.Contains("Sail") || mf.name.Contains("Sail") || mf.name.Contains("Lantern")) continue;
      foreach (var vv in mf.sharedMesh.vertices){ var lp = model.InverseTransformPoint(mf.transform.TransformPoint(vv)); if (lp.y > -0.3f && lp.y < lidY + 0.45f) pts.Add(new Vector2(lp.x, lp.z)); }
    }
    if (pts.Count < 3){ Debug.LogWarning("[Setup] hull lid: no waterline vertices"); return null; }
    pts = pts.Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToList();
    float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
    var hull = new List<Vector2>();
    foreach (var p in pts){ while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
    int lower = hull.Count + 1;
    for (int i = pts.Count - 2; i >= 0; i--){ var p = pts[i]; while (hull.Count >= lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
    hull.RemoveAt(hull.Count - 1);
    var c = Vector2.zero; foreach (var p in hull) c += p; c /= hull.Count;
    var v = new List<Vector3>{ new Vector3(c.x, lidY, c.y) }; var t = new List<int>();
    foreach (var p in hull){ var q = c + (p - c) * 0.95f; v.Add(new Vector3(q.x, lidY, q.y)); }
    for (int i = 0; i < hull.Count; i++){ t.Add(0); t.Add(1 + i); t.Add(1 + (i + 1) % hull.Count); }
    var m = new Mesh{ name = "HullLid" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
    Debug.Log($"[Setup] hull lid: {hull.Count} points, {m.bounds.size.x:F2} x {m.bounds.size.z:F2} m at y={lidY:F2}"); return m;
  }
  static Mesh SkySphere(int lon, int lat){
    var m = new Mesh{ name = "SkySphere" }; var v = new List<Vector3>(); var t = new List<int>();
    for (int y = 0; y <= lat; y++) for (int x = 0; x <= lon; x++){ float a = x * Mathf.PI * 2f / lon, b = y * Mathf.PI / lat; v.Add(new Vector3(Mathf.Sin(b) * Mathf.Cos(a), Mathf.Cos(b), Mathf.Sin(b) * Mathf.Sin(a))); }
    for (int y = 0; y < lat; y++) for (int x = 0; x < lon; x++){ int i = y * (lon + 1) + x; t.AddRange(new[]{ i, i + 1, i + lon + 1, i + 1, i + lon + 2, i + lon + 1 }); }
    m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateBounds(); m.bounds = new Bounds(Vector3.zero, Vector3.one * 2f); return m;
  }

  static GameObject Part(string name, Transform parent, Mesh mesh, Material mat){
    var go = new GameObject(name); go.transform.SetParent(parent, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
    var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; return go;
  }

  /// <summary>Bloom (Art Dev: threshold ~0.9) in a global volume. Kept cheap for WebGL: quarter-res start, 5 iterations, no
  /// high-quality filtering.</summary>
  static VolumeProfile PostFX(){
    var path = Gen + "/PostFX.asset"; var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
    if (!prof){ prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, path); }
    if (!prof.TryGet<Bloom>(out var bloom)){ bloom = prof.Add<Bloom>(true); bloom.name = "Bloom"; AssetDatabase.AddObjectToAsset(bloom, prof); }
    bloom.active = true; bloom.threshold.Override(0.9f); bloom.intensity.Override(0.75f); bloom.scatter.Override(0.65f); bloom.clamp.Override(20f);
    bloom.highQualityFiltering.Override(false); bloom.downscale.Override(BloomDownscaleMode.Quarter); bloom.maxIterations.Override(5);
    EditorUtility.SetDirty(bloom); EditorUtility.SetDirty(prof); return prof;
  }
  /// <summary>Keep our shaders (and the panorama skybox) in the WebGL build whatever the stripper thinks.</summary>
  static void AlwaysInclude(params string[] names){
    var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset"); var so = new SerializedObject(gs); var arr = so.FindProperty("m_AlwaysIncludedShaders");
    foreach (var n in names){
      var sh = Shader.Find(n); if (!sh){ Debug.LogWarning("[Setup] no shader " + n); continue; }
      bool has = false; for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) has = true;
      if (!has){ arr.InsertArrayElementAtIndex(arr.arraySize); arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh; }
    }
    so.ApplyModifiedPropertiesWithoutUndo();
  }

  [MenuItem("Astral Tide/Rebuild Main Scene")]
  public static void SetupScene(){
    Directory.CreateDirectory(Gen); Directory.CreateDirectory("Assets/Scenes");
    EnsureURP();
    var leg = Leg.All[0];
    // ---- Art Dev's M1 set, by name (null = procedural fallback) ----
    var skiff = Find<GameObject>("Assets/Art", "AT_Skiff"); var skiffAlb = Find<Texture2D>("Assets/Art", "AT_Skiff_Albedo"); var skiffEm = Find<Texture2D>("Assets/Art", "AT_Skiff_Emission");
    var spireFbx = Find<GameObject>("Assets/Art", "AT_LanternSpire"); var spireAlb = Find<Texture2D>("Assets/Art", "AT_LanternSpire_Albedo"); var spireEm = Find<Texture2D>("Assets/Art", "AT_LanternSpire_Emission");
    var panos = Leg.All.Select(l => Find<Texture2D>("Assets/Art", l.panoKey)).ToArray(); var pano = panos.FirstOrDefault(x => x); var starTex = Find<Texture2D>("Assets/Art", "AT_Star"); var starfield = Find<Texture2D>("Assets/Art", "AT_Starfield");
    var wakeTex = Find<Texture2D>("Assets/Art", "AT_Wake");

    var skyMat = Mat("SkyMat", "AstralTide/Sky", 1000);
    var starsMat = Mat("StarVeilMat", "AstralTide/Stars", 2501);   // after URP draws the skybox (opaque pass end), before the sea
    Material panoMat = null;
    if (pano){
      panoMat = Mat("SkyPanoMat", "Skybox/Panoramic", 1000, m => { m.SetTexture("_MainTex", pano); m.SetFloat("_Mapping", 1); m.SetFloat("_ImageType", 0); m.SetFloat("_Layout", 0); m.SetFloat("_MirrorOnBack", 0); m.SetFloat("_Exposure", 1f); m.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f)); m.SetFloat("_Rotation", 0f); });
    }
    var seaMat = Mat("SeaMat", "AstralTide/Sea", 2502, m => { m.SetColor("_Tint", leg.water); });
    var maskMat = Mat("WaterMaskMat", "AstralTide/WaterMask", 2499);
    Material Lit(string name, Texture2D alb, Texture2D em, float emInt) => Mat(name, "Universal Render Pipeline/Lit", 2020, m => {
      m.SetTexture("_BaseMap", alb); m.SetTexture("_MainTex", alb); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.15f); m.SetFloat("_Metallic", 0f);
      m.SetFloat("_Cull", 0f); m.doubleSidedGI = true;   // the sail and flags are single planes
      if (em){ m.SetTexture("_EmissionMap", em); m.SetColor("_EmissionColor", Color.white * emInt); m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
    });
    Material Mirror(string name, Texture2D alb, Texture2D em, int queue, float emGain) => Mat(name, "AstralTide/Lit", queue, m => {
      m.SetTexture("_MainTex", alb ? alb : Texture2D.whiteTexture); m.SetTexture("_EmissionMap", em ? em : Texture2D.blackTexture); m.SetFloat("_EmissionGain", em ? emGain : 0f);
      m.SetFloat("_VColor", alb ? 0 : 1); m.SetColor("_Glow", leg.glow); m.SetFloat("_GlowBoost", 1f); m.SetFloat("_Mirror", 1); m.SetFloat("_ZTest", 4); m.SetFloat("_ZWrite", 1);   // real depth under the water; the sea blends over it
    });
    var shipMat = skiff ? Lit("SkiffLit", skiffAlb, skiffEm, 3f) : Mat("ShipMat", "AstralTide/Lit", 2020, m => { m.SetColor("_Glow", leg.glow); m.SetFloat("_GlowBoost", 1.3f); m.SetFloat("_Mirror", 0); m.SetFloat("_ZTest", 4); m.SetFloat("_ZWrite", 1); m.SetFloat("_VColor", 1); });
    var shipMirMat = Mirror(skiff ? "SkiffMirror" : "ShipMirrorMat", skiff ? skiffAlb : null, skiff ? skiffEm : null, 2006, 2f);
    var spireMat = spireFbx ? Lit("SpireLit", spireAlb, spireEm, 4f) : Mat("SpireMat", "AstralTide/Lit", 2020, m => { m.SetColor("_Glow", leg.glow); m.SetFloat("_GlowBoost", 1.2f); m.SetFloat("_Mirror", 0); m.SetFloat("_ZTest", 4); m.SetFloat("_ZWrite", 1); m.SetFloat("_VColor", 1); });
    var spireMirMat = Mirror(spireFbx ? "SpireMirror" : "SpireMirrorMat", spireFbx ? spireAlb : null, spireFbx ? spireEm : null, 2005, 2.5f);
    // FX: Art Dev's star sprite for sparkles and gust cores, the wake texture on a trail (fallback: the procedural glow)
    var glowMat = starTex ? Mat("StarSparkle", "AstralTide/FX", 3000, m => { m.SetTexture("_MainTex", starTex); m.SetColor("_Color", new Color(1.6f, 1.5f, 1.35f, 1f)); m.SetFloat("_Billboard", 0); m.SetFloat("_SwapUV", 0); })
      : Mat("GlowParticle", "AstralTide/Glow", 3000, m => { m.SetColor("_Color", Color.white); m.SetFloat("_Billboard", 0); m.SetFloat("_Core", 3); });
    var gustMat = starTex ? Mat("GustStar", "AstralTide/FX", 3000, m => { m.SetTexture("_MainTex", starTex); m.SetColor("_Color", new Color(2.2f, 2.0f, 1.7f, 0.9f)); m.SetFloat("_Billboard", 1); m.SetFloat("_SwapUV", 0); })
      : Mat("GustGlow", "AstralTide/Glow", 3000, m => { m.SetColor("_Color", new Color(1f, 0.93f, 0.78f, 0.55f)); m.SetFloat("_Billboard", 1); m.SetFloat("_Core", 4); });
    var haloMat = Mat("SpireHalo", "AstralTide/Glow", 3001, m => { m.SetColor("_Color", new Color(leg.glow.r, leg.glow.g, leg.glow.b, 0.7f)); m.SetFloat("_Billboard", 1); m.SetFloat("_Core", 5); });
    var lanternMat = Mat("LanternGlow", "AstralTide/Glow", 3002, m => { m.SetColor("_Color", new Color(1f, 0.85f, 0.55f, 0.8f)); m.SetFloat("_Billboard", 1); m.SetFloat("_Core", 6); });
    var fogMat = Mat("FogBank", "AstralTide/Glow", 2999, m => { m.SetColor("_Color", new Color(leg.fog.r, leg.fog.g, leg.fog.b, 0.07f)); m.SetFloat("_Billboard", 1); m.SetFloat("_Core", 0.5f); });
    var wakeMat = wakeTex ? Mat("WakeTrail", "AstralTide/FX", 3000, m => { m.SetTexture("_MainTex", wakeTex); m.SetColor("_Color", new Color(1.1f, 1.05f, 1.25f, 0.9f)); m.SetFloat("_SwapUV", 1); m.SetFloat("_Billboard", 0); }) : null;
    var hullMesh = SaveMesh("ShipHull", ShipHull()); var sailMesh = SaveMesh("ShipSail", ShipSail()); var spireMesh = SaveMesh("LanternSpire", Spire());
    var quad = SaveMesh("GlowQuad", Quad()); var seaMesh = SaveMesh("SeaRings", SeaRings(146, 160)); var skyMesh = SaveMesh("SkySphere", SkySphere(48, 32));
    var post = PostFX();
    AlwaysInclude("AstralTide/Sky", "AstralTide/Stars", "AstralTide/Sea", "AstralTide/Lit", "AstralTide/Glow", "AstralTide/FX", "AstralTide/WaterMask", "Skybox/Panoramic", "Universal Render Pipeline/Lit");
    AssetDatabase.SaveAssets();

    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    RenderSettings.fog = false; RenderSettings.skybox = panoMat;
    RenderSettings.ambientMode = AmbientMode.Trilight;   // soft twilight fill (the models have painted-in shading)
    RenderSettings.ambientSkyColor = new Color(0.62f, 0.55f, 0.82f); RenderSettings.ambientEquatorColor = new Color(0.86f, 0.66f, 0.72f); RenderSettings.ambientGroundColor = new Color(0.3f, 0.24f, 0.4f);

    var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
    var cam = camGo.AddComponent<Camera>(); cam.clearFlags = panoMat ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor; cam.backgroundColor = leg.mid0;
    cam.fieldOfView = 55; cam.nearClipPlane = 0.2f; cam.farClipPlane = 4200; cam.allowHDR = true;
    var cd = camGo.AddComponent<UniversalAdditionalCameraData>(); cd.renderPostProcessing = true; cd.renderShadows = false; cd.antialiasing = AntialiasingMode.None;
    camGo.AddComponent<AudioListener>();
    var vol = new GameObject("Global Volume").AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 1; vol.sharedProfile = post;

    var sunGo = new GameObject("Twilight Light"); var sun = sunGo.AddComponent<Light>(); sun.type = LightType.Directional; sun.color = new Color(1f, 0.78f, 0.84f); sun.intensity = 0.9f; sun.shadows = LightShadows.None;
    sunGo.transform.rotation = Quaternion.Euler(16f, 35f, 0f);

    // the sky: Art Dev's panorama as the skybox with the star veil (additive dome) over it; fallback: the procedural sky dome
    var sky = Part("Sky", null, skyMesh, skyMat); sky.transform.localScale = Vector3.one * 3800f;
    var veil = Part("StarVeil", null, skyMesh, starsMat); veil.transform.localScale = Vector3.one * 3800f;   // Voyage turns on one or the other per leg
    var sea = Part("Sea", null, seaMesh, seaMat);

    // the ship and its reflection (mirrored about the water plane)
    var shipGo = new GameObject("Ship"); var boat = shipGo.AddComponent<Boat>();
    var model = new GameObject("Model").transform; model.SetParent(shipGo.transform, false);
    var mirror = new GameObject("Mirror").transform; mirror.SetParent(shipGo.transform, false); mirror.localScale = new Vector3(1, -1, 1);
    const float SkiffScale = 1.35f;
    Vector3 lanternPos = new Vector3(0, 2.9f, 4.02f);
    Transform BuildShip(Transform root, Material mat){
      if (skiff){
        var hold = new GameObject("Skiff").transform; hold.SetParent(root, false); hold.localScale = Vector3.one * SkiffScale;
        var inst = (GameObject)Object.Instantiate(skiff, hold, false); inst.name = "AT_Skiff";
        foreach (var r in inst.GetComponentsInChildren<Renderer>()){ var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = mat; r.sharedMaterials = ms; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        var glass = inst.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "LanternGlass"); if (glass) lanternPos = root.InverseTransformPoint(glass.GetComponent<Renderer>() ? glass.GetComponent<Renderer>().bounds.center : glass.position);
        var sailT = inst.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Sail");
        if (sailT && sailT.GetComponent<Renderer>()){   // a pivot at the foot of the sail (the boom) so it lowers toward it
          var bb = sailT.GetComponent<Renderer>().bounds; var piv = new GameObject("SailPivot").transform; piv.SetParent(sailT.parent, false);
          piv.position = new Vector3(bb.center.x, root.lossyScale.y < 0 ? bb.max.y : bb.min.y, bb.center.z); piv.rotation = root.rotation; sailT.SetParent(piv, true); return piv;
        }
        Debug.LogWarning("[Setup] skiff has no Sail child: the sail won't shorten"); return null;
      }
      Part("Hull", root, hullMesh, mat);
      var yard = new GameObject("SailPivot").transform; yard.SetParent(root, false); yard.localPosition = new Vector3(0, 8.9f, 0.62f);
      Part("Sail", yard, sailMesh, mat); return yard;
    }
    boat.sailT = BuildShip(model, shipMat); boat.mirrorSail = BuildShip(mirror, shipMirMat); boat.model = model; boat.mirror = mirror;
    var lantern = Part("LanternGlow", model, quad, lanternMat); lantern.transform.localPosition = lanternPos; lantern.transform.localScale = Vector3.one * (skiff ? 1.4f : 3.2f);
    var bl = new GameObject("LanternLight").AddComponent<Light>(); bl.transform.SetParent(model, false); bl.transform.localPosition = lanternPos;
    bl.type = LightType.Point; bl.color = new Color(1f, 0.8f, 0.5f); bl.range = 7f; bl.intensity = 2.5f; bl.shadows = LightShadows.None;
    // Mike's M1 note: water showed through the hull. A depth-only lid at the waterline inside the hull stops the sea drawing there.
    var lidMesh = HullLid(model, 0.12f);
    if (lidMesh){ lidMesh = SaveMesh("HullLid", lidMesh); var lid = Part("WaterMask", model, lidMesh, maskMat); }
    // the Cosmic Sea's twin glowing cyan wake lines (switched on for that leg only)
    var cyanMat = Mat("CosmicWakeLine", "AstralTide/FX", 3000, m => { m.SetTexture("_MainTex", Texture2D.whiteTexture); m.SetColor("_Color", new Color(0.5f, 2.6f, 3.0f, 1f)); m.SetFloat("_SwapUV", 0); m.SetFloat("_Billboard", 0); });
    var cosmic = new TrailRenderer[2];
    for (int i = 0; i < 2; i++){
      var g = new GameObject(i == 0 ? "CosmicWakePort" : "CosmicWakeStarboard"); g.transform.SetParent(model, false); g.transform.localPosition = new Vector3((i == 0 ? -0.95f : 0.95f), 0.12f, -2.6f);
      var tr = g.AddComponent<TrailRenderer>(); tr.sharedMaterial = cyanMat; tr.time = 4.5f; tr.minVertexDistance = 0.5f; tr.alignment = LineAlignment.View; tr.widthMultiplier = 0.18f;
      tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.4f); var gr = new Gradient(); gr.SetKeys(new[]{ new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[]{ new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
      tr.colorGradient = gr; tr.shadowCastingMode = ShadowCastingMode.Off; tr.receiveShadows = false; tr.numCapVertices = 2; g.SetActive(false); cosmic[i] = tr;
    }
    boat.cosmicWake = cosmic;
    TrailRenderer wake = null;
    if (wakeMat){
      var wg = new GameObject("Wake"); wg.transform.SetParent(shipGo.transform, false); wg.transform.localPosition = new Vector3(0, 0.05f, -2.4f * SkiffScale); wg.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
      wake = wg.AddComponent<TrailRenderer>(); wake.sharedMaterial = wakeMat; wake.time = 5f; wake.minVertexDistance = 0.4f; wake.alignment = LineAlignment.TransformZ;
      wake.textureMode = LineTextureMode.Tile; wake.textureScale = new Vector2(0.12f, 1f); wake.widthMultiplier = 1.9f; wake.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 2.6f);
      var gr = new Gradient(); gr.SetKeys(new[]{ new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.8f, 1f), 1f) }, new[]{ new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.5f, 0.45f), new GradientAlphaKey(0f, 1f) });
      wake.colorGradient = gr; wake.shadowCastingMode = ShadowCastingMode.Off; wake.receiveShadows = false; wake.numCapVertices = 2;
    }
    boat.wakeTrail = wake;

    // the Lantern Spire (scaled up for its silhouette on the horizon), its light, halo and reflection
    const float SpireScale = 1.9f; float lanternY = spireFbx ? 26.3f * SpireScale : 52f;
    var spire = new GameObject("LanternSpire").transform; var spireMir = new GameObject("LanternSpireMirror").transform; spireMir.localScale = new Vector3(1, -1, 1);
    if (spireFbx){
      foreach (var (root, mat) in new[]{ (spire, spireMat), (spireMir, spireMirMat) }){
        var hold = new GameObject("Spire").transform; hold.SetParent(root, false); hold.localScale = Vector3.one * SpireScale;
        var inst = (GameObject)Object.Instantiate(spireFbx, hold, false); inst.name = "AT_LanternSpire";
        foreach (var r in inst.GetComponentsInChildren<Renderer>()){ var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = mat; r.sharedMaterials = ms; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
      }
    } else { Part("Tower", spire, spireMesh, spireMat); Part("TowerMirror", spireMir, spireMesh, spireMirMat); }
    var halo = Part("Halo", spire, quad, haloMat).transform; halo.localPosition = new Vector3(0, lanternY, 0); halo.localScale = Vector3.one * 30f;
    var sl = new GameObject("LanternLight").AddComponent<Light>(); sl.transform.SetParent(spire, false); sl.transform.localPosition = new Vector3(0, lanternY, 0);
    sl.type = LightType.Point; sl.color = new Color(1f, 0.82f, 0.52f); sl.range = 90f; sl.intensity = 4f; sl.shadows = LightShadows.None;

    // ---- M2: the other four landmarks (Art Dev's, by name; fallback: a glowing procedural tower) ----
    int NL = Leg.All.Length; var lmRoot = new Transform[NL]; var lmMir = new Transform[NL]; var lmHalo = new Transform[NL]; var lmLight = new Light[NL]; var lmTop = new float[NL]; var lmMat = new Material[NL]; var lmEm = new float[NL];
    var lmAnim = new Transform[NL]; var lmAnimMir = new Transform[NL]; Transform clapper = null, clapperMir = null;
    lmRoot[0] = spire; lmMir[0] = spireMir; lmHalo[0] = halo; lmLight[0] = sl; lmTop[0] = lanternY; lmMat[0] = spireMat; lmEm[0] = 4f;
    var lmDefs = new[]{ (key: "AT_AuroraGate", anchor: "Anchor_GateCenter", scale: 1.5f, em: 4f, halo: 46f, tint: Color.white, mat: "AT_AuroraGate"), (key: "AT_FallenStar", anchor: "Anchor_StarCore", scale: 2f, em: 4.5f, halo: 34f, tint: Color.white, mat: "AT_FallenStar"),
                        (key: "AT_GlacierBell", anchor: "Anchor_BellGlow", scale: 1.6f, em: 4f, halo: 30f, tint: Color.white, mat: "AT_GlacierBell"), (key: "AT_WhiteStar", anchor: "Anchor_StarCenter", scale: 1.6f, em: 9f, halo: 80f, tint: Color.white, mat: "AT_WhiteStar"),
                        // M3 Nebula Gate: the Aurora Gate model, bigger and recoloured magenta-violet (no new art)
                        (key: "AT_AuroraGate", anchor: "Anchor_GateCenter", scale: 1.9f, em: 5f, halo: 60f, tint: new Color(1f, 0.62f, 1f), mat: "NebulaGate") };
    Transform Pivot(Transform parent, Vector3 worldPos, string n){ var pv = new GameObject(n).transform; pv.SetParent(parent, false); pv.position = worldPos; pv.rotation = parent.rotation; return pv; }
    for (int k = 1; k < NL; k++){
      var d = lmDefs[k - 1]; var L = Leg.All[k];
      var fbx = Find<GameObject>("Assets/Art", d.key); var alb = Find<Texture2D>("Assets/Art", d.key + "_Albedo"); var em = Find<Texture2D>("Assets/Art", d.key + "_Emission");
      var root = new GameObject(L.landmarkName.Replace(" ", "")).transform; var mir = new GameObject(L.landmarkName.Replace(" ", "") + "Mirror").transform; mir.localScale = new Vector3(1, -1, 1);
      Vector3 anchor = new Vector3(0, 24f, 0);
      if (fbx){
        var lit = Lit(d.mat + "Lit", alb, em, d.em); var mirM = Mirror(d.mat + "Mirror", alb, em, 2005, d.em * 0.5f); lmMat[k] = lit; lmEm[k] = d.em;
        if (d.tint != Color.white){ lit.SetColor("_BaseColor", d.tint); lit.SetColor("_EmissionColor", d.tint * d.em); mirM.SetColor("_Tint", d.tint); }
        foreach (var (r0, mat) in new[]{ (root, lit), (mir, mirM) }){
          var hold = new GameObject("Model").transform; hold.SetParent(r0, false); hold.localScale = Vector3.one * d.scale;
          var inst = (GameObject)Object.Instantiate(fbx, hold, false); inst.name = d.key;
          foreach (var r in inst.GetComponentsInChildren<Renderer>()){ var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = mat; r.sharedMaterials = ms; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
          var an = inst.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == d.anchor);
          if (an && r0 == root) anchor = root.InverseTransformPoint(an.position); else if (!an) Debug.LogWarning($"[Setup] {d.key}: no {d.anchor}");
          bool isMir = r0 == mir;
          if (L.landmark == Landmark.GlacierBell){   // pivots at the top of the bell so it (and the clapper, lagging) can swing on arrival
            var parts = inst.GetComponentsInChildren<Transform>().Where(t => t.name == "Bell" || t.name == "BellInner").ToArray(); var clap = inst.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Clapper");
            var bellR = parts.Select(t => t.GetComponent<Renderer>()).FirstOrDefault(x => x);
            if (bellR){
              var bb = bellR.bounds; var top = new Vector3(bb.center.x, isMir ? bb.min.y : bb.max.y, bb.center.z);
              var pv = Pivot(r0, top, "BellPivot"); foreach (var t in parts) t.SetParent(pv, true);
              Transform cp = null; if (clap){ cp = Pivot(r0, top, "ClapperPivot"); clap.SetParent(cp, true); }
              if (isMir){ lmAnimMir[k] = pv; clapperMir = cp; } else { lmAnim[k] = pv; clapper = cp; }
            } else Debug.LogWarning("[Setup] GlacierBell: no Bell child (no swing)");
          }
          if (L.landmark == Landmark.WhiteStar && an){   // a pivot at the star's centre for the arrival flare
            var pv = Pivot(r0, an.position, "StarPivot"); hold.SetParent(pv, true);
            if (isMir) lmAnimMir[k] = pv; else lmAnim[k] = pv;
          }
        }
      } else {
        var fm = Mat($"Landmark{k}Fallback", "AstralTide/Lit", 2020, m => { m.SetColor("_Glow", L.glow); m.SetFloat("_GlowBoost", 1.2f); m.SetFloat("_Mirror", 0); m.SetFloat("_ZTest", 4); m.SetFloat("_ZWrite", 1); m.SetFloat("_VColor", 1); });
        var fmm = Mat($"Landmark{k}FallbackMirror", "AstralTide/Lit", 2005, m => { m.SetColor("_Glow", L.glow); m.SetFloat("_GlowBoost", 1f); m.SetFloat("_Mirror", 1); m.SetFloat("_ZTest", 4); m.SetFloat("_ZWrite", 1); m.SetFloat("_VColor", 1); });
        Part("Tower", root, spireMesh, fm); Part("TowerMirror", mir, spireMesh, fmm); lmMat[k] = fm; anchor = new Vector3(0, 52f, 0);
      }
      var h = Part("Halo", root, quad, haloMat).transform; h.localPosition = anchor; h.localScale = Vector3.one * d.halo;
      var li = new GameObject("Light").AddComponent<Light>(); li.transform.SetParent(root, false); li.transform.localPosition = anchor;
      li.type = LightType.Point; li.color = L.glow; li.range = 110f; li.intensity = 4f; li.shadows = LightShadows.None;
      lmRoot[k] = root; lmMir[k] = mir; lmHalo[k] = h; lmLight[k] = li; lmTop[k] = anchor.y;
      root.gameObject.SetActive(false); mir.gameObject.SetActive(false);
    }
    // ---- M2 props on the shared atlas (fallback: runtime procedural meshes) ----
    var propAlb = Find<Texture2D>("Assets/Art", "AT_Props_Albedo"); var propEm = Find<Texture2D>("Assets/Art", "AT_Props_Emission");
    var propMat = Lit("PropsLit", propAlb, propEm, 4f); var propMirMat = Mirror("PropsMirror", propAlb, propEm, 2006, 2f);
    Material VC(string n, Color glow, bool mirror) => Mat(n, "AstralTide/Lit", mirror ? 2006 : 2020, m => { m.SetColor("_Glow", glow); m.SetFloat("_GlowBoost", 1.6f); m.SetFloat("_Mirror", mirror ? 1 : 0); m.SetFloat("_ZTest", 4); m.SetFloat("_ZWrite", 1); m.SetFloat("_VColor", 1); });
    var nodeTex = Find<Texture2D>("Assets/Art", "AT_ConstellationNode"); var lineTex = Find<Texture2D>("Assets/Art", "AT_ConstellationLine"); var currentTex = Find<Texture2D>("Assets/Art", "AT_Current");
    var fragMat = starTex ? Mat("FragmentGlint", "AstralTide/FX", 3000, m => { m.SetTexture("_MainTex", starTex); m.SetColor("_Color", new Color(2.2f, 2.1f, 1.6f, 1f)); m.SetFloat("_Billboard", 1); m.SetFloat("_SwapUV", 0); })
      : Mat("FragmentGlow", "AstralTide/Glow", 3000, m => { m.SetColor("_Color", new Color(1f, 0.95f, 0.75f, 0.9f)); m.SetFloat("_Billboard", 1); m.SetFloat("_Core", 4); });
    var constMat = nodeTex ? Mat("ConstellationNode", "AstralTide/FX", 3003, m => { m.SetTexture("_MainTex", nodeTex); m.SetColor("_Color", new Color(2.4f, 2.4f, 2.6f, 1f)); m.SetFloat("_Billboard", 1); m.SetFloat("_SwapUV", 0); })
      : Mat("ConstellationGlow", "AstralTide/Glow", 3003, m => { m.SetColor("_Color", new Color(0.9f, 0.95f, 1f, 1f)); m.SetFloat("_Billboard", 1); m.SetFloat("_Core", 5); });
    var lineMat = Mat("ConstellationLine", "AstralTide/FX", 3002, m => { m.SetTexture("_MainTex", lineTex ? lineTex : Texture2D.whiteTexture); m.SetColor("_Color", lineTex ? new Color(1.4f, 1.5f, 1.8f, 0.8f) : new Color(0.6f, 0.7f, 1f, 0.25f)); m.SetFloat("_Billboard", 0); m.SetFloat("_SwapUV", 0); });
    var buoyMat = starTex ? Mat("BuoyGlint", "AstralTide/FX", 3000, m => { m.SetTexture("_MainTex", starTex); m.SetColor("_Color", new Color(0.9f, 2.6f, 2.8f, 1f)); m.SetFloat("_Billboard", 1); m.SetFloat("_SwapUV", 0); })
      : Mat("BuoyGlow", "AstralTide/Glow", 3000, m => { m.SetColor("_Color", new Color(0.5f, 1f, 1f, 0.9f)); m.SetFloat("_Billboard", 1); m.SetFloat("_Core", 4); });

    // M3: warm, glowing fragments for Frost Mirror's white ice (Mike's note), and the whale
    var propWarmMat = Lit("PropsLitWarm", propAlb, propEm, 6f); propWarmMat.SetColor("_BaseColor", new Color(1f, 0.78f, 0.45f)); propWarmMat.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.25f) * 6f);
    var fragWarmMat = starTex ? Mat("FragmentGlintWarm", "AstralTide/FX", 3000, m => { m.SetTexture("_MainTex", starTex); m.SetColor("_Color", new Color(3.2f, 1.9f, 0.7f, 1f)); m.SetFloat("_Billboard", 1); m.SetFloat("_SwapUV", 0); })
      : Mat("FragmentGlowWarm", "AstralTide/Glow", 3000, m => { m.SetColor("_Color", new Color(1f, 0.6f, 0.25f, 1f)); m.SetFloat("_Billboard", 1); m.SetFloat("_Core", 4); });
    var fragHaloMat = Mat("FragmentHaloWarm", "AstralTide/Glow", 2999, m => { m.SetColor("_Color", new Color(1f, 0.55f, 0.2f, 0.75f)); m.SetFloat("_Billboard", 1); m.SetFloat("_Core", 1.6f); });
    // Frost Mirror whales: Art Dev's AT_FrostWhale only (scenery, no colliders). If it hasn't landed, there are no whales.
    var whaleArt = Find<GameObject>("Assets/Art", "AT_FrostWhale"); Material whaleMat = null, whaleMirMat = null;
    if (whaleArt){ var wa = Find<Texture2D>("Assets/Art", "AT_FrostWhale_Albedo"); var we = Find<Texture2D>("Assets/Art", "AT_FrostWhale_Emission"); whaleMat = Lit("WhaleLit", wa, we, 3f); whaleMirMat = Mirror("WhaleMirror", wa, we, 2006, 1.5f); }
    // ice floes: Art Dev's AT_IceFloe_Mat by name on our faceted floe mesh, else the procedural icy vertex-colour material
    var floeMat = Find<Material>("Assets/Art", "AT_IceFloe_Mat");

    var v = new GameObject("Voyage").AddComponent<Voyage>();
    v.propWarmMat = propWarmMat; v.fragWarmMat = fragWarmMat; v.fragHaloMat = fragHaloMat; v.whaleArt = whaleArt; v.whaleMat = whaleMat; v.whaleMirMat = whaleMirMat; v.floeArtMat = floeMat;
    v.landmarks = lmRoot; v.landmarkMirrors = lmMir; v.halos = lmHalo; v.landmarkLights = lmLight; v.landmarkTop = lmTop; v.landmarkMats = lmMat; v.landmarkEm = lmEm;
    v.landmarkAnim = lmAnim; v.landmarkAnimMir = lmAnimMir; v.clapper = clapper; v.clapperMir = clapperMir;
    v.panos = panos; v.currentTex = currentTex; v.fragIcon = nodeTex; v.starVeil = veil.transform;
    v.meteorArt = Find<GameObject>("Assets/Art", "AT_MeteorRock"); v.floeArt = null; /* M3: the FBX floe scaled up read as whales; floes are our flat faceted mesh now */ v.fragArt = Find<GameObject>("Assets/Art", "AT_StarFragment"); v.buoyArt = Find<GameObject>("Assets/Art", "AT_StarBuoy");
    v.propMat = propMat; v.propMirMat = propMirMat; v.fragMat = fragMat; v.constMat = constMat; v.lineMat = lineMat; v.buoyMat = buoyMat;
    v.rockMat = VC("MeteorProc", new Color(1f, 0.5f, 0.2f), false); v.rockMirMat = VC("MeteorProcMirror", new Color(1f, 0.5f, 0.2f), true);
    v.iceMat = VC("IceProc", new Color(0.8f, 0.95f, 1f), false); v.iceMirMat = VC("IceProcMirror", new Color(0.8f, 0.95f, 1f), true);
    v.cam = cam; v.sea = sea.transform; v.sky = sky.transform; v.boat = boat; v.boatLight = bl;
    v.quadMesh = quad; v.glowMat = glowMat; v.gustMat = gustMat; v.haloMat = haloMat; v.fogMat = fogMat; v.seaMat = seaMat;
    v.skyboxMat = panoMat; v.skyPano = pano; v.starfield = starfield; v.realModels = skiff && spireFbx;
    v.compassStrip = Find<Texture2D>("Assets/Art", "AT_StarCompass_Strip"); v.compassMarker = Find<Texture2D>("Assets/Art", "AT_StarCompass_GoalMarker");

    var au = new GameObject("VoyageAudio").AddComponent<VoyageAudio>();
    au.music = Find<AudioClip>("Assets/Audio", "dusk_shallows_loop"); au.arrival = Find<AudioClip>("Assets/Audio", "landmark_arrival_swell");
    au.lapping = Find<AudioClip>("Assets/Audio", "water_lapping_loop"); au.wakeHiss = Find<AudioClip>("Assets/Audio", "wake_hiss_loop");
    au.sailFill = Find<AudioClip>("Assets/Audio", "sail_filling"); au.gust = Find<AudioClip>("Assets/Audio", "star_gust_whoosh"); au.chime = Find<AudioClip>("Assets/Audio", "on_course_chime");
    au.legMusic = Leg.All.Select(l => Find<AudioClip>("Assets/Audio", l.musicKey)).ToArray();
    au.currentRush = Find<AudioClip>("Assets/Audio", "current_rush_loop"); au.meteorBump = Find<AudioClip>("Assets/Audio", "meteor_bump"); au.iceCreak = Find<AudioClip>("Assets/Audio", "ice_creak");
    au.fragChime = Find<AudioClip>("Assets/Audio", "star_fragment_chime"); au.constellation = Find<AudioClip>("Assets/Audio", "constellation_complete"); au.boost = Find<AudioClip>("Assets/Audio", "boost_whoosh"); au.glacierBell = Find<AudioClip>("Assets/Audio", "glacier_bell");

    EditorSceneManager.SaveScene(scene, ScenePath);
    EditorBuildSettings.scenes = new[]{ new EditorBuildSettingsScene(ScenePath, true) };
    Debug.Log("[Setup] scene saved");
  }

  /// <summary>Page hardening (same as Neon Hive / Salt and Glory): canvas touch-action:none + preventDefault on touch events, no
  /// context menu, Space / arrows / Tab don't scroll; title and favicon (Web/favicon.png) patched; the QA hook only when asked.</summary>
  static void PatchIndexHtml(string outDir, bool qaHook){
    var p = Path.Combine(outDir, "index.html"); if (!File.Exists(p)) return;
    var html = File.ReadAllText(p); if (html.Contains("at-input-guard")) return;
    html = System.Text.RegularExpressions.Regex.Replace(html, "<title>.*?</title>", "<title>Astral Tide</title>");
    if (File.Exists("Web/favicon.png")){
      File.Copy("Web/favicon.png", Path.Combine(outDir, "favicon.png"), true);
      html = html.Replace("<link rel=\"shortcut icon\" href=\"TemplateData/favicon.ico\">", "<link rel=\"icon\" type=\"image/png\" href=\"favicon.png\">");
    } else Debug.LogWarning("[Build] Web/favicon.png missing, keeping the template favicon");
    const string css = "<style id=\"at-input-guard-css\">html,body{overscroll-behavior:none;background:#1B1638}#unity-container.unity-desktop{position:fixed;left:0;top:0;right:0;bottom:0;transform:none;display:flex;flex-direction:column;align-items:center;justify-content:center}#unity-canvas{touch-action:none;-ms-touch-action:none;user-select:none;-webkit-user-select:none;-webkit-touch-callout:none;-webkit-tap-highlight-color:transparent;outline:none}</style>";
    const string js = @"<script id=""at-input-guard"">(function(){
  var c=document.getElementById('unity-canvas'); if(!c) return;
  var stop=function(e){ if(e.cancelable) e.preventDefault(); };
  ['touchstart','touchmove','touchend','touchcancel'].forEach(function(t){ c.addEventListener(t,stop,{passive:false}); });
  ['gesturestart','gesturechange','gestureend','dblclick','contextmenu'].forEach(function(t){ c.addEventListener(t,stop,{passive:false}); });
  document.addEventListener('contextmenu',function(e){ if(e.target===c) e.preventDefault(); });
  window.addEventListener('keydown',function(e){
    var k=e.key||'', tag=(e.target&&e.target.tagName)||'';
    if((k===' '||k==='Spacebar'||k==='Tab'||k.indexOf('Arrow')===0) && !/INPUT|TEXTAREA|SELECT/.test(tag)) e.preventDefault();
  },{passive:false});
  if(!c.hasAttribute('tabindex')) c.setAttribute('tabindex','-1');
  c.addEventListener('pointerdown',function(){ try{ c.focus(); }catch(_){} });
})();</script>";
    // M3 (Mike): landscape only. On an upright phone or tablet a simple "rotate your device" prompt covers the game.
    const string rot = "<style id=\"at-rotate-css\">#at-rotate{display:none;position:fixed;left:0;top:0;right:0;bottom:0;z-index:2147483647;background:#1B1638;color:#F4EEFF;font:600 22px/1.45 system-ui,-apple-system,Segoe UI,Roboto,sans-serif;flex-direction:column;align-items:center;justify-content:center;text-align:center;padding:28px;box-sizing:border-box}#at-rotate .ph{width:54px;height:92px;border:4px solid #FFD58A;border-radius:12px;margin-bottom:26px;animation:atrot 2.4s ease-in-out infinite}#at-rotate small{display:block;margin-top:10px;font-weight:400;font-size:16px;opacity:.75}@keyframes atrot{0%,25%{transform:rotate(0)}55%,100%{transform:rotate(-90deg)}}@media (orientation:portrait) and (max-width:1024px){#at-rotate{display:flex}}</style>";
    const string rotDiv = "<div id=\"at-rotate\" role=\"alert\"><div class=\"ph\"></div>Rotate your device<small>Astral Tide sails in landscape</small></div>";
    html = html.Replace("</head>", rot + "\n  </head>").Replace("<body>", "<body>\n    " + rotDiv);
    if (qaHook) html = html.Replace(".then((unityInstance) => {", ".then((unityInstance) => { window.atUnity = unityInstance;");   // dev / AT_QA builds only
    html = html.Replace("</head>", css + "\n  </head>").Replace("</body>", js + "\n  </body>");
    File.WriteAllText(p, html);
  }

  [MenuItem("Astral Tide/Build WebGL to docs")]
  public static void Build(){
    int code = 1;
    try {
      PlayerSettings.colorSpace = ColorSpace.Linear;
      SetupScene();
      PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
      PlayerSettings.WebGL.decompressionFallback = true;
      PlayerSettings.WebGL.threadsSupport = false;
      PlayerSettings.colorSpace = ColorSpace.Linear;   // Art Dev's materials assume linear
      PlayerSettings.productName = "Astral Tide"; PlayerSettings.companyName = "mikenev";
      PlayerSettings.defaultWebScreenWidth = 1280; PlayerSettings.defaultWebScreenHeight = 720;
      bool dev = System.Environment.GetEnvironmentVariable("AT_DEV") == "1", qa = !dev && System.Environment.GetEnvironmentVariable("AT_QA") == "1";
      if (dev){ PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace; PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.Full); }
      else { PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly; PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly); }
      var outDir = Path.GetFullPath(dev ? "Builds/WebGLDev" : qa ? "Builds/WebGLQA" : "docs");
      var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions{ scenes = new[]{ ScenePath }, locationPathName = outDir, target = BuildTarget.WebGL,
        options = dev ? BuildOptions.Development : BuildOptions.None, extraScriptingDefines = qa ? new[]{ "AT_QA" } : null });
      Debug.Log("BUILD RESULT: " + r.summary.result + " size=" + r.summary.totalSize);
      if (r.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded){ File.WriteAllText(Path.Combine(outDir, ".nojekyll"), ""); PatchIndexHtml(outDir, dev || qa); code = 0; }
    } catch (System.Exception e){ Debug.LogException(e); }
    if (Application.isBatchMode) EditorApplication.Exit(code);
  }
}
