// STORM ENGINE - "blueprint" hologram for machines you have unlocked but not built yet.
Shader "StormEngine/Ghost"
{
    Properties
    {
        _Color ("Color", Color) = (0.45, 0.8, 1, 0.35)
        _EdgeColor ("Edge", Color) = (0.8, 0.95, 1, 0.9)
        _LineDensity ("Scan Lines", Float) = 18
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Ghost"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EdgeColor;
                float _LineDensity;
            CBUFFER_END

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
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fres = pow(1.0h - saturate(dot(n, v)), 2.0h);
                half scan = 0.75h + 0.25h * sin(i.positionWS.y * _LineDensity * 6.2831h - _Time.y * 3.0h);
                half3 color = lerp(_Color.rgb, _EdgeColor.rgb, fres);
                half alpha = saturate((_Color.a + fres * _EdgeColor.a) * scan);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
