Shader "Hidden/PranaTactics/OutlineMask"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D_X_FLOAT(_OutlineSceneDepth);
            float4 _OutlineSize;
            int _OutlineId;
            int _OutlineMode;
            float _OutlineWidth;
            float4 _OutlineColor;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            uint2 Frag(Varyings input) : SV_Target
            {
                float sceneDepth = SAMPLE_TEXTURE2D_X_LOD(_OutlineSceneDepth, sampler_PointClamp,
                    input.positionCS.xy / _OutlineSize.xy, 0).r;
                #if UNITY_REVERSED_Z
                bool visible = input.positionCS.z >= sceneDepth - 0.00001;
                #else
                bool visible = input.positionCS.z <= sceneDepth + 0.00001;
                #endif
                if ((_OutlineMode == 1 && !visible) || ((_OutlineMode == 2 || _OutlineMode == 4) && visible))
                    discard;
                uint fill = ((_OutlineMode == 3 || _OutlineMode == 4) && !visible) ? 1u : 0u;
                uint metadata = (uint)_OutlineId | ((uint)round(clamp(_OutlineWidth, 0, 40) * 4) << 16) | (fill << 24);
                uint4 color = (uint4)round(saturate(_OutlineColor) * 255);
                return uint2(metadata, color.r | (color.g << 8) | (color.b << 16) | (color.a << 24));
            }
            ENDHLSL
        }
    }
}
