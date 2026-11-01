// Astral Tide: the one sky function, shared by the sky and the sea (the sea evaluates it along the reflected ray, so the water
// mirrors the sky you see). With Art Dev's painted panorama (AT_DuskShallows_SkyPano, drawn as the Skybox/Panoramic skybox) the
// base is that texture with the same lat-long mapping; without it, a procedural lavender gradient. On top: the star veil (the
// AT_Starfield layer plus procedural twinkles, coming out as the leg goes on) and the Lantern Spire's glow. Globals from Voyage.
#ifndef ASTRAL_SKY_INCLUDED
#define ASTRAL_SKY_INCLUDED
float4 _AT_SkyTop, _AT_SkyMid, _AT_SkyLow, _AT_Horizon, _AT_GlowCol, _AT_GoalDir;   // _AT_GoalDir.xyz = unit dir to the lantern, w = glow strength
float _AT_StarAmount, _AT_PanoMix, _AT_PanoGain, _AT_StarfieldMix, _AT_Flash;
float _AT_Aurora, _AT_Shoot, _AT_Nebula; float4 _AT_AuroraCol, _AT_AuroraCol2, _AT_NebulaCol, _AT_NebulaCol2;   // M2 per-leg sky features
TEXTURE2D(_AT_Pano); SAMPLER(sampler_AT_Pano); TEXTURE2D(_AT_Starfield); SAMPLER(sampler_AT_Starfield);

