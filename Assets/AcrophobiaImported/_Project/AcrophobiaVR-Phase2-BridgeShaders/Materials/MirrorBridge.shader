Shader "AcrophobiaVR/MirrorBridge"
{
    Properties
    {
        _ReflectionTex ("Reflection Texture (set by PlanarReflection.cs)", 2D) = "black" {}
        _TintColor ("Reflection Tint", Color) = (0.9, 0.92, 0.95, 1.0)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 4.0
        _DistortionStrength ("Reflection Distortion", Range(0, 0.05)) = 0.01
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float4 screenPos    : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                float _FresnelPower;
                float _DistortionStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // Subtle normal-based distortion so the reflection isn't a
                // perfectly crisp mirror — a slightly imperfect reflection
                // reads as more "real" surface and less like an obvious effect.
                float2 distortion = IN.normalWS.xz * _DistortionStrength;
                screenUV += distortion;

                half4 reflection = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, screenUV);
                half4 color = reflection * _TintColor;

                float3 viewDir = normalize(GetCameraPositionWS() - IN.positionWS);
                float3 normal = normalize(IN.normalWS);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);

                // At grazing angles, boost reflection intensity slightly —
                // consistent with how real mirror-like floors behave and with
                // the plan's note that the doubled visual is what makes mirror
                // bridges more disorienting than plain glass.
                color.rgb *= (1.0 + fresnel * 0.3);

                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
