// Astral Tide: the mirror sea. A large quad that follows the camera; each pixel reflects the sky function along the view ray
// bounced off a rippled normal, so stars and the spire glow shimmer in the water. Half transparent so the mirrored copies of
// the ship and the spire (drawn just before, below the surface) show through. Fog thickens past the edges of the course.
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
      CBUFFER_START(UnityPerMaterial) float _Ripple; float4 _Tint, _Deep; CBUFFER_END
      float4 _AT_Course;   // xy = start (x,z), zw = unit direction of the course line (x,z)
      float4 _AT_FogCol; float4 _AT_FogBand;   // x = fog start (lateral m), y = full fog, z = behind-start limit
      float4 _AT_Wake[8];  // xyz = recent stern points, w = age weight
      struct A { float4 pos : POSITION; };
      struct V { float4 pos : SV_POSITION; float3 w : TEXCOORD0; };
      V vert (A i){ V o; float3 w = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(w); o.w = w; return o; }
      float2 waves(float2 p, float t){
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
        float2 g = waves(i.w.xz, t) * _Ripple * (0.25 + 0.75 * fade);
        float3 n = normalize(float3(-g.x, 1, -g.y));
        float3 r = reflect(v, n); r.y = max(r.y, 0.002);
        half3 c = AstralSky(r, t) * _Tint.rgb;
        float fres = pow(1.0 - saturate(-v.y), 3.0);
        c = lerp(c * 0.82 + _Deep.rgb * 0.18, c, fres);
        // painterly brush streaks on the water
        float bs = at_fbm(float2(i.w.x * 0.012, i.w.z * 0.05) + t * 0.01);
        c *= 0.93 + bs * 0.14;
        // glowing wake: soft trail of light along recent stern points
        float wk = 0;
        for (int k = 0; k < 8; k++){ float2 d = i.w.xz - _AT_Wake[k].xz; wk += _AT_Wake[k].w * exp(-dot(d, d) / (2.5 + k * 1.6)); }
        c += _AT_GlowCol.rgb * saturate(wk) * 0.35 * (0.7 + 0.3 * sin(t * 3 + i.w.x));
        // fog at the edges of the course (and behind the start)
        float2 rel = i.w.xz - _AT_Course.xy; float lat = abs(rel.x * _AT_Course.w - rel.y * _AT_Course.z); float along = dot(rel, _AT_Course.zw);
        float fog = smoothstep(_AT_FogBand.x, _AT_FogBand.y, lat) + smoothstep(-_AT_FogBand.z * 0.5, -_AT_FogBand.z * 1.5, along);
        fog = saturate(fog) * (0.75 + 0.25 * at_noise(i.w.xz * 0.02 + t * 0.03));
        c = lerp(c, _AT_FogCol.rgb, fog * 0.85);
        float a = lerp(0.5, 0.66, fres);
        a = max(a, max(fog * 0.9, saturate(wk) * 0.6));
        return half4(c, a);
      }
      ENDHLSL
    }
  }
}
