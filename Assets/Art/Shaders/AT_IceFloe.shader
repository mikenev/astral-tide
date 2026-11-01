// Astral Tide – faceted ice for code-built meshes with no UVs (e.g. VoyageLegs "IceFloe").
// Albedo = tiling ice texture projected top-down in WORLD XZ x vertex colour x tint; flat facet
// normals from screen derivatives; one fixed key light + ambient; bright fresnel rim as emission.
// Pipeline-agnostic unlit pass (renders in URP as SRPDefaultUnlit). Pair with Bloom for the rim glow.
Shader "AstralTide/IceFloe"
{
  Properties
  {
    _MainTex ("Ice Tile (world XZ)", 2D) = "white" {}
    _Tiling ("Tiles per metre", Float) = 0.25
    _Tint ("Tint", Color) = (0.92,0.97,1,1)
    _UseVertexColor ("Use vertex colour (0-1)", Range(0,1)) = 0.6
    _LightDir ("Key light dir (world, toward light)", Vector) = (0.4,0.8,0.3,0)
    _Ambient ("Ambient", Color) = (0.55,0.65,0.8,1)
    _RimColor ("Rim colour (HDR)", Color) = (1.2,1.8,2.2,1)
    _RimPower ("Rim power", Range(0.5,8)) = 2.5
    _EdgeGlow ("Top-edge sparkle", Range(0,2)) = 0.6
  }
  SubShader
  {
    Tags { "RenderType"="Opaque" "Queue"="Geometry" }
    Pass
    {
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "UnityCG.cginc"
      sampler2D _MainTex; float _Tiling; float4 _Tint; float _UseVertexColor; float4 _LightDir; float4 _Ambient; float4 _RimColor; float _RimPower; float _EdgeGlow;
      struct a2v { float4 vertex:POSITION; float4 color:COLOR; };
      struct v2f { float4 pos:SV_POSITION; float3 wpos:TEXCOORD0; float4 col:COLOR; };
      v2f vert(a2v v){ v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.wpos=mul(unity_ObjectToWorld,v.vertex).xyz; o.col=v.color; return o; }
      float4 frag(v2f i):SV_Target
      {
        float3 n=normalize(cross(ddy(i.wpos),ddx(i.wpos)));
        float3 V=normalize(_WorldSpaceCameraPos-i.wpos);
        if(dot(n,V)<0) n=-n;
        float3 tex=tex2D(_MainTex,i.wpos.xz*_Tiling).rgb;
        float3 vc=lerp(float3(1,1,1),i.col.rgb,_UseVertexColor);
        float3 alb=tex*vc*_Tint.rgb;
        float ndl=saturate(dot(n,normalize(_LightDir.xyz)));
        float3 lit=alb*(_Ambient.rgb+ndl*0.75);
        float rim=pow(1-saturate(dot(n,V)),_RimPower);
        float top=saturate(n.y)*saturate(1-abs(n.y-0.85)*6)*_EdgeGlow; // sparkle on sloped top facets
        return float4(lit+_RimColor.rgb*rim+_RimColor.rgb*top*0.25,1);
      }
      ENDHLSL
    }
  }
  Fallback "Unlit/Color"
}
