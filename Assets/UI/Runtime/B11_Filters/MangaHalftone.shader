Shader "UI Toolkit/Filters/MangaHalftone" {
  Properties { _MainTex ("Texture", 2D) = "white" {} _DotSpacing ("Dot Spacing (px)", Float) = 6 _InkColor ("Ink Color", Color) = (0.08,0.06,0.1,1) }
  SubShader { Tags { "RenderType"="Opaque" } Blend One Zero ZWrite Off ZTest Always Cull Off
    Pass { CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile _ _UIE_OUTPUT_LINEAR
      #include "UnityCG.cginc"
      #include "UnityUIEFilter.cginc"
      struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; uint rectIndex:TEXCOORD1; };
      sampler2D _MainTex; float4 _MainTex_ST; float4 _MainTex_TexelSize; float _DotSpacing; float4 _InkColor;
      v2f vert (FilterVertexInput v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.uv,_MainTex); o.rectIndex=GetFilterRectIndex(v); return o; }
      static const float2x2 kRot=float2x2(0.70710678,-0.70710678,0.70710678,0.70710678), kRotInv=float2x2(0.70710678,0.70710678,-0.70710678,0.70710678);
      fixed4 frag (v2f i) : SV_Target {
        float4 uvRect = GetFilterUVRect(i.rectIndex);
        float2 px = (i.uv - uvRect.xy) * _MainTex_TexelSize.zw;
        float spacing = max(_DotSpacing, 1.0);
        float2 g = mul(kRot, px) / spacing; float2 cell = floor(g) + 0.5;
        float2 ht = 0.5 * _MainTex_TexelSize.xy;
        float2 cuv = clamp(uvRect.xy + mul(kRotInv, cell*spacing) * _MainTex_TexelSize.xy, uvRect.xy+ht, uvRect.xy+uvRect.zw-ht);
        half4 cs = tex2D(_MainTex, cuv);
        half3 crgb = cs.a > 0.0001 ? cs.rgb / cs.a : 0;
        float lum = dot(crgb, float3(0.299,0.587,0.114));
        float radius = sqrt(saturate(1.0 - lum)) * 0.70710678;
        float d = length(g - cell); float aa = max(fwidth(d), 1e-4);
        float ink = 1.0 - smoothstep(radius-aa, radius+aa, d);
        half4 src = tex2D(_MainTex, i.uv);
        half3 rgb = lerp(half3(1,1,1), _InkColor.rgb, ink * _InkColor.a);
        half4 col = half4(rgb * src.a, src.a);
        #if _UIE_OUTPUT_LINEAR
        col.rgb = GammaToLinearSpace(col.rgb);
        #endif
        return col; }
      ENDCG } } }
