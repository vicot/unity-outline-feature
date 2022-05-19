Shader "VicotSoft/OutlineFeature/mask"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

    TEXTURE2D_X(_MaskTex);
    SAMPLER(sampler_MaskTex);

    TEXTURE2D_X(_MainTex);
    SAMPLER(sampler_MainTex);

    TEXTURE2D_X(_HiddenMaskTex);
    SAMPLER(sampler_HiddenMaskTex);

    TEXTURE2D_X(_HiddenTex);
    SAMPLER(sampler_HiddenTex);

    half _Cutoff;

    struct SimpleAttributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    Varyings VertexSimple(SimpleAttributes input)
    {
        Varyings output;

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.uv = GetFullScreenTriangleTexCoord(input.vertexID);

        return output;
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
        ZTest LEqual
        Lighting Off

        Pass
        {
            Name "Opaque"

            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex FullscreenVert
            #pragma fragment frag

            half4 frag(Varyings input) : SV_Target
            {
                return 1;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Transparent"

            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex FullscreenVert
            #pragma fragment frag

            half4 frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(c.a - _Cutoff);
                return 1;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Simple transparent"
            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex VertexSimple
            #pragma fragment frag

            float4 frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (c.a - _Cutoff <= 0) discard;
                return 1;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Get Hidden"
            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex VertexSimple
            #pragma fragment frag

            float4 frag(Varyings input) : SV_Target
            {
                half4 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 hidd = SAMPLE_TEXTURE2D(_HiddenMaskTex, sampler_HiddenMaskTex, input.uv);
                int m = main.r > 0 ? 1 : 0;
                int h = hidd.r > 0 ? 1 : 0;

                return h ^ m;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Copy with material"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex VertexSimple
            #pragma fragment frag

            float4 frag(Varyings input) : SV_Target
            {
                half4 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 material = SAMPLE_TEXTURE2D(_HiddenTex, sampler_HiddenTex, input.uv);

                return main * material;
            }
            ENDHLSL
        }
    }
}