Shader "Game/Effects/Unlit"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        [HDR] _BaseColor("Color", Color) = (1,1,1,1)
        [Enum(Texture,0,SoftLine,1,SoftCircle,2)] _Shape("Shape", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Shape;
                half _SrcBlend;
                half _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half fog : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _BaseColor;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.color;
                if (_Shape > 1.5h)
                {
                    half2 offset = input.uv * 2.0h - 1.0h;
                    half opacity = saturate(1.0h - dot(offset, offset));
                    color.a *= opacity * opacity;
                }
                else if (_Shape > 0.5h)
                    color.a *= saturate(1.0h - abs(input.uv.y * 2.0h - 1.0h));
                half3 fogColor = _DstBlend < 1.5h ? half3(0,0,0) : unity_FogColor.rgb;
                color.rgb = MixFogColor(color.rgb, fogColor, input.fog);
                return color;
            }
            ENDHLSL
        }
    }
}
