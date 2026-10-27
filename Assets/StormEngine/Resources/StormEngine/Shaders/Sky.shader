// STORM ENGINE - stylised gradient sky with a soft cartoon sun and optional stars.
Shader "StormEngine/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.25, 0.62, 0.93, 1)
        _HorizonColor ("Horizon", Color) = (0.75, 0.91, 1, 1)
        _BottomColor ("Bottom", Color) = (0.55, 0.78, 0.95, 1)
        _SunColor ("Sun", Color) = (1, 0.95, 0.7, 1)
        _SunDirection ("Sun Direction", Vector) = (-0.4, 0.55, 0.7, 0)
        _SunSize ("Sun Size", Range(0.9, 0.9999)) = 0.9975
        _SunGlow ("Sun Glow", Range(0, 1)) = 0.35
        _Stars ("Stars", Range(0, 1)) = 0
        _Flash ("Lightning Flash", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half4 _SunColor;
                float4 _SunDirection;
                half _SunSize;
                half _SunGlow;
                half _Stars;
                half _Flash;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                half up = d.y;
                half3 color = up > 0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, smoothstep(0.0h, 0.6h, up))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, smoothstep(0.0h, 0.35h, -up));

                float3 sd = normalize(_SunDirection.xyz);
                half sunDot = dot(d, sd);
                half disc = smoothstep(_SunSize, _SunSize + 0.0008h, sunDot);
                half glow = pow(saturate(sunDot), 64.0h) * _SunGlow + pow(saturate(sunDot), 8.0h) * _SunGlow * 0.35h;
                color += _SunColor.rgb * glow;
                color = lerp(color, _SunColor.rgb, disc);

                if (_Stars > 0.001h)
                {
                    float3 cell = floor(d * 180.0);
                    half star = step(0.9965h, hash(cell)) * smoothstep(0.05h, 0.3h, up);
                    color += star * _Stars;
                }

                color = lerp(color, half3(1, 1, 0.95h), _Flash * 0.6h);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
