Shader "URP/AdvancedWater"
{
    Properties
    {
        [Header(Colors)]
        _ColorShallow("Shallow Color", Color) = (0.0, 0.5, 0.8, 1.0)
        _ColorDeep("Deep Color", Color) = (0.0, 0.1, 0.3, 1.0)
        _DepthMultiplier("Depth Multiplier", Float) = 1.0
        
        [Header(Waves)]
        _WaveSpeed("Wave Speed", Float) = 1.0
        _WaveHeight("Wave Height", Float) = 0.2
        _WaveFrequency("Wave Frequency", Float) = 0.5
        _WaveSteepness("Wave Steepness", Float) = 0.5
        
        [Header(Normals)]
        _NormalMap1("Normal Map 1", 2D) = "bump" {}
        _NormalMap2("Normal Map 2", 2D) = "bump" {}
        _NormalSpeed("Normal Speed", Float) = 0.05
        _NormalScale("Normal Scale", Float) = 1.0
        
        [Header(Reflection)]
        _ReflectionStrength("Reflection Strength", Range(0, 1)) = 0.5
        _Smoothness("Smoothness", Range(0, 1)) = 0.9
        
        [Header(Foam)]
        _FoamColor("Foam Color", Color) = (1,1,1,1)
        _FoamSpeed("Foam Speed", Float) = 0.5
        _FoamIntensity("Foam Intensity", Float) = 1.0
        
        [Header(Edge Fade)]
        _EdgeFade("Edge Fade", Float) = 1.0
    }
    
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
            };
            
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            
            CBUFFER_START(UnityPerMaterial)
            float4 _ColorShallow;
            float4 _ColorDeep;
            float _WaveSpeed;
            float _WaveHeight;
            float _WaveFrequency;
            float _Smoothness;
            float _NormalStrength;
            float4 _NormalMap_ST;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Простая синусоидальная анимация волн
                float wave = sin((input.positionOS.x + input.positionOS.z) * _WaveFrequency + _Time.y * _WaveSpeed);
                input.positionOS.y += wave * _WaveHeight;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = normalInput.tangentWS;
                output.bitangentWS = normalInput.bitangentWS;
                output.uv = TRANSFORM_TEX(input.uv, _NormalMap);
                output.shadowCoord = GetShadowCoord(vertexInput);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Нормали из нормал мапы
                float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                normalTS.xy *= _NormalStrength;
                normalTS = normalize(normalTS);
                
                float3x3 TBN = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 normalWS = normalize(mul(normalTS, TBN));
                
                // Основанное на глубине/высоте смешивание цвета
                float depth = saturate(input.positionWS.y * 0.5 + 0.5);
                half4 waterColor = lerp(_ColorDeep, _ColorShallow, depth);
                
                // Освещение
                Light mainLight = GetMainLight(input.shadowCoord);
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * NdotL + SampleSH(normalWS);
                
                half4 color = half4(waterColor.rgb * lighting, waterColor.a);
                
                return color;
            }
            ENDHLSL
        }
    }
}