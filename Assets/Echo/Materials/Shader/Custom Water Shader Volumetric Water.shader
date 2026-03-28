Shader "URP/VolumetricWater"
{
    Properties
    {
        [Header(Surface)]
        _SurfaceColor("Surface Color", Color) = (0.0, 0.5, 0.8, 0.8)
        _DepthColor("Depth Color", Color) = (0.0, 0.1, 0.3, 1.0)
        
        [Header(Volume)]
        _VolumeColor("Volume Color", Color) = (0.1, 0.3, 0.5, 0.6)
        _VolumeDensity("Volume Density", Float) = 0.1
        _VolumeDepth("Volume Depth", Float) = 5.0
        
        [Header(Caustics)]
        _CausticsTexture("Caustics Texture", 2D) = "white" {}
        _CausticsSpeed("Caustics Speed", Float) = 0.5
        _CausticsScale("Caustics Scale", Float) = 1.0
        
        [Header(Fog Inside)]
        _WaterFogColor("Water Fog Color", Color) = (0.1, 0.2, 0.3, 1.0)
        _WaterFogDensity("Water Fog Density", Float) = 0.05
        
        [Header(Foam)]
        _FoamColor("Foam Color", Color) = (1,1,1,1)
        _FoamDepth("Foam Depth", Float) = 0.2
        _FoamMinDepth("Foam Min Depth", Float) = 0.1
        _FoamSpeed("Foam Speed", Float) = 0.5
        _FoamNoise("Foam Noise", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }
        
        // Первый проход: Внешняя поверхность
        Pass
        {
            Name "Surface"
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vertSurface
            #pragma fragment fragSurface
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float screenDepth : TEXCOORD4;
            };
            
            TEXTURE2D(_CausticsTexture);
            TEXTURE2D(_FoamNoise);
            SAMPLER(sampler_CausticsTexture);
            SAMPLER(sampler_FoamNoise);
            
            CBUFFER_START(UnityPerMaterial)
            float4 _SurfaceColor;
            float4 _DepthColor;
            float4 _WaterFogColor;
            float4 _FoamColor;
            float _CausticsSpeed;
            float _CausticsScale;
            float _WaterFogDensity;
            float _FoamDepth;
            float _FoamSpeed;
            float4 _FoamNoise_ST;
            CBUFFER_END
            
            Varyings vertSurface(Attributes input)
            {
                Varyings output;
                
                // Простая волновая анимация
                float wave = sin(input.positionOS.x * 0.5 + _Time.y * 0.5) * 0.1;
                wave += sin(input.positionOS.z * 0.3 + _Time.y * 0.3) * 0.05;
                input.positionOS.y += wave;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = input.uv;
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.screenDepth = vertexInput.positionCS.z;
                
                return output;
            }
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            half4 fragSurface(Varyings input) : SV_Target
            {
                // Глубина для пены
                float2 screenUV = input.positionCS.xy / _ScreenParams.xy;
                float sceneDepth = SampleSceneDepth(screenUV);
                sceneDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
                float waterDepth = LinearEyeDepth(input.screenDepth, _ZBufferParams);
                float depthDifference = sceneDepth - waterDepth;
                
                // Пена на мелководье
                float foam = 1 - saturate(depthDifference / _FoamDepth);
                foam *= step(0.1, depthDifference); // Убираем пену на глубоких местах
                
                // Добавляем шум пены
                float foamTime = _Time.y * _FoamSpeed;
                float2 foamUV = input.positionWS.xz * 0.1 + float2(foamTime * 0.2, foamTime * 0.1);
                float foamNoise = SAMPLE_TEXTURE2D(_FoamNoise, sampler_FoamNoise, foamUV).r;
                foam *= foamNoise;
                
                // Цвет воды с пеной
                half4 waterColor = lerp(_SurfaceColor, _DepthColor, foam * 0.5);
                half4 finalColor = lerp(waterColor, _FoamColor, foam);
                
                // Освещение
                Light mainLight = GetMainLight(input.shadowCoord);
                float NdotL = saturate(dot(normalize(input.normalWS), mainLight.direction));
                half3 lighting = mainLight.color * NdotL + SampleSH(input.normalWS);
                
                // Подводные каустики
                float2 causticsUV = input.positionWS.xz * _CausticsScale;
                causticsUV += _Time.y * _CausticsSpeed;
                float caustics = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, causticsUV).r;
                
                finalColor.rgb *= lighting;
                finalColor.rgb *= (1.0 + caustics * 0.3 * (1 - foam));
                
                return finalColor;
            }
            ENDHLSL
        }
        
        // Второй проход: Внутренний объем
        Pass
        {
            Name "Volume"
            Cull Front // Рендерим внутреннюю часть
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vertVolume
            #pragma fragment fragVolume
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct AttributesVolume
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct VaryingsVolume
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float depth : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };
            
            TEXTURE2D(_CausticsTexture);
            SAMPLER(sampler_CausticsTexture);
            
            CBUFFER_START(UnityPerMaterial)
            float4 _SurfaceColor;
            float4 _VolumeColor;
            float4 _WaterFogColor;
            float _VolumeDepth;
            float _WaterFogDensity;
            float _CausticsSpeed;
            float _CausticsScale;
            CBUFFER_END
            
            VaryingsVolume vertVolume(AttributesVolume input)
            {
                VaryingsVolume output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = input.uv;
                
                // Глубина внутри воды (расстояние от поверхности)
                // Предполагаем, что поверхность воды на y=0
                output.depth = max(0, -input.positionOS.y);
                
                return output;
            }
            
            half4 fragVolume(VaryingsVolume input) : SV_Target
            {
                // Основанный на глубине цвет
                float depthFactor = saturate(input.depth / _VolumeDepth);
                half4 volumeColor = lerp(_SurfaceColor, _VolumeColor, depthFactor);
                
                // Подводные каустики (блики)
                float2 causticsUV = input.positionWS.xz * _CausticsScale;
                causticsUV += _Time.y * _CausticsSpeed;
                float caustics = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, causticsUV).r;
                
                // Добавляем туман внутри воды
                float fog = exp(-_WaterFogDensity * input.depth);
                volumeColor.rgb = lerp(_WaterFogColor.rgb, volumeColor.rgb, fog);
                volumeColor.rgb *= (1.0 + caustics * 0.3);
                
                // Больше непрозрачности на глубине
                volumeColor.a = lerp(0.3, 0.8, depthFactor);
                
                return volumeColor;
            }
            ENDHLSL
        }
    }
}