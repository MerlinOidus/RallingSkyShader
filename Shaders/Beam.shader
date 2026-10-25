// Additive glowing beam for lasers and spotlights. Soft at the silhouette, bright in the core,
// so with Bloom it reads like a volumetric light shaft. Fades out near the camera, so a beam that swings
// through the view (spotlights passing under the camera) never flashes the whole screen white.
// Written with UnityCG so it compiles in any pipeline (URP renders it as an unlit pass).
Shader "RTXOn/Beam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (4, 0.4, 0.3, 1)
        _Core ("Core Sharpness", Range(0.5, 8)) = 2.5
        _NearFadeStart ("Near Fade Start", Float) = 1.5
        _NearFadeEnd ("Near Fade End", Float) = 5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Beam"
            Blend One One
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            half4 _Color;
            half _Core;
            float _NearFadeStart;
            float _NearFadeEnd;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                float heightOS : TEXCOORD2;
                float3 posWS : TEXCOORD4;
                UNITY_FOG_COORDS(3)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                o.viewWS = WorldSpaceViewDir(v.vertex);
                o.heightOS = v.vertex.y; // Unity cylinder: -1 .. 1
                o.posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half facing = saturate(abs(dot(normalize(i.normalWS), normalize(i.viewWS))));
                half core = pow(facing, _Core);
                half ends = saturate(1.0 - abs(i.heightOS)) * 0.6 + 0.4;
                half near = saturate((distance(i.posWS, _WorldSpaceCameraPos) - _NearFadeStart) / max(_NearFadeEnd - _NearFadeStart, 0.01));
                fixed4 col = fixed4(_Color.rgb * core * ends * near * near, 1);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0)); // additive: fade to nothing in fog
                return col;
            }
            ENDCG
        }
    }
}
