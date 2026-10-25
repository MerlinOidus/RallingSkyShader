// Full-screen effects for screen filter triggers (ScreenFilters.cs, drawn by ScreenFilterFeature after post-processing):
// Glitch, Grayscale, Negative, Chromatic aberration, Scan lines and Hue shift. Each is 0 (off) .. 1 (full), set as
// globals, so several can be on at once. In Resources so a build always has it.
Shader "RTXOn/ScreenFilters"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off
        Blend Off

        Pass
        {
            Name "RTX Screen Filters"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _RTXFilterA;     // x Glitch, y Grayscale, z Negative, w Chromatic
            float4 _RTXFilterB;     // x Scan lines, y Hue shift
            float4 _RTXFilterSpeed; // x Glitch jumps, y Scan line scroll, z Hue turns per second
            float _RTXFilterTime;

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float3 Sample(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
            }

            // Turns the colour around the grey axis (Rodrigues rotation): a hue shift that keeps brightness.
            float3 HueRotate(float3 c, float angle)
            {
                const float3 k = float3(0.57735, 0.57735, 0.57735);
                float s, co;
                sincos(angle, s, co);
                return c * co + cross(k, c) * s + k * dot(k, c) * (1.0 - co);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float t = _RTXFilterTime;
                float glitch = _RTXFilterA.x, gray = _RTXFilterA.y, negative = _RTXFilterA.z, chroma = _RTXFilterA.w;
                float scan = _RTXFilterB.x, hue = _RTXFilterB.y;

                // Glitch: bands of the picture jump sideways with a colour split, a new pattern Speed x 12 times a
                // second; now and then the whole picture jumps up or down.
                float split = 0.0;
                if (glitch > 0.001)
                {
                    float tick = floor(t * 12.0 * _RTXFilterSpeed.x);
                    float band = floor(uv.y * 18.0);
                    float on = step(1.0 - 0.45 * glitch, Hash(float2(band, tick)));
                    uv.x += on * (Hash(float2(tick, band + 7.0)) - 0.5) * 0.12 * glitch;
                    float big = step(0.93, Hash(float2(tick, 3.3))) * glitch;
                    uv.y = frac(uv.y + big * (Hash(float2(tick, 9.1)) - 0.5) * 0.08);
                    split = 0.012 * glitch * saturate(on + big);
                }

                // Chromatic aberration: red and blue pulled apart towards the screen edges.
                float2 off = (uv - 0.5) * 0.025 * chroma + float2(split, 0.0);
                float3 c;
                c.r = Sample(uv + off).r;
                c.g = Sample(uv).g;
                c.b = Sample(uv - off).b;
                c = saturate(c);

            #if !defined(UNITY_COLORSPACE_GAMMA)
                c = LinearToSRGB(c); // the effects work on the colours as they look on screen
            #endif

                // Hue shift: half a turn (opposite colours); Speed above 0 keeps turning (rainbow).
                if (hue > 0.001)
                    c = lerp(c, saturate(HueRotate(c, (0.5 + t * _RTXFilterSpeed.z) * TWO_PI)), hue);

                float luma = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = lerp(c, luma.xxx, gray);
                c = lerp(c, 1.0 - c, negative);

                // Scan lines: dark lines every 3 pixels, scrolling with Speed, and a soft bright bar rolling down.
                if (scan > 0.001)
                {
                    float lines = 0.5 + 0.5 * sin((input.positionCS.y + t * 30.0 * _RTXFilterSpeed.y) * (TWO_PI / 3.0));
                    float roll = frac(uv.y + t * 0.25 * _RTXFilterSpeed.y) - 0.5;
                    c *= lerp(1.0, 0.55 + 0.45 * lines, scan);
                    c += scan * 0.06 * exp(-roll * roll * 140.0);
                }

            #if !defined(UNITY_COLORSPACE_GAMMA)
                c = SRGBToLinear(saturate(c));
            #endif
                return half4(c, 1.0);
            }
            ENDHLSL
        }
    }
}
