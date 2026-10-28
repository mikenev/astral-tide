using UnityEngine;
namespace AstralTide {
  /// <summary>The little ship (sailing model from Salt and Glory's Ship, without the wind and the guns: this sea is calm and you
  /// can't fail). Sail 0..1 sets the speed, the rudder turns harder as you gather way, a gust adds a short burst. M2: the ship rides
  /// the swell (Waves, the same function the sea shader draws), sampling bow, stern and both sides for heave, pitch and roll; a
  /// current carries it along (drift); a bump (meteor, ice floe) slows it and rocks it. Its mirrored copy reflects about the
  /// local water surface.</summary>
  public class Boat : MonoBehaviour {
    public float sail, rudder, heading, speed, boost;
    public float maxSpeed = 11f, turnRate = 34f;
    public Transform model, mirror, sailT, mirrorSail; public TrailRenderer wakeTrail; public TrailRenderer[] cosmicWake;
    public Vector3 drift;             // world velocity from currents (m/s)
    public float freeboard = 0.06f;   // sits the hull a touch higher on the water
    public float halfLen = 2.6f, halfBeam = 1.0f;
    float heel, bobSeed = 1.7f, rock, rockV; Vector3 trailBase; bool trailBaseSet;
    public float WaveH { get; private set; }
    public float Pitch { get; private set; }
    public float Roll { get; private set; }
    public float Rock => rock;
    public Vector3 Pos => transform.position;
    public Vector3 Fwd => transform.forward;

    public void Reset(Vector3 pos, float hdg, float sailLevel){
      sail = sailLevel; rudder = 0f; speed = 0f; boost = 0f; heading = hdg; drift = Vector3.zero; rock = rockV = 0f;
      transform.SetPositionAndRotation(new Vector3(pos.x, 0f, pos.z), Quaternion.Euler(0f, hdg, 0f));
      if (wakeTrail) wakeTrail.Clear();
      if (cosmicWake != null) foreach (var t in cosmicWake) if (t) t.Clear();
    }
    /// <summary>A knock: lose most of the way, get shoved away from the obstacle, and rock.</summary>
    public void Bump(Vector3 away, float strength){
      speed *= 0.35f; boost = 0f; away.y = 0f;
      transform.position += away.normalized * 0.6f; drift += away.normalized * 3.5f * strength;
      rockV += 38f * strength * (Vector3.Dot(away, transform.right) > 0f ? -1f : 1f);
    }
    public void Tick(float dt, float stopAt = -1f){
      float target = maxSpeed * Mathf.SmoothStep(0f, 1f, sail) + boost;
      if (stopAt >= 0f) target = Mathf.Min(target, stopAt);
      speed = Mathf.MoveTowards(speed, target, (target > speed ? 1.6f + boost * 0.4f : 1.4f) * dt);
      boost = Mathf.MoveTowards(boost, 0f, 2.2f * dt);
      heading = Mathf.Repeat(heading + rudder * turnRate * (0.35f + 0.65f * Mathf.Clamp01(speed / 5f)) * dt, 360f);
      transform.rotation = Quaternion.Euler(0f, heading, 0f);
      transform.position += (Fwd * speed + drift) * dt;
      drift = Vector3.MoveTowards(drift, Vector3.zero, 2.5f * dt);   // currents keep topping it up while you're in one
      heel = Mathf.Lerp(heel, -rudder * 6f * Mathf.Clamp01(speed / 6f), dt * 2f);
      // the rock after a bump: a damped spring
      rockV += (-rock * 30f - rockV * 3.2f) * dt; rock += rockV * dt;
      // ride the swell: heave from the centre and the four points around the hull, pitch from bow vs stern, roll from the sides
      var p = transform.position; var f = Fwd; var r = transform.right;
      float hc = Waves.H(p.x, p.z), hb = Waves.H(p.x + f.x * halfLen, p.z + f.z * halfLen), hs = Waves.H(p.x - f.x * halfLen, p.z - f.z * halfLen);
      float hr = Waves.H(p.x + r.x * halfBeam, p.z + r.z * halfBeam), hl = Waves.H(p.x - r.x * halfBeam, p.z - r.z * halfBeam);
      float k = 1f - Mathf.Exp(-dt * 6f);
      WaveH = Mathf.Lerp(WaveH, (hc * 2f + hb + hs + hr + hl) / 6f, k);
      Pitch = Mathf.Lerp(Pitch, -Mathf.Atan2(hb - hs, 2f * halfLen) * Mathf.Rad2Deg, k);   // bow up = negative X
      Roll = Mathf.Lerp(Roll, Mathf.Atan2(hr - hl, 2f * halfBeam) * Mathf.Rad2Deg, k);      // starboard up = positive Z
      float t = Time.time + bobSeed; float calm = Waves.amp < 0.2f ? 1f : 0.4f;   // the little idle bob stays on calm water
      var lp = new Vector3(0f, WaveH + freeboard + Mathf.Sin(t * 1.1f) * 0.06f * calm, 0f);
      var lr = Quaternion.Euler(Pitch + Mathf.Sin(t * 0.7f) * 1.2f * calm, 0f, Roll + heel + rock + Mathf.Sin(t * 0.9f) * 1.4f * calm);
      if (model){ model.localPosition = lp; model.localRotation = lr; }
      // reflect about the local water surface (y = WaveH): mirrored height 2h - y
      if (mirror){ mirror.localPosition = new Vector3(lp.x, 2f * WaveH - lp.y, lp.z); mirror.localRotation = new Quaternion(-lr.x, lr.y, -lr.z, lr.w); }
      float sk = sail < 0.02f ? 0.06f : Mathf.Lerp(0.3f, 1f, sail);   // the sail hangs from the yard: shorten it downward
      if (sailT) sailT.localScale = new Vector3(1f, sk, 1f);
      if (mirrorSail) mirrorSail.localScale = new Vector3(1f, sk, 1f);
      if (wakeTrail){
        if (!trailBaseSet){ trailBase = wakeTrail.transform.localPosition; trailBaseSet = true; }
        wakeTrail.transform.localPosition = trailBase + Vector3.up * WaveH; wakeTrail.emitting = speed > 0.4f;
      }
      if (cosmicWake != null) foreach (var tr in cosmicWake) if (tr && tr.gameObject.activeInHierarchy) tr.emitting = speed > 0.4f;
    }
  }
}
