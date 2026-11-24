Shader "UTT/BIM XRay Outline Adapter"
{
    Properties
    {
        _BaseColor("Outline", Color) = (1, 0.45, 0.03, 1)
        _Width("Pixels", Range(1, 5)) = 2
        _MeasurementColor("Measurement", Color) = (1, 0.08, 0.45, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+101" }

        // The measurement LineRenderer continues to use this material for its world-space line.
        Pass
        {
            Name "Measurement"
            ZWrite Off ZTest Always Cull Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Width;
                half4 _MeasurementColor;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return _MeasurementColor; }
            ENDHLSL
        }

        Pass
        {
            Name "SilhouetteOutline"
            Tags { "LightMode"="BIMSilhouetteComposite" }
            ZWrite Off ZTest Always Cull Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Width;
                half4 _MeasurementColor;
            CBUFFER_END

            half Mask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half center = Mask(uv);
                if (center >= 0.999) return 0;

                float2 stepUV = _BlitTexture_TexelSize.xy * max(1.0, _Width);
                float2 diagonal = stepUV * 0.707107;
                half edge = Mask(uv + float2(stepUV.x, 0));
                edge = max(edge, Mask(uv - float2(stepUV.x, 0)));
                edge = max(edge, Mask(uv + float2(0, stepUV.y)));
                edge = max(edge, Mask(uv - float2(0, stepUV.y)));
                edge = max(edge, Mask(uv + diagonal));
                edge = max(edge, Mask(uv - diagonal));
                edge = max(edge, Mask(uv + float2(diagonal.x, -diagonal.y)));
                edge = max(edge, Mask(uv + float2(-diagonal.x, diagonal.y)));
                // A second ring keeps the outline connected for thin rail fasteners and contact wires.
                if (_Width > 1.5)
                {
                    edge = max(edge, Mask(uv + float2(stepUV.x, 0) * 0.5));
                    edge = max(edge, Mask(uv - float2(stepUV.x, 0) * 0.5));
                    edge = max(edge, Mask(uv + float2(0, stepUV.y) * 0.5));
                    edge = max(edge, Mask(uv - float2(0, stepUV.y) * 0.5));
                }
                return half4(_BaseColor.rgb, _BaseColor.a * saturate(edge - center));
            }
            ENDHLSL
        }
    }
}