float at_hash3(float3 p){ p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
float at_noise(float2 p){
  float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
  float a = frac(sin(dot(i, float2(127.1, 311.7))) * 43758.55), b = frac(sin(dot(i + float2(1, 0), float2(127.1, 311.7))) * 43758.55);
  float c = frac(sin(dot(i + float2(0, 1), float2(127.1, 311.7))) * 43758.55), d = frac(sin(dot(i + float2(1, 1), float2(127.1, 311.7))) * 43758.55);
  return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}
float at_fbm(float2 p){ float s = 0, a = 0.5; for (int i = 0; i < 4; i++){ s += a * at_noise(p); p = p * 2.03 + 7.1; a *= 0.5; } return s; }

float at_stars(float3 d, float scale, float density, float size, float t){
  float3 p = d * scale; float3 c = floor(p); float b = 0;
  for (int z = 0; z <= 1; z++) for (int y = 0; y <= 1; y++) for (int x = 0; x <= 1; x++){
    float3 cc = c + float3(x, y, z); float h = at_hash3(cc);
    if (h > density) continue;
    float3 sp = cc + float3(at_hash3(cc + 11.3), at_hash3(cc + 23.7), at_hash3(cc + 41.9));
    float tw = 0.65 + 0.35 * sin(t * (1.3 + h * 7.0) + h * 40.0);
    b += smoothstep(size, 0, length(p - sp)) * tw * (0.5 + h / density * 0.5);
  }
  return b;
}
// Unity's Skybox/Panoramic lat-long mapping, so the reflection lines up with the skybox
float2 at_latlong(float3 d){ return float2(0.5, 1.0) - float2(atan2(d.z, d.x) / 6.2831853, acos(clamp(d.y, -1, 1)) / 3.14159265); }

half3 AstralSkyBase(float3 d, float t){
  float ae = abs(d.y);
  if (_AT_PanoMix > 0.001){
    float3 du = normalize(float3(d.x, ae, d.z));   // the upper half (the pano's lower half is only a dim fallback copy)
    return SAMPLE_TEXTURE2D_LOD(_AT_Pano, sampler_AT_Pano, at_latlong(du), 0).rgb * _AT_PanoGain;
  }
  float az = atan2(d.x, d.z);
  float brush = at_fbm(float2(az * 3.0, ae * 9.0) + float2(t * 0.004, 0)) - 0.5;
  float h = saturate(ae * 1.15 + brush * 0.06);
  half3 c = lerp(_AT_Horizon.rgb, _AT_SkyLow.rgb, smoothstep(0.0, 0.12, h));
  c = lerp(c, _AT_SkyMid.rgb, smoothstep(0.08, 0.38, h));
  c = lerp(c, _AT_SkyTop.rgb, smoothstep(0.3, 0.95, h));
  float streak = at_fbm(float2(az * 2.2 + t * 0.002, ae * 26.0));
  float band = smoothstep(0.02, 0.09, ae) * smoothstep(0.32, 0.12, ae);
  c = lerp(c, c * 1.18 + _AT_GlowCol.rgb * 0.08, saturate((streak - 0.52) * 3.0) * band * 0.7);
  // Aurora Reach: two curtains of light with vertical rays, drifting slowly
  if (_AT_Aurora > 0.001){
    for (int k = 0; k < 2; k++){
      float yb = 0.1 + 0.08 * k + 0.09 * at_noise(float2(az * 1.3 + k * 5.0 + t * 0.02, k));
      float x = ae - yb;
      float curtain = step(0.0, x) * exp(-x * (5.0 + k * 2.0)) * smoothstep(-0.02, 0.02, x);
      float rays = 0.45 + 0.55 * sin(az * (22.0 + k * 9.0) + at_fbm(float2(az * 3.0, t * 0.05 + k)) * 6.0 + t * (0.25 + k * 0.1));
      float sway = 0.5 + 0.5 * sin(az * 2.0 + t * 0.07 + k * 2.0);
      c += lerp(_AT_AuroraCol.rgb, _AT_AuroraCol2.rgb, saturate(x * 3.0)) * curtain * rays * sway * _AT_Aurora;
    }
  }
  // The Cosmic Sea: a purple nebula with brighter folds
  if (_AT_Nebula > 0.001){
    float n = at_fbm(float2(az * 1.4 + t * 0.002, ae * 3.2)), n2 = at_fbm(float2(az * 2.3 - 3.0, ae * 5.0 + 2.0));
    float m = smoothstep(0.38, 0.85, n) * smoothstep(0.0, 0.25, ae);
    c += lerp(_AT_NebulaCol.rgb, _AT_NebulaCol2.rgb, n2) * m * _AT_Nebula + _AT_NebulaCol2.rgb * pow(m, 4.0) * 0.6 * _AT_Nebula;
  }
  return c;
}
// Ember Drift: shooting stars. Each slot picks a start point and direction from a hash of its time window and streaks for a moment.
half3 AstralShooting(float3 d, float t){
  if (_AT_Shoot <= 0.001) return 0;
  float az = atan2(d.x, d.z), el = asin(saturate(abs(d.y))); half3 c = 0;
  for (int i = 0; i < 4; i++){
    float span = 2.6 + i * 0.7; float slot = floor(t / span + i * 0.31); float ph = frac(t / span + i * 0.31);
    float h1 = at_hash3(float3(slot, i, 1.7)), h2 = at_hash3(float3(slot, i, 5.3)), h3 = at_hash3(float3(slot, i, 9.1));
    if (h3 > _AT_Shoot) continue;
    float life = 0.35; if (ph > life) continue; float k = ph / life;
    float2 p0 = float2(h1 * 6.2831853 - 3.14159265, 0.35 + 0.6 * h2); float ang = -0.5 - h3 * 0.8; float2 dir = float2(cos(ang) * (h1 > 0.5 ? 1 : -1), sin(ang));
    float2 head = p0 + dir * k * 0.5, tail = head - dir * 0.16;
    float2 q = float2(az, el); float2 dq = q - tail; dq.x = (abs(dq.x) > 3.14159265) ? dq.x - sign(dq.x) * 6.2831853 : dq.x;
    float2 hd = head - tail; float u = saturate(dot(dq, hd) / dot(hd, hd)); float2 e = dq - hd * u; e.x *= cos(el);
    float line_ = exp(-dot(e, e) / 0.0000025) * u * (1.0 - k * 0.6);
    c += half3(1.0, 0.86, 0.7) * line_ * 2.0;
  }
  return c;
}
half3 AstralStarsAndGlow(float3 d, float t){
  d = normalize(d); float ae = abs(d.y); half3 c = 0;
  // stars: the first ones high up, more toward the horizon as _AT_StarAmount rises
  float vis = smoothstep(0.55 - _AT_StarAmount * 0.6, 0.85 - _AT_StarAmount * 0.6, ae) * saturate(_AT_StarAmount * 1.6);
  float s = at_stars(d, 70.0, 0.22, 0.22, t) * 1.2 + at_stars(d, 160.0, 0.30, 0.18, t * 1.3) * 0.55 * saturate(_AT_StarAmount * 1.4 - 0.3)
            + at_stars(d, 250.0, 0.34, 0.16, t * 0.9) * 0.6 * saturate(_AT_StarAmount - 1.2);   // M3 nebula leg: a third, denser layer (2x stars)
  c += half3(1.0, 0.95, 0.88) * s * vis * (_AT_StarfieldMix > 0.001 ? 0.6 : 1.0);
  if (_AT_StarfieldMix > 0.001){
    float3 du = normalize(float3(d.x, ae, d.z)); float2 uv = at_latlong(du) * float2(6, 3) + float2(t * 0.0008, 0);
    half4 sf = SAMPLE_TEXTURE2D_LOD(_AT_Starfield, sampler_AT_Starfield, uv, 0);
    float tw = 0.75 + 0.25 * sin(t * 1.7 + uv.x * 37.0 + uv.y * 23.0);
    c += sf.rgb * sf.a * vis * tw * _AT_StarfieldMix;
  }
  // the spire's lantern glow on the horizon (and its mirror)
  float3 g = normalize(float3(_AT_GoalDir.x, abs(_AT_GoalDir.y), _AT_GoalDir.z));
  float k = saturate(dot(float3(d.x, ae, d.z), g));
  c += _AT_GlowCol.rgb * (pow(k, 900.0) * 1.2 + pow(k, 60.0) * 0.25 + pow(k, 8.0) * 0.06) * _AT_GoalDir.w;
  c += _AT_GlowCol.rgb * _AT_Flash * 0.35;
  return c;
}
half3 AstralSky(float3 d, float t){ d = normalize(d); return AstralSkyBase(d, t) + AstralStarsAndGlow(d, t) + AstralShooting(d, t); }
#endif
