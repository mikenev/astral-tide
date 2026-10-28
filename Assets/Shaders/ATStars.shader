// Astral Tide: the star veil over Art Dev's painted sky (Skybox/Panoramic underneath). An additive inside-out dome: the
// AT_Starfield layer and the procedural twinkles come out as the leg goes on (_AT_StarAmount), plus the Lantern Spire's glow on
// the horizon. The sea draws the same function along its reflected rays, so the water mirrors it.
Shader "AstralTide/Stars" {
  SubShader {
    Tags { "RenderType"="Transparent" "Queue"="Transparent-499" "RenderPipeline"="UniversalPipeline" }
    Pass {
      Tags { "LightMode"="UniversalForward" }
      Blend One One ZWrite Off ZTest LEqual Cull Front
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "AstralSky.hlsl"
      struct A { float4 pos : POSITION; };
      struct V { float4 pos : SV_POSITION; float3 d : TEXCOORD0; };
      V vert (A i){ V o; float3 w = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(w); o.d = w - _WorldSpaceCameraPos; return o; }
      half4 frag (V i) : SV_Target { float3 d = normalize(i.d); half3 c = AstralStarsAndGlow(d, _Time.y); if (d.y < 0) c *= 0.6; return half4(c, 1); }
      ENDHLSL
    }
  }
}
