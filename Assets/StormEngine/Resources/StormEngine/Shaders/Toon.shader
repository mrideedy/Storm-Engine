// STORM ENGINE - cel shaded "cartoon" surface for URP.
// Two-band lighting with real main-light shadows, a glossy highlight, a rim light, a sky/ground
// hemisphere tint, emission (for glows picked up by bloom) and an inverted-hull ink outline.
// Grass and flowers can sway in the wind (weight = vertex colour alpha, amount = _Sway).
Shader "StormEngine/Toon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Tint", Color) = (0.62, 0.66, 0.86, 1)
        _RimColor ("Rim Color", Color) = (1, 1, 1, 0.35)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 0.55)
        _Gloss ("Gloss", Range(4, 256)) = 64
        _Emission ("Emission", Color) = (0, 0, 0, 0)
        _OutlineColor ("Outline Color", Color) = (0.08, 0.1, 0.2, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.02)) = 0.004
        _BandSoftness ("Band Softness", Range(0.001, 0.3)) = 0.04
        _Sway ("Wind Sway", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadeColor;
            half4 _RimColor;
            half _RimPower;
            half4 _HighlightColor;
            half _Gloss;
            half4 _Emission;
            half4 _OutlineColor;
            half _OutlineWidth;
            half _BandSoftness;
            half _Sway;
        CBUFFER_END

        // Scene-wide controls set by WorldView / EnvironmentRig. All default to "no effect" at 0.
        half4 _StormAmbient;
        half _StormDarken;
        half4 _SkyTint;
        half4 _GroundTint;
        half _HemiStrength;
        half _VibranceBoost;
        half _WindStrength;

        float3 ApplySway(float3 positionWS, half weight)
        {
            if (_Sway <= 0.0001h) return positionWS;
            float t = _Time.y;
            float2 wave = float2(sin(t * 1.9 + positionWS.x * 0.7 + positionWS.z * 0.4), cos(t * 1.6 + positionWS.z * 0.8 - positionWS.x * 0.3));
            positionWS.xz += wave * (_Sway * (0.35 + _WindStrength) * weight * 0.09);
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                float3 positionWS = ApplySway(TransformObjectToWorld(input.positionOS.xyz), input.color.a);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.positionWS = positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);

                half ndl = dot(n, light.direction) * 0.5h + 0.5h;
                half band = smoothstep(0.5h - _BandSoftness, 0.5h + _BandSoftness, ndl);
                half shadow = smoothstep(0.35h, 0.65h, light.shadowAttenuation);
                half lit = band * shadow;

                half3 baseColor = _BaseColor.rgb * i.color.rgb;
                // Cast shadows on light-facing surfaces read a touch deeper than the terminator band,
                // so objects look grounded and the shadow shapes stay legible.
                half cast = (1.0h - shadow) * band;
                half3 shaded = baseColor * _ShadeColor.rgb * (1.0h - 0.16h * cast);
                half3 lightTint = lerp(half3(1, 1, 1), light.color, 0.35h);
                half3 color = lerp(shaded, baseColor * lightTint, lit);

                float3 h = normalize(light.direction + v);
                half spec = pow(saturate(dot(n, h)), _Gloss);
                spec = smoothstep(0.45h, 0.55h, spec) * lit * _HighlightColor.a;
                color += _HighlightColor.rgb * spec;

                half rim = pow(1.0h - saturate(dot(n, v)), _RimPower);
                rim = smoothstep(0.45h, 0.6h, rim) * _RimColor.a * (0.35h + 0.65h * lit);
                color += _RimColor.rgb * rim;

                // Sky light from above, warm bounce from the ground below: colour and depth for free.
                half hemi = n.y * 0.5h + 0.5h;
                half3 hemiTint = lerp(_GroundTint.rgb, _SkyTint.rgb, hemi);
                color *= lerp(half3(1, 1, 1), hemiTint, _HemiStrength);

                // Storms dim the whole scene towards a cool ambient tone.
                color = lerp(color, color * _StormAmbient.rgb, _StormDarken);
                color += _Emission.rgb;

                // Extra colour when there is no post-processing grade (Simple quality).
                half luma = dot(color, half3(0.299h, 0.587h, 0.114h));
                color = max(0.0h, lerp(luma.xxx, color, 1.0h + _VibranceBoost));
                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                // No outline requested: move the vertex outside the clip volume so nothing is drawn.
                if (_OutlineWidth <= 0.00001h)
                {
                    o.positionCS = float4(2, 2, 2, 1);
                    return o;
                }
                float4 clip = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 offset = normalize(mul((float3x3)UNITY_MATRIX_VP, normalWS).xy + 1e-5);
                float aspect = _ScreenParams.y / _ScreenParams.x;
                offset.x *= aspect;
                clip.xy += offset * _OutlineWidth * clip.w * 2.0;
                o.positionCS = clip;
                o.fogFactor = ComputeFogFactor(clip.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 color = lerp(_OutlineColor.rgb, _OutlineColor.rgb * _StormAmbient.rgb, _StormDarken);
                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = positionCS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half frag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
}
