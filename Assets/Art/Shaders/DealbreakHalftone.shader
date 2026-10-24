// Flat UI fills printed like the item and character art (owner, 2026-10-24): the fill colour with a
// darker halftone dot screen, light at the top of a shape and denser toward its foot. Dots sit on a
// fixed 45° grid in canvas units, so they move with their element and keep one size everywhere.
// TEXCOORD0 is the position inside the shape (0..1, y up); a negative x marks geometry that prints
// solid (borders, rings), so hairlines never break up into dots.
Shader "UI/Dealbreak Halftone"
{
 Properties {
  [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
  _Color ("Tint", Color) = (1,1,1,1)
  _Cell ("Dot cell (canvas px)", Float) = 7
  _Angle ("Screen angle (degrees)", Float) = 45
  _DensityTop ("Dot coverage at the top", Range(0,1)) = 0
  _DensityBottom ("Dot coverage at the foot", Range(0,1)) = 0.5
  _Ink ("Dot ink", Color) = (0.043,0.043,0.047,1)
  _Strength ("Dot ink strength", Range(0,1)) = 0.38
  _StencilComp ("Stencil Comparison", Float) = 8
  _Stencil ("Stencil ID", Float) = 0
  _StencilOp ("Stencil Operation", Float) = 0
  _StencilWriteMask ("Stencil Write Mask", Float) = 255
  _StencilReadMask ("Stencil Read Mask", Float) = 255
  _ColorMask ("Color Mask", Float) = 15
 }
 SubShader {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
  Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   struct v2f {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float4 local:TEXCOORD1;};
   sampler2D _MainTex;fixed4 _Color;fixed4 _TextureSampleAdd;float4 _ClipRect;
   float _Cell,_Angle,_DensityTop,_DensityBottom,_Strength;fixed4 _Ink;
   v2f vert(appdata v){v2f o;o.local=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;}
   fixed4 frag(v2f i):SV_Target {
    fixed4 col=(tex2D(_MainTex,saturate(i.uv))+_TextureSampleAdd)*i.color;
    if(i.uv.x>=0){
     float t=radians(_Angle);
     float2 p=float2(i.local.x*cos(t)+i.local.y*sin(t),-i.local.x*sin(t)+i.local.y*cos(t))/_Cell;
     float d=length(frac(p)-.5);
     float coverage=lerp(_DensityBottom,_DensityTop,saturate(i.uv.y));
     float radius=sqrt(saturate(coverage)/3.14159);
     float aa=max(fwidth(d),1e-4);
     float dot=1-smoothstep(radius-aa,radius+aa,d);
     col.rgb=lerp(col.rgb,col.rgb*(1-_Strength)+_Ink.rgb*_Strength*.5,dot*step(1e-3,coverage));
    }
    #ifdef UNITY_UI_CLIP_RECT
    col.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
    clip(col.a-0.001);
    #endif
    return col;
   }
   ENDCG
  }
 }
}
