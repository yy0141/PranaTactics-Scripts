Shader "Hidden/PranaTactics/OutlineComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always
        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        Texture2D<uint2> _OutlineMask;
        Texture2D<uint2> _OutlineSeeds;
        float4 _OutlineSize;
        int _OutlineJump;
        struct Varyings { float4 positionCS : SV_POSITION; };
        Varyings Vert(uint vertexID : SV_VertexID)
        {
            Varyings output;
            output.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
            return output;
        }
        bool IsInside(int2 pixel)
        {
            return all(pixel >= 0) && all(pixel < (int2)_OutlineSize.xy);
        }
        uint2 ReadMask(int2 pixel)
        {
            return IsInside(pixel) ? _OutlineMask.Load(int3(pixel, 0)) : uint2(0, 0);
        }
        uint Pack(uint2 value) { return value.x | (value.y << 16); }
        uint2 Unpack(uint value) { return uint2(value & 65535u, value >> 16); }
        float DistanceToSeed(int2 pixel, uint2 seed)
        {
            float2 boundary = ((float2)Unpack(seed.x) - 1) * 0.5;
            return length(((float2)pixel + 0.5 - boundary) / _OutlineSize.zw);
        }
        float4 DecodeColor(uint value)
        {
            return float4(value & 255u, (value >> 8) & 255u, (value >> 16) & 255u, value >> 24) / 255.0;
        }
        uint2 Seed(Varyings input) : SV_Target
        {
            int2 pixel = (int2)input.positionCS.xy;
            uint2 center = ReadMask(pixel);
            const int2 directions[4] = { int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1) };
            [unroll] for (int i = 0; i < 4; i++)
            {
                int2 neighbor = pixel + directions[i];
                uint2 other = ReadMask(neighbor);
                if ((center.x & 65535u) != (other.x & 65535u))
                {
                    int2 source = (center.x & 65535u) != 0 ? pixel : neighbor;
                    return uint2(Pack((uint2)(pixel * 2 + 2 + directions[i])), Pack((uint2)source));
                }
            }
            return uint2(0, 0);
        }
        uint2 Jump(Varyings input) : SV_Target
        {
            int2 pixel = (int2)input.positionCS.xy;
            uint2 nearest = uint2(0, 0);
            float nearestDistance = 1e10;
            [unroll] for (int y = -1; y <= 1; y++)
            {
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    int2 source = pixel + int2(x, y) * _OutlineJump;
                    if (!IsInside(source)) continue;
                    uint2 candidate = _OutlineSeeds.Load(int3(source, 0));
                    if (candidate.x == 0) continue;
                    float distance = DistanceToSeed(pixel, candidate);
                    if (distance < nearestDistance)
                    {
                        nearest = candidate;
                        nearestDistance = distance;
                    }
                }
            }
            return nearest;
        }
        float4 Composite(Varyings input) : SV_Target
        {
            int2 pixel = (int2)input.positionCS.xy;
            uint2 mask = ReadMask(pixel);
            uint2 seed = _OutlineSeeds.Load(int3(pixel, 0));
            float4 result = (mask.x & (1u << 24)) != 0 ? DecodeColor(mask.y) : float4(0, 0, 0, 0);
            if (seed.x != 0)
            {
                uint2 source = ReadMask((int2)Unpack(seed.y));
                float width = ((source.x >> 16) & 255u) * 0.25;
                float4 color = DecodeColor(source.y);
                color.a *= width > 0 ? saturate(width * 0.5 + 0.5 - DistanceToSeed(pixel, seed)) : 0;
                if (color.a > result.a) result = color;
            }
            return result;
        }
        ENDHLSL
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Seed
            ENDHLSL
        }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Jump
            ENDHLSL
        }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
    }
}
