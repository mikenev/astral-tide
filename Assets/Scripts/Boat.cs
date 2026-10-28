using UnityEngine;
namespace AstralTide {
  /// <summary>The little ship (sailing model from Salt and Glory's Ship, without the wind and the guns: this sea is calm and you
  /// can't fail). Sail 0..1 sets the speed, the rudder turns harder as you gather way, a gust adds a short burst. The model bobs and
  /// heels into turns, the sail shortens as it is lowered, and its mirrored copy under the water does the same.</summary>
  public class Boat : MonoBehaviour {
    public float sail, rudder, heading, speed, boost;
    public float maxSpeed = 11f, turnRate = 34f;
    public Transform model, mirror, sailT, mirrorSail; public TrailRenderer wakeTrail;
    float heel, bobSeed = 1.7f;
    public Vector3 Pos => transform.position;
    public Vector3 Fwd => transform.forward;

    public void Reset(Vector3 pos, float hdg, float sailLevel){
      sail = sailLevel; rudder = 0f; speed = 0f; boost = 0f; heading = hdg;
      transform.SetPositionAndRotation(new Vector3(pos.x, 0f, pos.z), Quaternion.Euler(0f, hdg, 0f));
      if (wakeTrail) wakeTrail.Clear();
    }
    public void Tick(float dt, float stopAt = -1f){
      float target = maxSpeed * Mathf.SmoothStep(0f, 1f, sail) + boost;
      if (stopAt >= 0f) target = Mathf.Min(target, stopAt);
      speed = Mathf.MoveTowards(speed, target, (target > speed ? 1.6f + boost * 0.4f : 1.4f) * dt);
      boost = Mathf.MoveTowards(boost, 0f, 2.2f * dt);
      heading = Mathf.Repeat(heading + rudder * turnRate * (0.35f + 0.65f * Mathf.Clamp01(speed / 5f)) * dt, 360f);
      transform.rotation = Quaternion.Euler(0f, heading, 0f);
      transform.position += Fwd * speed * dt;
      heel = Mathf.Lerp(heel, -rudder * 6f * Mathf.Clamp01(speed / 6f), dt * 2f);
      float t = Time.time + bobSeed;
      var lp = new Vector3(0f, Mathf.Sin(t * 1.1f) * 0.1f, 0f); var lr = Quaternion.Euler(Mathf.Sin(t * 0.7f) * 1.6f, 0f, heel + Mathf.Sin(t * 0.9f) * 1.8f);
      if (model){ model.localPosition = lp; model.localRotation = lr; }
      if (mirror){ mirror.localPosition = new Vector3(lp.x, -lp.y, lp.z); mirror.localRotation = new Quaternion(-lr.x, lr.y, -lr.z, lr.w); }   // reflect about y = 0
      float k = sail < 0.02f ? 0.06f : Mathf.Lerp(0.3f, 1f, sail);   // the sail hangs from the yard: shorten it downward
      if (sailT) sailT.localScale = new Vector3(1f, k, 1f);
      if (mirrorSail) mirrorSail.localScale = new Vector3(1f, k, 1f);
      if (wakeTrail) wakeTrail.emitting = speed > 0.4f;
    }
  }
}
