Shader "Custom/LocalizedFog"
{
    Properties
    {
        _MainTex ("Fog Noise (Grayscale)", 2D) = "white" {}
        [HDR] _FogColor ("Fog Color (Lights)", Color) = (0.2, 0.2, 0.2, 1)
        [HDR] _ShadowColor ("Shadow Color (Darks)", Color) = (0, 0, 0, 1) // Default to Black to avoid blue bugs
        _NoiseScale ("Noise Scale/Tiling", Float) = 1.0
        _Speed ("Scroll Speed", Vector) = (0.05, 0.05, 0, 0)
        _Density ("Base Density (Intensity)", Range(0, 20)) = 5.0
        _MinOpacity ("Min Visibility (Hide Sky)", Range(0, 1)) = 0.5
        
        [Header(Fog of War Area)]
        _InnerRadius ("Clear Radius", Float) = 8.0
        _OuterRadius ("Fade Radius", Float) = 20.0
        _HeightFade ("Height Limit", Float) = 10.0
        
        [Header(Photoshop Curves)]
        _BlackPoint ("Black Point (Shadows)", Range(0, 1)) = 0.0
        _WhitePoint ("White Point (Highlights)", Range(0, 1)) = 1.0
        _Gamma ("Gamma (Midtones)", Range(0.1, 5.0)) = 1.8
        _Contrast ("Contrast Curve", Range(0, 2)) = 1.1

        [Header(Noise Levels)]
        _NoiseMin ("Noise Floor", Range(0, 1)) = 0.2
        _NoiseMax ("Noise Ceiling", Range(0, 1)) = 0.8

        [Header(Atmosphere and Depth)]
        _DepthFadeDistance ("Floor Blending", Range(0.01, 10)) = 2.0
        _CameraFade ("Camera Proximity", Range(0.01, 5)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+50" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            ZWrite On  
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _FogColor;
                float4 _ShadowColor;
                float _NoiseScale;
                float2 _Speed;
                float _Density;
                float _MinOpacity;
                float _InnerRadius;
                float _OuterRadius;
                float _HeightFade;
                float _BlackPoint;
                float _WhitePoint;
                float _Gamma;
                float _Contrast;
                float _NoiseMin;
                float _NoiseMax;
                float _DepthFadeDistance;
                float _CameraFade;
            CBUFFER_END

            float4 _PlayerPos;
            float4 _ExtraLightPos[8];
            float _ExtraLightRadius[8];

            Varyings vert (Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            // High Precision S-Curve for Professional Look
            float ContrastCurve(float x, float contrast)
            {
                float res = x < 0.5 ? 0.5 * pow(2.0 * x, contrast) : 1.0 - 0.5 * pow(2.0 * (1.0 - x), contrast);
                return saturate(res);
            }

            half4 frag (Varyings input) : SV_Target
            {
                // 1. Calculate Linear Mask (Distance from Player)
                float distXZ = distance(input.positionWS.xz, _PlayerPos.xz);
                float rawMask = saturate((distXZ - _InnerRadius) / max(0.01, _OuterRadius - _InnerRadius));
                
                // Height restriction
                float heightDiff = abs(input.positionWS.y - _PlayerPos.y);
                float heightFactor = saturate(heightDiff / _HeightFade);
                rawMask = lerp(rawMask, 1.0, heightFactor);

                // Extra Lights
                for(int i = 0; i < 8; i++)
                {
                    if(_ExtraLightPos[i].w > 0)
                    {
                        float d = distance(input.positionWS.xz, _ExtraLightPos[i].xz);
                        float lightRadius = _ExtraLightRadius[i];
                        float hDiff = abs(input.positionWS.y - _ExtraLightPos[i].y);
                        float hFactor = saturate(hDiff / (lightRadius * 0.5));
                        
                        float extraMask = smoothstep(0.2, 1.0, d / max(0.01, lightRadius));
                        extraMask = lerp(extraMask, 1.0, hFactor);
                        rawMask = min(rawMask, extraMask);
                    }
                }
                
                // 2. APPLY PHOTOSHOP CURVES & LEVELS
                float curvedMask = pow(rawMask, _Gamma);
                curvedMask = saturate((curvedMask - _BlackPoint) / max(0.001, _WhitePoint - _BlackPoint));
                curvedMask = ContrastCurve(curvedMask, _Contrast);

                // 3. Noise & Texture
                float2 uv1 = input.positionWS.xz * 0.1 * _NoiseScale + _Speed * _Time.y;
                float2 uv2 = input.positionWS.xz * 0.25 * _NoiseScale - _Speed * _Time.y * 0.4;
                float noiseRaw = (tex2D(_MainTex, uv1).r + tex2D(_MainTex, uv2).r) * 0.5;
                
                // Noise Levels
                float noise = smoothstep(_NoiseMin, _NoiseMax, noiseRaw);
                float visibilityMultiplier = lerp(_MinOpacity, 1.0, noise);

                // 4. Color Tinting (Professional Depth)
                // We use noiseRaw (unclamped) for smoother color transition
                half3 finalRGB = lerp(_ShadowColor.rgb, _FogColor.rgb, noiseRaw);

                // 5. Technical Fades
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float vertexDepth = input.screenPos.w;
                
                float depthFade = saturate((sceneDepth - vertexDepth) / _DepthFadeDistance);
                float camFade = saturate((vertexDepth - _CameraFade) / 2.0);

                // 6. Final Combination
                float alpha = curvedMask * visibilityMultiplier * _Density * depthFade * camFade;

                // Edge protection (hides sky) - and ensures it matches shadow color
                float mapEdgeHider = smoothstep(0.85, 1.0, curvedMask);
                alpha = lerp(alpha, 1.0, mapEdgeHider); 

                return half4(finalRGB, saturate(alpha * _FogColor.a));
            }
            ENDHLSL
        }
    }
}
