// STORM ENGINE - cartoon glass: transparent body, bright fresnel edge and a painted highlight.
Shader "StormEngine/Glass"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.72, 0.9, 1, 0.14)
        _EdgeColor ("Edge Color", Color) = (0.85, 0.97, 1, 0.85)
        _EdgePower ("Edge Power", Range(0.5, 8)) = 2.6
        _HighlightColor ("Highlight", Color) = (1, 1, 1, 0.6)
        _HighlightDir ("Highlight Direction", Vector) = (-0.5, 0.6, -0.6, 0)
        _HighlightSize ("Highlight Size", Range(0.8, 0.999)) = 0.986
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "GlassForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _EdgeColor;
                half _EdgePower;
                half4 _HighlightColor;
                float4 _HighlightDir;
                half _HighlightSize;
            CBUFFER_END

            half _StormDarken;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fres = pow(1.0h - saturate(dot(n, v)), _EdgePower);
                half3 color = lerp(_Tint.rgb, _EdgeColor.rgb, fres);
                half alpha = saturate(_Tint.a + fres * _EdgeColor.a);

                // Cartoon shine: a crisp main dot and a smaller one beside it.
                float3 hd = normalize(_HighlightDir.xyz);
                half spot = smoothstep(_HighlightSize, _HighlightSize + 0.006h, dot(n, hd));
                float3 hd2 = normalize(hd + float3(0.2, -0.18, 0.0));
                spot = max(spot, smoothstep(_HighlightSize + 0.013h, _HighlightSize + 0.017h, dot(n, hd2)));
                color = lerp(color, _HighlightColor.rgb, spot * _HighlightColor.a);
                alpha = max(alpha, spot * _HighlightColor.a);

                color *= 1.0h - _StormDarken * 0.35h;
                color = MixFog(color, i.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
