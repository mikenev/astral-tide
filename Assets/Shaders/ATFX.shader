// Astral Tide: textured unlit FX (Art Dev's AT_Star sparkles, the AT_Wake trail, star-gust cores). Additive by default.
// _SwapUV = 1 for the wake: Art Dev's texture runs along V, a TrailRenderer's texture runs along U. _Billboard = 1 turns a quad
// to face the camera (size from the object scale).
Shader "AstralTide/FX" {
  Properties {
    _MainTex ("Texture", 2D) = "white" {}
    _Color ("Colour (HDR)", Color) = (1,1,1,1)
    _SwapUV ("Swap UV (wake trail)", Float) = 0
    _Billboard ("Billboard", Float) = 0
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
      TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
      CBUFFER_START(UnityPerMaterial) float4 _MainTex_ST; float4 _Color; float _SwapUV, _Billboard; CBUFFER_END
      struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
      struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
      V vert (A i){
        V o;
        if (_Billboard > 0.5){
          float3 c = TransformObjectToWorld(float3(0, 0, 0));
          float sx = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
          o.pos = TransformWorldToHClip(c + (UNITY_MATRIX_V[0].xyz * i.pos.x + UNITY_MATRIX_V[1].xyz * i.pos.y) * sx);
        } else o.pos = TransformObjectToHClip(i.pos.xyz);
        float2 uv = TRANSFORM_TEX(i.uv, _MainTex); o.uv = _SwapUV > 0.5 ? uv.yx : uv; o.col = i.col; return o;
      }
      half4 frag (V i) : SV_Target { half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv); return half4(t.rgb * _Color.rgb * i.col.rgb, saturate(t.a * _Color.a * i.col.a)); }
      ENDHLSL
    }
  }
}
