Shader "NewMini/Kill Chain Range Overlay"
{
    Properties
    {
        _Color ("Overlay Color", Color) = (0, 0, 0, 0.62)
        _RangeRadius01 ("Range Radius", Range(0, 0.5)) = 0.125
        _EffectStrength ("Effect Strength", Range(0, 1)) = 0
        _InnerAlpha ("Inner Darkness", Range(0, 1)) = 0.035
        _FeatherWidth ("Boundary Feather", Range(0.001, 0.1)) = 0.018
        _EdgeAlpha ("Boundary Darkness", Range(0, 1)) = 0.16
        _EdgeWidth ("Boundary Width", Range(0.001, 0.05)) = 0.008
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _RangeRadius01;
                float _EffectStrength;
                float _InnerAlpha;
                float _FeatherWidth;
                float _EdgeAlpha;
                float _EdgeWidth;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float radialDistance = distance(input.uv, float2(.5, .5));
                float feather = max(_FeatherWidth, .0001);

                // Three readable layers: a lightly shaded playable area, a soft boundary band,
                // and a fully dimmed unreachable area.
                float outsideMask = smoothstep(
                    _RangeRadius01 - feather,
                    _RangeRadius01 + feather,
                    radialDistance);
                float innerDepth = smoothstep(
                    _RangeRadius01 * .2,
                    max(_RangeRadius01 - feather, _RangeRadius01 * .21),
                    radialDistance);
                float edgeBand = 1.0 - smoothstep(
                    _EdgeWidth,
                    _EdgeWidth + feather,
                    abs(radialDistance - _RangeRadius01));

                float innerAlpha = _InnerAlpha * lerp(.35, 1.0, innerDepth);
                float layeredAlpha = lerp(innerAlpha, _Color.a, outsideMask);
                layeredAlpha = saturate(layeredAlpha + edgeBand * _EdgeAlpha);
                return half4(_Color.rgb, layeredAlpha * saturate(_EffectStrength));
            }
            ENDHLSL
        }
    }
}
