// Astral Tide: the sky dome (an inside-out sphere that follows the camera). Upper half: the twilight sky; lower half: its mirror,
// which is what you see through the sea surface behind the ripples.
Shader "AstralTide/Sky" {
  SubShader {
    Tags { "RenderType"="Background" "Queue"="Background" "RenderPipeline"="UniversalPipeline" "PreviewType"="Skybox" }
    Pass {
      Tags { "LightMode"="UniversalForward" }
      ZWrite Off Cull Front
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "AstralSky.hlsl"
      struct A { float4 pos : POSITION; };
      struct V { float4 pos : SV_POSITION; float3 d : TEXCOORD0; };
      V vert (A i){ V o; float3 w = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(w); o.d = w - _WorldSpaceCameraPos; return o; }
      half4 frag (V i) : SV_Target { float3 d = normalize(i.d); half3 c = AstralSky(d, _Time.y); if (d.y < 0) c *= 0.78; return half4(c, 1); }
      ENDHLSL
    }
  }
}
