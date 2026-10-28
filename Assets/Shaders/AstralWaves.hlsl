// Astral Tide: the swell. Four directional sine waves (deep-water dispersion), the same sum as AstralTide.Waves on the CPU, which
// the ship samples for heave, pitch and roll. Voyage sets the globals each frame (and the clock, so both sides agree exactly).
#ifndef ASTRAL_WAVES_INCLUDED
#define ASTRAL_WAVES_INCLUDED
float4 _AT_Wave;        // x = amplitude (m), y = wavelength (m), z = speed multiplier, w = direction (radians, 0 = +Z)
float _AT_WaveTime;
float AT_WaveH(float2 p, out float2 grad){
  const float4 C[4] = { float4(1.0, 1.0, 0.0, 0.0), float4(0.55, 0.62, 0.61, 1.7), float4(0.35, 0.41, -0.87, 4.1), float4(0.18, 0.27, 1.4, 2.3) };
  float h = 0; grad = 0;
  if (_AT_Wave.x <= 0.0001) return 0;
  for (int i = 0; i < 4; i++){
    float a = _AT_Wave.w + C[i].z; float2 d = float2(sin(a), cos(a));
    float k = 6.2831853 / (_AT_Wave.y * C[i].y); float w = sqrt(9.81 * k) * _AT_Wave.z;
    float ph = k * dot(d, p) + w * _AT_WaveTime + C[i].w; float A = _AT_Wave.x * C[i].x;
    h += A * sin(ph); grad += A * k * cos(ph) * d;
  }
  return h;
}
#endif
