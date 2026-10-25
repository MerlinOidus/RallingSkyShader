// The theme's background picture behind the whole track (BackgroundView). It hangs at the far end of the camera's
// view and is drawn before the see-through things, so only the sky (where nothing solid stands) shows it.
// Two pictures with a blend for look changes; a missing picture fades to the sky instead of to black.
// Written with UnityCG so it compiles in any pipeline (URP renders it as an unlit pass). It sits in a Resources
// folder so builds keep it (BackgroundView finds it by name).
Shader "RTXOn/Background"
{
    Properties
    {
        _MainTex ("Background", 2D) = "black" {}
        _AltTex ("Next Background", 2D) = "black" {}
        _Blend ("Blend", Range(0, 1)) = 0
        _AlphaA ("Background Shown", Range(0, 1)) = 1
        _AlphaB ("Next Background Shown", Range(0, 1)) = 1
        [HDR] _TintA ("Tint", Color) = (1, 1, 1, 1)
        [HDR] _TintB ("Next Tint", Color) = (1, 1, 1, 1)
        _GradA ("Gradient (0 None, 1 Bottom, 2 Top, 3 Both)", Float) = 0
        _GradB ("Next Gradient", Float) = 0
        _GradSize ("Gradient Size (part of the screen)", Range(0.05, 1)) = 0.45
        _GradColor ("Gradient Colour (fog)", Color) = (0.74, 0.86, 0.98, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-499" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Name "Background"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _AltTex;
            float _Blend, _AlphaA, _AlphaB;
            float4 _TintA, _TintB;
            float _GradA, _GradB, _GradSize;
            float4 _GradColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screen : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            // How much of the fog colour covers the picture at screen height y (0 = bottom, 1 = top).
            float Gradient (float style, float y)
            {
                float bottom = 1.0 - smoothstep(0.0, _GradSize, y);
                float top = smoothstep(1.0 - _GradSize, 1.0, y);
                if (style > 2.5) return max(bottom, top);
                if (style > 1.5) return top;
                if (style > 0.5) return bottom;
                return 0.0;
            }

            float4 frag (v2f i) : SV_Target
            {
                float y = saturate(i.screen.y / max(i.screen.w, 0.0001));
                float3 a = tex2D(_MainTex, i.uv).rgb * _TintA.rgb;
                float3 b = tex2D(_AltTex, i.uv).rgb * _TintB.rgb;
                a = lerp(a, _GradColor.rgb, Gradient(_GradA, y));
                b = lerp(b, _GradColor.rgb, Gradient(_GradB, y));
                // Blend with premultiplied alpha, so fading to "no picture" shows the sky, not black.
                float4 c = lerp(float4(a * _AlphaA, _AlphaA), float4(b * _AlphaB, _AlphaB), _Blend);
                return float4(c.a > 0.0001 ? c.rgb / c.a : float3(0, 0, 0), c.a);
            }
            ENDCG
        }
    }
}
