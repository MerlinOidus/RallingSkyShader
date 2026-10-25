// Three-colour gradient sky with a soft sun. Predictable colours (unlike Skybox/Procedural,
// whose sky tint also tints the ground), and the bottom colour matches the fog so the track floats in mist.
// Optional HDRI layers: an environment with an HDRI (Environment Preset > Sky HDRI) shows it instead of the gradient
// (A = the environment blended from, B = the one blended to, _HdriBlend between them), so Day, Sunset and Night can
// each have their own HDRI and still fade into each other.
Shader "RTXOn/SkyGradient"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.2, 0.45, 0.88, 1)
        _HorizonColor ("Horizon", Color) = (0.82, 0.92, 1, 1)
        _BottomColor ("Bottom (match fog)", Color) = (0.74, 0.86, 0.98, 1)
        _TopFalloff ("Top Falloff", Range(0.1, 4)) = 0.55
        _BottomFalloff ("Bottom Falloff", Range(0.5, 20)) = 4
        [HDR] _SunColor ("Sun", Color) = (6, 5.6, 4.8, 1)
        _SunDir ("Sun Direction (towards sun)", Vector) = (0.43, 0.67, -0.61, 0)
        _SunSize ("Sun Size", Range(0.001, 0.2)) = 0.03
        _SunGlow ("Sun Glow", Range(0, 2)) = 0.35
        _Stars ("Stars", Range(0, 1)) = 0
        _Exposure ("Exposure", Range(0, 4)) = 1

        [NoScaleOffset] _HdriA ("HDRI A", Cube) = "black" {}
        _HdriMixA ("HDRI A Amount", Range(0, 1)) = 0
        _HdriExposureA ("HDRI A Exposure", Range(0, 8)) = 1
        _HdriRotationA ("HDRI A Rotation", Range(0, 360)) = 0
        _HdriTintA ("HDRI A Tint", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _HdriB ("HDRI B", Cube) = "black" {}
        _HdriMixB ("HDRI B Amount", Range(0, 1)) = 0
        _HdriExposureB ("HDRI B Exposure", Range(0, 8)) = 1
        _HdriRotationB ("HDRI B Rotation", Range(0, 360)) = 0
        _HdriTintB ("HDRI B Tint", Color) = (1, 1, 1, 1)
        _HdriBlend ("HDRI A -> B", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _TopColor, _HorizonColor, _BottomColor, _SunColor;
            float4 _SunDir;
            half _TopFalloff, _BottomFalloff, _SunGlow, _Exposure, _Stars;
            float _SunSize; // float: cos() of small angles needs full precision on mobile

            samplerCUBE _HdriA, _HdriB;
            half4 _HdriA_HDR, _HdriB_HDR, _HdriTintA, _HdriTintB;
            half _HdriMixA, _HdriMixB, _HdriExposureA, _HdriExposureB, _HdriBlend;
            float _HdriRotationA, _HdriRotationB;

            // An HDRI in direction d, turned around the vertical axis by degrees.
            half3 Hdri(samplerCUBE tex, half4 decode, float3 d, float degrees)
            {
                float s, c;
                sincos(radians(degrees), s, c);
                float3 r = float3(c * d.x - s * d.z, d.y, s * d.x + c * d.z);
                return DecodeHDR(texCUBE(tex, r), decode);
            }

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                half3 col = d.y >= 0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(d.y), _TopFalloff))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-d.y * _BottomFalloff));

                // HDRI layers over the gradient. The HDRI has its own sun and stars, so the drawn ones fade with it.
                half hdri = lerp(_HdriMixA, _HdriMixB, _HdriBlend);
                if (hdri > 0.001)
                {
                    half3 a = col, b = col;
                    if (_HdriMixA > 0.001) a = lerp(col, Hdri(_HdriA, _HdriA_HDR, d, _HdriRotationA) * _HdriExposureA * _HdriTintA.rgb, _HdriMixA);
                    if (_HdriMixB > 0.001) b = lerp(col, Hdri(_HdriB, _HdriB_HDR, d, _HdriRotationB) * _HdriExposureB * _HdriTintB.rgb, _HdriMixB);
                    col = lerp(a, b, _HdriBlend);
                }
                half drawn = 1 - hdri;

                float3 sun = normalize(_SunDir.xyz);
                float s = saturate(dot(d, sun));
                float disc = smoothstep(cos(_SunSize), cos(_SunSize * 0.6), s);
                float glow = pow(s, 12.0) * _SunGlow;
                col += _SunColor.rgb * (disc + glow * 0.15) * drawn;

                // Procedural twinkling stars on a direction grid, only above the horizon.
                if (_Stars > 0.001)
                {
                    float2 g = float2(atan2(d.x, d.z), asin(clamp(d.y, -1.0, 1.0))) * 70.0;
                    float2 cell = floor(g);
                    float2 f = frac(g) - 0.5;
                    float h = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
                    float twinkle = 0.6 + 0.4 * sin(_Time.y * 2.5 + h * 60.0);
                    float star = step(0.975, h) * smoothstep(0.18, 0.0, length(f)) * saturate(d.y * 5.0);
                    col += star * twinkle * _Stars * 2.5 * drawn;
                }

                return half4(col * _Exposure, 1);
            }
            ENDCG
        }
    }
}
