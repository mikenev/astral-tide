// Astral Tide: the mirror sea, one shader for every leg (recoloured by Voyage). A dense concentric grid that follows the camera,
// displaced by the swell (AstralWaves.hlsl, the same function the ship rides on the CPU); each pixel reflects the sky function
// along the view ray bounced off the wave + ripple normal, so stars, aurora and nebula shimmer in the water. Half transparent
// so the mirrored copies of the ship and the landmark show through. Glowing currents (Aurora Reach), the wake glow, and fog
// past the edges of the course.
Shader "AstralTide/Sea" {
  Properties {
    _Ripple ("Ripple strength", Float) = 0.06
    _Tint ("Water tint", Color) = (0.82, 0.8, 0.95, 1)
    _Deep ("Deep colour", Color) = (0.09, 0.07, 0.2, 1)
  }
  SubShader {
    Tags { "RenderType"="Transparent" "Queue"="Transparent-498" "RenderPipeline"="UniversalPipeline" }
    Pass {
      Tags { "LightMode"="UniversalForward" }
      Blend SrcAlpha OneMinusSrcAlpha ZWrite On
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "AstralSky.hlsl"
      #include "AstralWaves.hlsl"
      CBUFFER_START(UnityPerMaterial) float _Ripple; float4 _Tint, _Deep; CBUFFER_END
      float4 _AT_Course;   // xy = start (x,z), zw = unit direction of the course line (x,z)
      float4 _AT_FogCol; float4 _AT_FogBand;   // x = fog start (lateral m), y = full fog, z = behind-start limit
      float4 _AT_Wake[8];  // xyz = recent stern points, w = weight
      float4 _AT_Current[12]; float4 _AT_CurrentInfo; float4 _AT_CurrentCol;   // segments (x0,z0,x1,z1); info x = count, y = half width
      float4 _AT_SeaTint; float _AT_Gloss; float _AT_CurrentTexOn;
      TEXTURE2D(_AT_CurrentTex); SAMPLER(sampler_AT_CurrentTex);   // Art Dev's AT_Current: U across (clamp), V along (repeat, scrolled)
      struct A { float4 pos : POSITION; };
      struct V { float4 pos : SV_POSITION; float3 w : TEXCOORD0; };
      float fadeOf(float3 w){ return smoothstep(900.0, 380.0, length(w.xz - _WorldSpaceCameraPos.xz)); }
      V vert (A i){
        V o; float3 w = TransformObjectToWorld(i.pos.xyz); float2 g;
        w.y = AT_WaveH(w.xz, g) * fadeOf(w);
        o.pos = TransformWorldToHClip(w); o.w = w; return o;
      }
      float2 ripples(float2 p, float t){
        float2 g = 0;
        g += float2(0.8, 0.6) * cos(dot(p, float2(0.8, 0.6)) * 0.35 + t * 0.9) * 0.35;
        g += float2(-0.5, 0.86) * cos(dot(p, float2(-0.5, 0.86)) * 0.62 + t * 1.3) * 0.22;
        g += float2(0.97, -0.25) * cos(dot(p, float2(0.97, -0.25)) * 1.4 + t * 1.9) * 0.12;
        g += (float2(at_noise(p * 0.18 + t * 0.05), at_noise(p * 0.18 - t * 0.04 + 9.0)) - 0.5) * 0.5;
        return g;
      }
      half4 frag (V i) : SV_Target {
        float t = _Time.y; float3 cam = _WorldSpaceCameraPos;
        float3 v = normalize(i.w - cam); float dist = length(i.w.xz - cam.xz);
        float fade = saturate(1.0 - dist / 900.0);
        float2 wg; AT_WaveH(i.w.xz, wg); wg *= fadeOf(i.w);
        float2 g = ripples(i.w.xz, t) * _Ripple * (0.25 + 0.75 * fade) + wg;
        float3 n = normalize(float3(-g.x, 1, -g.y));
        float3 r = reflect(v, n); r.y = max(r.y, 0.002);
        half3 c = AstralSky(r, t) * _Tint.rgb * _AT_SeaTint.rgb;
        float fres = pow(1.0 - saturate(-v.y), 3.0);
        c = lerp(c * 0.82 + _Deep.rgb * 0.18, c, fres);
        c += pow(saturate(dot(r, normalize(float3(_AT_GoalDir.x, max(_AT_GoalDir.y, 0.02), _AT_GoalDir.z)))), 300.0) * _AT_GlowCol.rgb * _AT_Gloss;   // glossy glint of the landmark on the swell
        float bs = at_fbm(float2(i.w.x * 0.012, i.w.z * 0.05) + t * 0.01);
        c *= 0.93 + bs * 0.14;
        float wk = 0;
        for (int k = 0; k < 8; k++){ float2 d = i.w.xz - _AT_Wake[k].xz; wk += _AT_Wake[k].w * exp(-dot(d, d) / (2.5 + k * 1.6)); }
        c += _AT_GlowCol.rgb * saturate(wk) * 0.35 * (0.7 + 0.3 * sin(t * 3 + i.w.x));
        // currents: glowing streams with streaks flowing along them
        float cur = 0; half3 curC = 0;
        for (int s = 0; s < 12; s++){
          if (s >= (int)_AT_CurrentInfo.x) break;
          float2 a = _AT_Current[s].xy, b = _AT_Current[s].zw, ab = b - a; float L = max(length(ab), 1.0); float2 dir = ab / L;
          float2 ap = i.w.xz - a; float u = clamp(dot(ap, dir), 0, L); float d = length(ap - dir * u);
          if (_AT_CurrentTexOn > 0.5){
            float side = dot(ap, float2(dir.y, -dir.x));
            if (abs(side) < _AT_CurrentInfo.y && dot(ap, dir) > -2 && dot(ap, dir) < L + 2){
              half4 tx = SAMPLE_TEXTURE2D_LOD(_AT_CurrentTex, sampler_AT_CurrentTex, float2(side / _AT_CurrentInfo.y * 0.5 + 0.5, u / (_AT_CurrentInfo.y * 4.0) - t * 0.35), 0);
              float endFade = saturate(u / 10.0) * saturate((L - u) / 10.0) + 0.0001;
              curC = max(curC, tx.rgb * tx.a * endFade * 1.3); cur = max(cur, tx.a * endFade);
            }
          } else {
            float body = exp(-pow(d / _AT_CurrentInfo.y, 2.0) * 1.6);
            float streak = smoothstep(0.55, 1.0, frac((u + _AT_Current[s].x * 0.37) / 14.0 - t * 0.55)) * (0.6 + 0.4 * sin(d * 0.9 + u * 0.05));
            float k2 = body * (0.35 + 0.9 * streak); cur = max(cur, k2); curC = max(curC, _AT_CurrentCol.rgb * k2);
          }
        }
        c += curC;
        float2 rel = i.w.xz - _AT_Course.xy; float lat = abs(rel.x * _AT_Course.w - rel.y * _AT_Course.z); float along = dot(rel, _AT_Course.zw);
        float fog = smoothstep(_AT_FogBand.x, _AT_FogBand.y, lat) + smoothstep(-_AT_FogBand.z * 0.5, -_AT_FogBand.z * 1.5, along);
        fog = saturate(fog) * (0.75 + 0.25 * at_noise(i.w.xz * 0.02 + t * 0.03));
        c = lerp(c, _AT_FogCol.rgb, fog * 0.85);
        float a = lerp(0.5, 0.66, fres) + _AT_SeaTint.a;
        a = saturate(max(a, max(fog * 0.9, max(saturate(wk) * 0.6, cur * 0.8))));
        return half4(c, a);
      }
      ENDHLSL
    }
  }
}
