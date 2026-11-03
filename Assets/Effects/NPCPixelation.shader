Shader "Game/Effects/NPCPixelation"
{
    Properties
    {
        _PixelSize("Pixel Size", Range(4, 64)) = 16
        _FaceSize("Face Size", Vector) = (0.65, 0.7, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _PixelSize;
                float4 _FaceSize;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 centerVS = TransformWorldToView(TransformObjectToWorld(float3(0, 0, 0)));
                if (centerVS.z >= 0.0)
                {
                    output.positionCS = float4(0, 0, 0, -1);
                    return output;
                }
                float depth = -centerVS.z;
                float planeDepth = max(_ProjectionParams.y + 0.01, depth - max(_FaceSize.x, _FaceSize.y) * 0.5);
                centerVS.xy *= lerp(planeDepth / depth, 1.0, unity_OrthoParams.w);
                centerVS.z = -planeDepth;
                float2 projectionScale = abs(float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11));
                // Keep at least two screen-space blocks over distant faces.
                float2 minimumSize = 4.0 * _PixelSize * lerp(planeDepth, 1.0, unity_OrthoParams.w)
                    / (_ScreenParams.xy * projectionScale);
                float2 size = max(_FaceSize.xy, minimumSize);
                centerVS.xy += input.positionOS.xy * size;
                output.positionCS = mul(UNITY_MATRIX_P, float4(centerVS, 1.0));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float size = max(1.0, _PixelSize);
                uv = (floor(uv * _ScreenParams.xy / size) + 0.5) * size / _ScreenParams.xy;
                return half4(SampleSceneColor(saturate(uv)), 1.0);
            }
            ENDHLSL
        }
    }
}
