Shader "Custom/LocalizedFog"
{
    Properties
    {
        _MainTex ("Fog Noise (Grayscale)", 2D) = "white" {}
        [HDR] _FogColor ("Fog Color", Color) = (0.1, 0.1, 0.1, 1)
        _NoiseScale ("Noise Scale/Tiling", Float) = 1.0
        _Speed ("Scroll Speed", Vector) = (0.05, 0.05, 0, 0)
        _Density ("Base Density (Intensity)", Range(0, 10)) = 5.0
        _MinOpacity ("Min Opacity (Hide Sky)", Range(0, 1)) = 0.5
        
        [Header(Fog of War)]
        _InnerRadius ("Player Clear Radius", Float) = 8.0
        _OuterRadius ("Player Fade Radius", Float) = 20.0
        _HeightFade ("Height Visibility Limit", Float) = 10.0

        [Header(Atmosphere)]
        _DepthFadeDistance ("Geometry Softness", Range(0.01, 10)) = 2.0
        _CameraFade ("Camera Proximity Fade", Range(0.01, 5)) = 1.0
    }
    SubShader
    {
        // Change to Geometry queue to be treated as an opaque blocker
        Tags { "RenderType"="Opaque" "Queue"="Geometry+50" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            ZWrite On  // <--- KEY CHANGE: Writes to depth buffer
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
                float _NoiseScale;
                float2 _Speed;
                float _Density;
                float _MinOpacity;
                float _InnerRadius;
                float _OuterRadius;
                float _HeightFade;
                float _DepthFadeDistance;
                float _CameraFade;
            CBUFFER_END

            // Global variables from C#
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

            half4 frag (Varyings input) : SV_Target
            {
                // 1. Calculate Visibility Mask
                // Player Visibility
                float distXZ = distance(input.positionWS.xz, _PlayerPos.xz);
                float mask = saturate((distXZ - _InnerRadius) / max(0.01, _OuterRadius - _InnerRadius));
                
                // Height restriction: If too high above player, fog remains closed (hides sky)
                float heightDiff = abs(input.positionWS.y - _PlayerPos.y);
                float heightFactor = saturate(heightDiff / _HeightFade);
                mask = lerp(mask, 1.0, heightFactor);

                // Extra Lights (Torches/Lamps)
                for(int i = 0; i < 8; i++)
                {
                    if(_ExtraLightPos[i].w > 0)
                    {
                        float d = distance(input.positionWS.xz, _ExtraLightPos[i].xz);
                        float lightRadius = _ExtraLightRadius[i];
                        
                        // Vertical restriction for lights
                        float hDiff = abs(input.positionWS.y - _ExtraLightPos[i].y);
                        float hFactor = saturate(hDiff / (lightRadius * 0.5));
                        
                        // Smooth hole
                        float normalizedDist = d / max(0.01, lightRadius);
                        float extraMask = smoothstep(0.2, 1.0, normalizedDist);
                        extraMask = lerp(extraMask, 1.0, hFactor);

                        mask = min(mask, extraMask);
                    }
                }

                // 2. Noise & Texture
                float2 uv1 = input.positionWS.xz * 0.1 * _NoiseScale + _Speed * _Time.y;
                float2 uv2 = input.positionWS.xz * 0.25 * _NoiseScale - _Speed * _Time.y * 0.4;
                float noise = (tex2D(_MainTex, uv1).r + tex2D(_MainTex, uv2).r) * 0.5;
                noise = smoothstep(0.1, 0.8, noise);
                
                float noiseWithMin = lerp(_MinOpacity, 1.0, noise);

                // 3. Technical Fades
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float vertexDepth = input.screenPos.w;
                
                float depthFade = saturate((sceneDepth - vertexDepth) / _DepthFadeDistance);
                float camFade = saturate((vertexDepth - _CameraFade) / 2.0);

                // 4. Final Combination
                half4 col = _FogColor;
                float alpha = mask * noiseWithMin * _Density * depthFade * camFade;

                // Edge protection to hide sky
                float mapEdgeHider = smoothstep(0.85, 1.0, mask);
                alpha = lerp(alpha, 1.2, mapEdgeHider); 

                col.a = saturate(alpha * _FogColor.a);
                return col;
            }
            ENDHLSL
        }
    }
}
