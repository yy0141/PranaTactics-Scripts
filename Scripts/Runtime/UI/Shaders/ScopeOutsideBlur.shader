Shader "PranaTactics/UI/ScopeOutsideBlur"
{
    Properties
    {
        [PerRendererData]
        _MainTex(
            "Texture",
            2D) = "white" {}

        _BlurStrength(
            "Blur Strength",
            Range(0, 1)) = 0

        _Color(
            "Tint",
            Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always

        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;

            float4 _MainTex_TexelSize;

            fixed4 _Color;

            float _BlurStrength;

            v2f vert(
                appdata_t input)
            {
                v2f output;

                output.vertex =
                    UnityObjectToClipPos(
                        input.vertex);

                output.texcoord =
                    input.texcoord;

                output.color =
                    input.color *
                    _Color;

                return output;
            }

            fixed4 frag(
                v2f input) : SV_Target
            {
                float radius =
                    saturate(
                        _BlurStrength) *
                    8.0;

                float2 offsetX =
                    float2(
                        _MainTex_TexelSize.x *
                        radius,
                        0.0);

                float2 offsetY =
                    float2(
                        0.0,
                        _MainTex_TexelSize.y *
                        radius);

                float2 offsetXY =
                    float2(
                        _MainTex_TexelSize.x *
                        radius,
                        _MainTex_TexelSize.y *
                        radius);

                fixed4 color =
                    tex2D(
                        _MainTex,
                        input.texcoord) *
                    0.20;

                color +=
                    tex2D(
                        _MainTex,
                        input.texcoord +
                        offsetX) *
                    0.10;

                color +=
                    tex2D(
                        _MainTex,
                        input.texcoord -
                        offsetX) *
                    0.10;

                color +=
                    tex2D(
                        _MainTex,
                        input.texcoord +
                        offsetY) *
                    0.10;

                color +=
                    tex2D(
                        _MainTex,
                        input.texcoord -
                        offsetY) *
                    0.10;

                color +=
                    tex2D(
                        _MainTex,
                        input.texcoord +
                        offsetXY) *
                    0.10;

                color +=
                    tex2D(
                        _MainTex,
                        input.texcoord -
                        offsetXY) *
                    0.10;

                color +=
                    tex2D(
                        _MainTex,
                        input.texcoord +
                        float2(
                            offsetXY.x,
                            -offsetXY.y)) *
                    0.10;

                color +=
                    tex2D(
                        _MainTex,
                        input.texcoord +
                        float2(
                            -offsetXY.x,
                            offsetXY.y)) *
                    0.10;

                color *=
                    input.color;

                return color;
            }

            ENDCG
        }
    }
}