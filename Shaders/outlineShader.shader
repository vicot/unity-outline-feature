Shader "VicotSoft/OutlineFeature/outline"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D_X(_MaskTex);
    SAMPLER(sampler_MaskTex);

    TEXTURE2D_X(_MainTex);
    SAMPLER(sampler_MainTex);
    float2 _MainTex_TexelSize;

    float4 _OutlineColor;
    int _SizeId;

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    Varyings VertexSimple(Attributes input)
    {
        Varyings output;

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.uv = GetFullScreenTriangleTexCoord(input.vertexID);

        return output;
    }

    static const int GAUSS_SIZES = 3;
    static const int GAUSS_SAMPLES = 41;
    static const int gauss_startIdx[GAUSS_SIZES] = {-2, -10, -20};
    static const float gauss_samples[GAUSS_SIZES][GAUSS_SAMPLES] = {
        {
            0.192077, 0.203914, 0.208019, 0.203914, 0.192077, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        },
        {
            0.011254, 0.016436, 0.023066, 0.031105, 0.040306, 0.050187, 0.060049, 0.069041, 0.076276, 0.080977, 0.082607, 0.080977, 0.076276, 0.069041, 0.060049, 0.050187,
            0.040306, 0.031105, 0.023066, 0.016436, 0.011254, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        },
        {
            0.005633, 0.006845, 0.008235, 0.009808, 0.011566, 0.013504, 0.015609, 0.017863, 0.020239, 0.022704, 0.025215, 0.027726, 0.030183, 0.032532, 0.034715, 0.036676,
            0.038363, 0.039728, 0.040733, 0.041348, 0.041555, 0.041348, 0.040733, 0.039728, 0.038363, 0.036676, 0.034715, 0.032532, 0.030183, 0.027726, 0.025215, 0.022704,
            0.020239, 0.017863, 0.015609, 0.013504, 0.011566, 0.009808, 0.008235, 0.006845, 0.005633
        }
    };


    float4 gaussH(Varyings i) : SV_TARGET
    {
        float2 res = _MainTex_TexelSize.xy;
        float4 sum = 0;

        [unroll(GAUSS_SAMPLES)] for (float y = 0; y < GAUSS_SAMPLES; y++)
        {
            float2 offset = float2(0, gauss_startIdx[_SizeId] + y);
            sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + offset * res) * gauss_samples[_SizeId][y];
        }
        return sum;
    }

    float4 gaussV(Varyings i) : SV_TARGET
    {
        if (SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv).a > 0)
            discard;

        float2 res = _MainTex_TexelSize.xy;
        float4 sum = 0;

        [unroll(GAUSS_SAMPLES)] for (float x = 0; x < GAUSS_SAMPLES; x++)
        {
            float2 offset = float2(gauss_startIdx[_SizeId] + x, 0);
            sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + offset * res) * gauss_samples[_SizeId][x];
        }

        return float4(_OutlineColor.rgb, saturate(sum.a));
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Lighting Off

        Pass
        {
            Name "HPass"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma shader_feature_local _USE_DRAWMESH
            #pragma vertex VertexSimple
            #pragma fragment gaussH
            ENDHLSL
        }

        Pass
        {
            Name "VPassBlend"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma shader_feature_local _USE_DRAWMESH
            #pragma vertex VertexSimple
            #pragma fragment gaussV
            ENDHLSL
        }
    }
}