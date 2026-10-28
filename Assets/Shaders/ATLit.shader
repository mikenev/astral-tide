// Astral Tide: vertex-colour shading for the procedural placeholder ship and spire. Soft wrap light from the twilight sky
// (cool from above, warm from the goal side), an emissive channel in vertex alpha (lantern glass, glowing windows) and a
// mirrored variant (_Mirror = 1) that draws the reflection below the water: darker, rippled, ZTest Always under the sea.
Shader "AstralTide/Lit" {
  Properties {
    _Tint ("Tint", Color) = (1,1,1,1)
    _Glow ("Glow colour", Color) = (1, 0.85, 0.55, 1)
    _GlowBoost ("Glow boost", Float) = 1
    _Mirror ("Mirror (reflection pass)", Float) = 0
    _MainTex ("Albedo (optional)", 2D) = "white" {}
    _EmissionMap ("Emission map (optional)", 2D) = "black" {}
    _EmissionGain ("Emission gain", Float) = 0
    _VColor ("Use vertex colours", Float) = 1
    _ZTest ("ZTest", Float) = 4
    _ZWrite ("ZWrite", Float) = 1
  }
  SubShader {
    Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
    Pass {
      Tags { "LightMode"="UniversalForward" }
      Cull Off ZTest [_ZTest] ZWrite [_ZWrite]
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
      CBUFFER_START(UnityPerMaterial) float4 _Tint, _Glow, _MainTex_ST; float _GlowBoost, _Mirror, _EmissionGain, _VColor; float _ZTest, _ZWrite; CBUFFER_END
      float4 _AT_SkyMid, _AT_Horizon, _AT_GlowCol, _AT_GoalDir; float _AT_Flash;
      struct A { float4 pos : POSITION; float3 n : NORMAL; float4 col : COLOR; float2 uv : TEXCOORD0; };
      struct V { float4 pos : SV_POSITION; float4 col : COLOR; float3 n : TEXCOORD0; float3 w : TEXCOORD1; float2 uv : TEXCOORD2; };
      V vert (A i){
        V o; float3 w = TransformObjectToWorld(i.pos.xyz);
        if (_Mirror > 0.5) w.x += sin(w.y * 1.7 + _Time.y * 2.0) * 0.08 * -w.y * 0.15;
        o.pos = TransformWorldToHClip(w); o.w = w; o.n = TransformObjectToWorldNormal(i.n); o.col = i.col; o.uv = i.uv; return o;
      }
      half4 frag (V i) : SV_Target {
        float3 n = normalize(i.n);
        float sky = saturate(n.y * 0.5 + 0.5);
        float3 gd = normalize(float3(_AT_GoalDir.x, 0.2, _AT_GoalDir.z));
        float warm = saturate(dot(n, gd) * 0.6 + 0.4);
        half3 light = lerp(_AT_Horizon.rgb * 0.55, _AT_SkyMid.rgb * 0.9 + 0.25, sky) + _AT_GlowCol.rgb * warm * 0.25;
        float4 vc = _VColor > 0.5 ? i.col : float4(1, 1, 1, 1);
        half3 base = vc.rgb * _Tint.rgb * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
        half3 c = base * (_VColor > 0.5 ? light : lerp(light, 1.0, 0.55));   // painted albedos already carry their shading
        float e = 1 - vc.a;   // vertex alpha < 1 marks emissive parts (procedural meshes)
        c = lerp(c, _Glow.rgb * _GlowBoost * (1 + _AT_Flash), e);
        c += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionGain * (1 + _AT_Flash);
        if (_Mirror > 0.5){ c *= 0.55; }
        return half4(c, 1);
      }
      ENDHLSL
    }
  }
}
