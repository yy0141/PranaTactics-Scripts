Shader "Hidden/PranaTactics/UnitEliminationTexture"
{
    Properties
    {
        _BodyColor ("Body Color", Color) = (0.003,0.002,0.008,1)
        _RimColor ("Rim Color", Color) = (0.03,0.014,0.065,1)
        _LineRootColor ("Line Root Color", Color) = (0.03,0.014,0.065,1)
        _LineTipColor ("Line Tip Color", Color) = (0.018,0.008,0.04,0)
        _CanvasSize ("Canvas Size", Float) = 800
        _Sphere ("Sphere", Vector) = (90,0.12,1,1)
        _Line ("Line", Vector) = (360,3,12,1)
        _Motion ("Motion", Vector) = (0,12,18,1)
    }
    SubShader
    {
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float4 _BodyColor, _RimColor, _LineRootColor, _LineTipColor;
            float4 _Sphere, _Line, _Motion;
            float _CanvasSize;
            float Noise(float knot, float tick, float seed)
            {
                return frac(sin(dot(float3(knot, tick, seed), float3(127.1,311.7,74.7))) * 43758.5453) * 2 - 1;
            }
            float Motion(float knot, float seed)
            {
                float time = _Motion.x * _Motion.y;
                float tick = floor(time);
                float blend = smoothstep(0,1,frac(time));
                return lerp(Noise(knot,tick,seed),Noise(knot,tick+1,seed),blend);
            }
            float Offset(float knot, float seed)
            {
                return Motion(knot,seed) * _Line.z * saturate(knot / max(1,_Motion.z) * 4);
            }
            half4 Frag(v2f_img input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * _CanvasSize;
                float aa = max(fwidth(p.x), 0.001);
                float sphereMask = _Sphere.x > 0 ? 1-smoothstep(_Sphere.x-aa,_Sphere.x+aa,length(p)) : 0;
                float rim = smoothstep(_Sphere.x*(1-_Sphere.y)-aa,_Sphere.x*(1-_Sphere.y)+aa,length(p));
                float4 sphereColor = lerp(_BodyColor,float4(_RimColor.rgb*_Sphere.z,_RimColor.a),rim);
                float along = abs(p.y) / max(_Line.x,0.001);
                float vertex = min(along,1) * _Motion.z;
                float knot = floor(vertex);
                float seed = _Motion.w + (p.y < 0 ? 971 : 0);
                float offset = lerp(Offset(knot,seed),Offset(knot+1,seed),frac(vertex));
                float width = _Line.y * (0.8+Motion(knot,seed+521)*0.45) * pow(saturate(1-along),0.35);
                float lineMask = (1-smoothstep(width*0.5-aa,width*0.5+aa,abs(p.x-offset)))
                    * (1-smoothstep(1-aa/max(_Line.x,0.001),1,along));
                lineMask *= _Line.x > 0 && _Line.y > 0 ? 1 : 0;
                float4 lineColor = lerp(_LineRootColor,_LineTipColor,saturate(along));
                float alpha = lerp(lineMask*lineColor.a,sphereColor.a,sphereMask)*_Sphere.w;
                float3 color = lerp(lineColor.rgb*_Line.w,sphereColor.rgb,sphereMask);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
