Shader "Game/Effects/Blood"
{
    Properties
    {
        _BaseColor("Color", Color) = (0.23, 0.006, 0.012, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; half fog : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _BaseColor;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float2 cell = frac(floor(input.uv * 48.0) * float2(0.1031, 0.11369));
                cell += dot(cell, cell.yx + 19.19);
                float noise = frac(cell.x * cell.y);
                float radius = length(p * float2(1.05, 1.2));
                float edge = radius + (noise - 0.5) * 0.13 + p.x * p.y * 0.22;
                half opacity = 1.0 - smoothstep(0.58, 0.68, edge);
                opacity = max(opacity, 1.0 - smoothstep(0.06, 0.10, length(p - float2(0.68, 0.36))));
                opacity = max(opacity, 1.0 - smoothstep(0.035, 0.07, length(p - float2(-0.55, -0.62))));
                opacity = max(opacity, 1.0 - smoothstep(0.04, 0.075, length(p - float2(-0.76, 0.1))));
                half shade = lerp(0.6h, 1.0h, saturate((0.68 - radius) * 4.0));
                half3 color = MixFog(input.color.rgb * shade, input.fog);
                return half4(color, input.color.a * opacity);
            }
            ENDHLSL
        }
    }
}
