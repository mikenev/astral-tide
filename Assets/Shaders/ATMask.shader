// Astral Tide: depth-only water mask. A lid across the hull at the waterline, drawn after the ship and before the sea: it writes
// depth but no colour, so the sea can't draw inside the hull (Mike's M1 note: the boat looked like it was sinking).
Shader "AstralTide/WaterMask" {
  SubShader {
    Tags { "RenderType"="Opaque" "Queue"="Transparent-501" "RenderPipeline"="UniversalPipeline" }
    Pass {
      Tags { "LightMode"="UniversalForward" }
      ColorMask 0 ZWrite On ZTest LEqual Cull Off
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      struct A { float4 pos : POSITION; };
      struct V { float4 pos : SV_POSITION; };
      V vert (A i){ V o; o.pos = TransformObjectToHClip(i.pos.xyz); return o; }
      half4 frag (V i) : SV_Target { return 0; }
      ENDHLSL
    }
  }
}
