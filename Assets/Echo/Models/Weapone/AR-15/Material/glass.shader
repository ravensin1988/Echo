Shader "Custom/Glass"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,0.5)
        _Smoothness ("Smoothness", Range(0,1)) = 0.95
        _Metallic ("Metallic", Range(0,1)) = 0
        _IOR ("Index of Refraction", Float) = 1.52
        _Thickness ("Thickness", Float) = 0.1
        _Distortion ("Distortion", Range(0,1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Glass"
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            // Основные библиотеки URP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl" // Для работы с буфером сцены

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                float3 viewDirWS    : TEXCOORD1;
                float2 uv           : TEXCOORD2;
            };

            // Параметры материала (автоматически подхватываются из Properties)
            half4 _Color;
            float _Smoothness;
            float _Metallic;
            float _IOR;
            float _Thickness;
            float _Distortion;

            // Текстура кадра камеры (объявляем через TEXTURE2D_X + SAMPLER)
            TEXTURE2D_X(_CameraFrameTexture);
            SAMPLER(sampler_CameraFrameTexture);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = positionInputs.positionCS;
                OUT.normalWS = normalInputs.normalWS;
                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                OUT.uv = IN.uv;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // Преломление (закон Снеллиуса)
                float3 refractDir = refract(V, N, 1.0 / _IOR);
                float3 refractPos = IN.positionCS.xyz + refractDir * _Thickness;

                // Конвертируем в экранные UV
                float2 screenUV = refractPos.xy / _ScreenParams.xy;
                screenUV = screenUV * 0.5 + 0.5; // Нормализация в [0,1]
                screenUV += N.xy * _Distortion; // Искажение по нормали

                // Читаем фон через TEXTURE2D_X и SAMPLER
                half4 background = SAMPLE_TEXTURE2D_X(_CameraFrameTexture, sampler_CameraFrameTexture, screenUV);

                // Упрощённое освещение (можно заменить на PBR при необходимости)
                half3 color = _Color.rgb * (1 - _Metallic) + _Metallic * 0.8;
                color *= 1 - dot(N, V) * 0.3; // Френелевское затемнение

                // Смешивание: фон + цвет материала
                half4 finalColor = half4(color, _Color.a);
                finalColor.rgb = lerp(background.rgb, finalColor.rgb, _Color.a);

                return finalColor;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
