// The midground blocks (Midground): unlit atlas texture that fades to its own Mid Fog colour with distance, like the
// original 6_Midground ("Lerped, Custom-Fog": lerp(texture, _ColorF, depth)). The scene fog does not touch it.
// Written with UnityCG so it compiles in any pipeline (URP renders it as an unlit pass). It sits in a Resources
// folder so builds keep it (Midground finds it by name).
Shader "RTXOn/Midground"
{
    Properties
    {
        _MainTex ("Texture (Tile Fragile atlas)", 2D) = "white" {}
        [HDR] _Tint ("Tint", Color) = (1, 1, 1, 1)
        _FogColor ("Mid Fog Colour", Color) = (0.07, 0.85, 0.88, 1)
        _FogStart ("Mid Fog Start", Float) = 17
        _FogEnd ("Mid Fog End", Float) = 30
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Midground"
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Tint, _FogColor;
            float _FogStart, _FogEnd;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float depth : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.depth = -UnityObjectToViewPos(v.vertex).z; // distance in front of the camera
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb * _Tint.rgb;
                float fog = saturate((i.depth - _FogStart) / max(0.01, _FogEnd - _FogStart));
                return float4(lerp(c, _FogColor.rgb, fog), 1);
            }
            ENDCG
        }
    }
}
