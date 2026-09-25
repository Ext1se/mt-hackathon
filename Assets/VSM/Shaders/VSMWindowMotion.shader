
Shader "VSM/Opaque Moving Landscape"
{
    Properties
    {
        _SkyColor("Цвет дымки", Color) = (.66,.72,.74,1)
        _TreeColor("Цвет силуэтов", Color) = (.43,.49,.44,1)
        _Speed("Скорость движения", Float) = 5
        _Contrast("Контраст силуэтов", Range(0,1)) = .14
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _SkyColor, _TreeColor;
            float _Speed, _Contrast;
            CBUFFER_END
            float _VSMCurve;
            float _VSMTravelPhase;
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            // Передаёт координаты в каждый глаз без экранных текстур и постобработки.
            Varyings Vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.world = TransformObjectToWorld(input.positionOS.xyz); output.positionCS = TransformWorldToHClip(output.world); return output;
            }
            // Мягкие аналитические силуэты имитируют уже размытый пейзаж: дорогое размытие кадра не требуется.
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float x = input.world.z * .37 + _VSMTravelPhase * (_Speed / 5.0);
                float horizon = 1.24 + _VSMCurve * .04;
                float treeline = horizon + .12 * sin(x * 1.3) + .09 * sin(x * 2.7 + 2);
                float trees = 1 - smoothstep(treeline - .20, treeline + .22, input.world.y);
                float pole = pow(saturate(.5 + .5 * sin(x * .41)), 26) * .28;
                float streak = .5 + .5 * sin(input.world.y * 17 + sin(x * .2));
                half3 color = lerp(_SkyColor.rgb, _TreeColor.rgb, _Contrast * (trees + pole));
                color += (streak - .5) * .012;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
