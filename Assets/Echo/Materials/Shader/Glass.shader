Shader "Custom/URP/AdvancedGlass"
{
    Properties
    {
        [Header(Base)]
        _BaseColor ("Base Tint (RGB) Alpha (A)", Color) = (1,1,1,0.25)

        [Header(Refraction  Roughness)]
        _RefractionStrength ("Refraction Strength", Range(0, 0.2)) = 0.04
        _Roughness ("Roughness", Range(0, 1)) = 0.2
        _BlurRadius ("Roughness Blur Radius (px)", Range(0, 8)) = 2

        [Header(Normal)]
        _NormalMap ("Base Normal Map", 2D) = "bump" {}
        _NormalScale ("Base Normal Scale", Range(0, 2)) = 1

        [Header(Fogging  Condensation)]
        _FogAmount ("Fog Amount", Range(0, 1)) = 0
        _FogColor ("Fog Color", Color) = (0.9,0.95,1,1)
        _FogNoiseTex ("Fog Noise", 2D) = "white" {}
        _FogScale ("Fog Scale", Range(0.1, 20)) = 4
        _FogSoftness ("Fog Softness", Range(0.01, 0.5)) = 0.15
        _FogNoiseSpeed ("Fog Noise Speed", Range(0, 1)) = 0.05

        [Header(Drops)]
        _DropsNormalMap ("Drops Normal Map", 2D) = "bump" {}
        _DropsMask ("Drops Mask", 2D) = "white" {}
        _DropsAmount ("Drops Amount", Range(0, 1)) = 0
        _DropsStrength ("Drops Distortion Strength", Range(0, 2)) = 0.7
        _DropsTiling ("Drops Tiling", Range(0.5, 20)) = 6
        _DropsSpeed ("Drops Fall Speed", Range(0, 2)) = 0.25

        [Header(Fresnel)]
        _FresnelStrength ("Fresnel Strength", Range(0, 2)) = 0.4
        _FresnelPower ("Fresnel Power", Range(1, 10)) = 5
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 tangentWS  : TEXCOORD2;
                float3 bitangentWS: TEXCOORD3;
                float2 uv         : TEXCOORD4;
                float4 screenPos  : TEXCOORD5;
            };

            TEXTURE2D(_NormalMap);       SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FogNoiseTex);     SAMPLER(sampler_FogNoiseTex);
            TEXTURE2D(_DropsNormalMap);  SAMPLER(sampler_DropsNormalMap);
            TEXTURE2D(_DropsMask);       SAMPLER(sampler_DropsMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;

                float _RefractionStrength;
                float _Roughness;
                float _BlurRadius;

                float _NormalScale;

                float _FogAmount;
                float4 _FogColor;
                float _FogScale;
                float _FogSoftness;
                float _FogNoiseSpeed;

                float _DropsAmount;
                float _DropsStrength;
                float _DropsTiling;
                float _DropsSpeed;

                float _FresnelStrength;
                float _FresnelPower;

                float4 _NormalMap_ST;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = nrm.normalWS;
                OUT.tangentWS = nrm.tangentWS;
                OUT.bitangentWS = nrm.bitangentWS;
                OUT.uv = IN.uv;
                OUT.screenPos = ComputeScreenPos(pos.positionCS);

                return OUT;
            }

            float3 SampleBlurredScene(float2 uv, float roughness, float radiusPx)
            {
                float2 px = 1.0 / _ScreenParams.xy;
                float r = roughness * radiusPx;

                float2 u0 = saturate(uv);
                float2 u1 = saturate(uv + float2( r, 0) * px);
                float2 u2 = saturate(uv + float2(-r, 0) * px);
                float2 u3 = saturate(uv + float2(0,  r) * px);
                float2 u4 = saturate(uv + float2(0, -r) * px);

                float3 c = 0;
                c += SampleSceneColor(u0);
                c += SampleSceneColor(u1);
                c += SampleSceneColor(u2);
                c += SampleSceneColor(u3);
                c += SampleSceneColor(u4);

                return c * 0.2;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 N0 = normalize(IN.normalWS);
                float3 V = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));

                // Base normal
                float2 uvBase = TRANSFORM_TEX(IN.uv, _NormalMap);
                float3 baseN_TS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvBase), _NormalScale);

                // Drops normals + mask (2 layers для меньшей повторяемости)
                float t = _Time.y;
                float2 dUV1 = IN.uv * _DropsTiling + float2(0, -t * _DropsSpeed);
                float2 dUV2 = IN.uv * (_DropsTiling * 1.73) + float2(0.37, -t * (_DropsSpeed * 0.63));

                float3 dropN1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_DropsNormalMap, sampler_DropsNormalMap, dUV1), _DropsStrength);
                float3 dropN2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_DropsNormalMap, sampler_DropsNormalMap, dUV2), _DropsStrength * 0.7);

                float m1 = SAMPLE_TEXTURE2D(_DropsMask, sampler_DropsMask, dUV1).r;
                float m2 = SAMPLE_TEXTURE2D(_DropsMask, sampler_DropsMask, dUV2).r;
                float dropMask = saturate((m1 + m2) * 0.5 * _DropsAmount);

                float3 dropN_TS = normalize(lerp(float3(0,0,1), normalize(dropN1 + dropN2), dropMask));

                // Combined normal in tangent space
                float3 N_TS = normalize(float3(baseN_TS.xy + dropN_TS.xy, baseN_TS.z * dropN_TS.z));

                float3x3 TBN = float3x3(normalize(IN.tangentWS), normalize(IN.bitangentWS), N0);
                float3 N = normalize(mul(N_TS, TBN));

                // Screen UV
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                screenUV = UnityStereoTransformScreenSpaceTex(screenUV);

                // Refraction offset
                float3 Nvs = TransformWorldToViewDir(N, true);
                float2 refrOffset = Nvs.xy * _RefractionStrength;

                float2 refrUV = screenUV + refrOffset;

                // Roughness blur
                float3 sceneCol = SampleBlurredScene(refrUV, _Roughness, _BlurRadius);

                // Fogging
                float2 fogUV = IN.uv * _FogScale + float2(0.0, t * _FogNoiseSpeed);
                float fogNoise = SAMPLE_TEXTURE2D(_FogNoiseTex, sampler_FogNoiseTex, fogUV).r;
                float fogPattern = smoothstep(0.5 - _FogSoftness, 0.5 + _FogSoftness, fogNoise);
                float fogMask = saturate(fogPattern * _FogAmount);

                sceneCol = lerp(sceneCol, _FogColor.rgb, fogMask);

                // Tint + Fresnel
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower) * _FresnelStrength;
                float3 finalCol = sceneCol * _BaseColor.rgb + fresnel.xxx;

                float alpha = saturate(_BaseColor.a + fogMask * 0.35 + dropMask * 0.15);

                return half4(finalCol, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}