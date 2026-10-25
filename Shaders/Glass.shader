// Realistic glass for URP (RTX ON > Make Fragile Glass builds M_FragileRTX with it).
// What makes it read as glass:
//  - refraction: the scene behind is bent through the surface (needs Opaque Texture on the URP asset),
//    with a little colour split at the bends (dispersion) and optional frosting (blur);
//  - Fresnel: clear when seen from above, more and more mirror towards grazing angles;
//  - reflections from the reflection probes / HDRI sky, and a sharp sun highlight;
//  - colour depth: light that travels further through the glass (grazing angles, the thick sides) gets more tint;
//  - glowing lines around each fragile section (a Fragile section = one slab, a Checker tile = one block: the game
//    tells every cell which edges join its section, _Joined) and glowing sides.
// Unlit by URP's standards (one forward pass); the ball's shadow still darkens it.
Shader "RTXOn/Glass"
{
    Properties
    {
        [Header(Glass)]
        _BaseColor ("Tint", Color) = (0.86, 0.96, 1, 1)
        _BaseMap ("Pattern (RGB tints the glass, A = paint)", 2D) = "white" {}
        _Paint ("Paint Opacity (pattern alpha drawn solid)", Range(0, 1)) = 0
        _Thickness ("Thickness (colour depth)", Range(0, 4)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.97

        [Header(Refraction)]
        _IOR ("Index Of Refraction (glass 1.5)", Range(1, 2.5)) = 1.5
        _Refraction ("Refraction Strength", Range(0, 0.3)) = 0.08
        _Dispersion ("Rainbow Edges (Dispersion)", Range(0, 1)) = 0.2
        _Frost ("Frost (blur)", Range(0, 1)) = 0

        [Header(Reflection)]
        _Reflection ("Reflection Strength", Range(0, 2)) = 1
        _Specular ("Sun Highlight", Range(0, 4)) = 1.5
        _ShadowDim ("Shadow On Glass", Range(0, 1)) = 0.5

        [Header(Surface)]
        [Normal] _BumpMap ("Normal Map (ripples, scratches)", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 0.3
        _EdgeTint ("Side Colour (thick edges)", Color) = (0.5, 0.86, 0.8, 1)
        _Edge ("Side Strength", Range(0, 1)) = 0.6

        [Header(Tile Lines)]
        _Lines ("Section Outline", Range(0, 1)) = 1
        [HDR] _LineColor ("Line Colour (tile outline + sides)", Color) = (0.3, 1.6, 1.6, 1)
        _LineWidth ("Line Width", Range(0, 0.2)) = 0.035
        _LineSoft ("Line Softness", Range(0.001, 0.2)) = 0.03
        _SideGlow ("Side Glow", Range(0, 1)) = 0.6

        [HideInInspector] _Joined ("Joined Edges (set by the game)", Vector) = (0, 0, 0, 0)

        [Header(Glow)]
        _EmissionMap ("Emission Map (cracks)", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "GlassForward"
            Tags { "LightMode" = "UniversalForward" }
            // The pass draws the refracted scene itself, so it replaces what is behind instead of blending.
            Blend One Zero
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            // Forward+ (Unity 6.0) / cluster light loop (Unity 6.1+): probes are looked up per screen tile.
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_EmissionMap);    SAMPLER(sampler_EmissionMap);
            TEXTURE2D_X(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _EmissionMap_ST;
                half4 _BaseColor;
                half4 _EdgeTint;
                half4 _EmissionColor;
                half _Paint, _Thickness, _Smoothness;
                half _IOR, _Refraction, _Dispersion, _Frost;
                half _Reflection, _Specular, _ShadowDim;
                half _BumpScale, _Edge;
                half4 _LineColor;
                float4 _Joined;
                half _Lines, _LineWidth, _LineSoft, _SideGlow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = n.normalWS;
                o.tangentWS = half4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Poisson-ish ring for frosting.
            static const float2 FrostTaps[8] =
            {
                float2(0.94, 0.12), float2(-0.38, 0.86), float2(-0.82, -0.44), float2(0.30, -0.92),
                float2(0.46, 0.42), float2(-0.52, 0.18), float2(0.06, -0.48), float2(-0.14, 0.50),
            };

            half3 Scene(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_CameraOpaqueTexture, sampler_CameraOpaqueTexture,
                    UnityStereoTransformScreenSpaceTex(clamp(uv, 0.001, 0.999)), 0).rgb;
            }

            half3 FrostedScene(float2 uv)
            {
                half3 c = Scene(uv);
                float2 radius = _Frost * 0.03 * float2(_ScreenParams.y / _ScreenParams.x, 1.0);
                UNITY_UNROLL
                for (int k = 0; k < 8; k++) c += Scene(uv + FrostTaps[k] * radius);
                return c / 9.0;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = TRANSFORM_TEX(i.uv, _BaseMap);
                half4 pattern = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);

                // Normal: geometric (for the thick sides) and with the normal map (for the surface).
                half3 geomN = normalize(i.normalWS);
                half3 tangent = i.tangentWS.xyz;
                half3 bitangent = cross(i.normalWS, tangent) * i.tangentWS.w;
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, TRANSFORM_TEX(i.uv, _BumpMap)), _BumpScale);
                half3 N = normalize(TransformTangentToWorld(nTS, half3x3(tangent, bitangent, i.normalWS))); // no tangents: flat
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half NdotV = saturate(dot(N, V));

                // ---- Refraction: how far the bent ray moves on screen.
                float3 bent = refract(-V, N, 1.0 / _IOR) + V;
                float2 offset = mul((float3x3)UNITY_MATRIX_V, bent).xy * _Refraction;
                offset.x *= _ScreenParams.y / _ScreenParams.x;
                half3 behind;
                if (_Frost > 0.001)
                    behind = FrostedScene(screenUV + offset);
                else
                {
                    // Red bends least, blue most: thin rainbow fringes where the glass bends the view.
                    float spread = _Dispersion * 0.5;
                    behind.r = Scene(screenUV + offset * (1.0 - spread)).r;
                    behind.g = Scene(screenUV + offset).g;
                    behind.b = Scene(screenUV + offset * (1.0 + spread)).b;
                }

                // ---- Colour depth: longer path through the glass = deeper tint; the sides are the thickest.
                half path = 1.0 / max(NdotV, 0.25);
                half3 tint = max(_BaseColor.rgb * pattern.rgb, 0.001);
                half3 transmit = behind * pow(tint, _Thickness * path);
                half sideness = saturate(1.0 - abs(geomN.y));
                half side = sideness * _Edge;
                transmit *= lerp(half3(1, 1, 1), _EdgeTint.rgb, side);

                // ---- Light.
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half shadow = sun.shadowAttenuation * sun.distanceAttenuation;
                transmit *= lerp(1.0, shadow, _ShadowDim);

                half perceptualRoughness = lerp(1.0 - _Smoothness, max(1.0 - _Smoothness, 0.45), _Frost);
                half f0 = pow((_IOR - 1.0) / (_IOR + 1.0), 2.0); // 0.04 for glass
                half fresnel = f0 + (1.0 - f0) * pow(1.0 - NdotV, 5.0);
                half3 reflection = GlossyEnvironmentReflection(reflect(-V, N), i.positionWS, perceptualRoughness, 1.0h, screenUV) * _Reflection;

                // Sun highlight (GGX, as URP Lit does).
                float3 H = SafeNormalize(sun.direction + V);
                half NoH = saturate(dot(N, H));
                half LoH = saturate(dot(sun.direction, H));
                half NoL = saturate(dot(N, sun.direction));
                half r = max(perceptualRoughness * perceptualRoughness, 0.004);
                half r2 = r * r;
                float d = NoH * NoH * (r2 - 1.0) + 1.00001;
                half specular = min(r2 / ((d * d) * max(0.1, LoH * LoH) * (r * 4.0 + 2.0)), 1000.0);
                half sunFresnel = f0 + (1.0 - f0) * pow(1.0 - LoH, 5.0);
                half3 highlight = specular * sunFresnel * NoL * shadow * sun.color * _Specular;

                half3 glass = transmit * (1.0 - fresnel) + reflection * fresnel + highlight;

                // Paint: the pattern's alpha drawn as a solid, lit layer (frames, prints, dirt).
                half paint = pattern.a * _Paint;
                half3 paintColor = pattern.rgb * _BaseColor.rgb * (sun.color * NoL * shadow + SampleSH(N)) + highlight;
                half3 color = lerp(glass, paintColor, paint);

                // Section outline: a glowing line along the cell's edges (per-cell UV 0..1), except the edges that join
                // another tile of the same section; and glowing sides.
                float2 cell = i.uv;
                float4 edges = float4(cell.x, 1.0 - cell.x, cell.y, 1.0 - cell.y) + _Joined * 4.0;
                half edgeDist = min(min(edges.x, edges.y), min(edges.z, edges.w));
                half outline = 1.0 - smoothstep(_LineWidth, _LineWidth + _LineSoft, edgeDist);
                half lineMask = saturate(max(outline * _Lines * (1.0 - sideness), sideness * _SideGlow));
                color = lerp(color, _LineColor.rgb, lineMask * 0.85) + _LineColor.rgb * lineMask * 0.15;

                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, TRANSFORM_TEX(i.uv, _EmissionMap)).rgb * _EmissionColor.rgb;

                // Fog: far glass fades into what is behind it (that is already fogged), not into an extra layer of fog.
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                    color = lerp(Scene(screenUV), color, ComputeFogIntensity(i.fogFactor));
                #endif
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
