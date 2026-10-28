// Astral Tide: additive glow. Used two ways: camera-facing billboard quads (lantern, spire halo, gust cores: _Billboard = 1, the
// quad's size from the object scale) and particle systems (gust sparkles, wake motes, arrival burst: vertex colour).
Shader "AstralTide/Glow" {
  Properties {
    _Color ("Colour", Color) = (1, 0.85, 0.6, 1)
    _Billboard ("Billboard", Float) = 0
    _Core ("Core sharpness", Float) = 2
  }
  SubShader {
    Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
    Pass {
      Tags { "LightMode"="UniversalForward" }
      Blend SrcAlpha One ZWrite Off Cull Off
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      CBUFFER_START(UnityPerMaterial) float4 _Color; float _Billboard, _Core; CBUFFER_END
      struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
      struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
      V vert (A i){
        V o;
        if (_Billboard > 0.5){
          float3 c = TransformObjectToWorld(float3(0, 0, 0));
          float sx = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
          float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
          float3 w = c + (right * i.pos.x + up * i.pos.y) * sx;
          o.pos = TransformWorldToHClip(w);
        } else o.pos = TransformObjectToHClip(i.pos.xyz);
        o.uv = i.uv; o.col = i.col; return o;
      }
      half4 frag (V i) : SV_Target {
        float d = length(i.uv - 0.5) * 2; float a = saturate(1 - d); a = a * a * (0.6 + 0.4 * pow(a, _Core) * 3);
        return half4(_Color.rgb * i.col.rgb, saturate(a * _Color.a * i.col.a));
      }
      ENDHLSL
    }
  }
}
