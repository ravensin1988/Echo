Shader "Echo/GlassVoronoi"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 0.1)
        _Roughness ("Roughness", Range(0, 1)) = 0.1
        _RefractionStrength ("Refraction Strength", Range(0, 1)) = 0.5
        _VoronoiScale ("Voronoi Scale", Range(0.1, 10)) = 3.0
        _VoronoiSpeed ("Voronoi Speed", Range(0, 2)) = 0.5
        _DistortionAmount ("Distortion Amount", Range(0, 1)) = 0.3
        
        [Header(Normal Map Settings)]
        _NormalStrength ("Normal Strength", Range(0, 1)) = 0.5
        _NormalScale ("Normal Scale", Range(0.1, 10)) = 2.0
        
        [Header(Advanced)]
        _FresnelPower ("Fresnel Power", Range(1, 10)) = 3.0
        _FresnelBias ("Fresnel Bias", Range(0, 1)) = 0.1
        
        [Header(Background)]
        _BackgroundTex ("Background Texture", 2D) = "black" {}
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }
        
        Pass
        {
            Name "GlassPass"
            Tags { "LightMode" = "UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            TEXTURE2D(_BackgroundTex);
            SAMPLER(sampler_BackgroundTex);
            float4 _BackgroundTex_TexelSize;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 tangent : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldViewDir : TEXCOORD2;
                float3 worldPosition : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Roughness;
                float _RefractionStrength;
                float _VoronoiScale;
                float _VoronoiSpeed;
                float _DistortionAmount;
                float _NormalStrength;
                float _NormalScale;
                float _FresnelPower;
                float _FresnelBias;
            CBUFFER_END
            
            // Hash functions for Voronoi
            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }
            
            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }
            
            // Voronoi noise - returns (distance to nearest point, F2 - F1 distance)
            float2 Voronoi(float2 uv, float time, out float2 cellId)
            {
                float2 gv = floor(uv);
                float2 fv = frac(uv);
                
                float minDist = 100.0;
                float minDist2 = 100.0;
                cellId = float2(0, 0);
                
                // Check 3x3 neighborhood
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 cell = gv + offset;
                        
                        // Generate random point in cell
                        float2 random = Hash22(cell);
                        
                        // Animate the point
                        random = 0.5 + 0.5 * sin(time * 0.5 + 6.2831 * random);
                        
                        float2 voronoiPt = offset + random;
                        float d = length(voronoiPt - fv);
                        
                        if (d < minDist)
                        {
                            minDist2 = minDist;
                            minDist = d;
                            cellId = cell;
                        }
                        else if (d < minDist2)
                        {
                            minDist2 = d;
                        }
                    }
                }
                
                // Edge distance for roughness variation
                float edgeDist = minDist2 - minDist;
                
                return float2(minDist, edgeDist);
            }
            
            // Voronoi normal from derivatives
            float3 VoronoiNormal(float2 uv, float time)
            {
                float2 cellId;
                float eps = 0.01;
                
                float h = Voronoi(uv, time, cellId).x;
                float hx = Voronoi(uv + float2(eps, 0), time, cellId).x;
                float hy = Voronoi(uv + float2(0, eps), time, cellId).x;
                
                return float3(hx - h, hy - h, eps);
            }
            
            // Sample background with refraction
            float4 SampleBackground(float2 screenUV, float2 offset, float roughness)
            {
                // Apply roughness-based blur using mip levels
                float mipLevel = roughness * 4.0;
                
                float2 distortedUV = screenUV + offset * _RefractionStrength;
                
                // Clamp UV to valid range
                distortedUV = clamp(distortedUV, 0.001, 0.999);
                
                // Sample with roughness blur
                float4 col = SAMPLE_TEXTURE2D_LOD(_BackgroundTex, sampler_BackgroundTex, distortedUV, mipLevel);
                
                return col;
            }
            
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.position = TransformObjectToHClip(v.vertex.xyz);
                o.worldPosition = TransformObjectToWorld(v.vertex.xyz);
                o.worldNormal = TransformObjectToWorldNormal(v.normal);
                o.worldViewDir = GetWorldSpaceNormalizeViewDir(o.worldPosition);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.position);
                
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
                float time = _Time.y * _VoronoiSpeed;
                float2 voronoiUV = i.uv * _VoronoiScale;
                
                // Get Voronoi data
                float2 cellId;
                float2 voronoiData = Voronoi(voronoiUV, time, cellId);
                float voronoiDist = voronoiData.x;
                float edgeDist = voronoiData.y;
                
                // Generate normal from Voronoi
                float3 voronoiNormal = VoronoiNormal(voronoiUV, time);
                float3 normalTS = normalize(float3(voronoiNormal.xy * _NormalStrength, 1.0 - _NormalStrength));
                
                // Convert to world space
                float3x3 TBN = float3x3(
                    float3(1, 0, 0),
                    float3(0, 1, 0),
                    float3(0, 0, 1)
                );
                float3 normalWS = normalize(mul(normalTS, TBN));
                
                // Refraction offset based on Voronoi
                float3 refractDir = refract(-i.worldViewDir, normalWS, 1.0 / 1.5);
                float2 refractOffset = refractDir.xy * _DistortionAmount * voronoiDist;
                
                // Screen space UV
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                
                // Calculate roughness variation from Voronoi
                float roughnessVariation = edgeDist * _Roughness;
                float finalRoughness = clamp(_Roughness + roughnessVariation * 0.5, 0.0, 1.0);
                
                // Sample background with refraction
                float4 backgroundColor = SampleBackground(screenUV, refractOffset, finalRoughness);
                
                // Fresnel effect
                float fresnel = pow(1.0 - max(dot(i.worldViewDir, normalWS), 0.0), _FresnelPower);
                fresnel = clamp(fresnel + _FresnelBias, 0.0, 1.0);
                
                // Glass color
                float4 glassColor = _Tint;
                
                // Mix with refraction based on roughness
                float refractionAmount = (1.0 - finalRoughness) * (1.0 - fresnel * 0.5);
                glassColor.rgb = lerp(glassColor.rgb, backgroundColor.rgb, refractionAmount * 0.8);
                
                // Edge highlighting
                float edge = smoothstep(0.0, 0.1, edgeDist);
                glassColor.rgb += edge * 0.05;
                
                // Final alpha
                float alpha = lerp(_Tint.a, 1.0, fresnel * 0.5 + 0.5);
                alpha *= (1.0 - finalRoughness * 0.3);
                
                return float4(glassColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
