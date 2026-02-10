Shader "Custom/LocalizedFog"
{
    Properties
    {
        _MainTex ("Fog Noise (Grayscale)", 2D) = "white" {}
        [HDR] _FogColor ("Fog Color", Color) = (1,1,1,1)
        _Speed ("Scroll Speed", Vector) = (0.1, 0.1, 0, 0)
        _Density ("Density", Range(0, 2)) = 1.0
        _DepthFadeDistance ("Depth Fade Distance", Range(0.01, 5)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            ZWrite Off
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
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _FogColor;
                float2 _Speed;
                float _Density;
                float _DepthFadeDistance;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // 1. Scrolling Noise
                float2 scrolledUV = input.uv + _Speed * _Time.y;
                float noise = tex2D(_MainTex, scrolledUV).r;

                // 2. Depth Fade (soft edge logic)
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneZ = LinearEyeDepth(rawDepth, _ZBufferParams);
                float thisZ = input.screenPos.w;
                float fade = saturate((sceneZ - thisZ) / _DepthFadeDistance);

                // 3. Final Color
                half4 col = _FogColor;
                col.a *= noise * fade * _Density;

                return col;
            }
            ENDHLSL
        }
    }
}
