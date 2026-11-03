Shader "BreathingOrb/SoftOrb"
{
    Properties
    {
        _DeepColor("Deep Blue", Color) = (0.015, 0.075, 0.15, 1)
        _LightColor("Soft Cyan", Color) = (0.15, 0.75, 0.85, 1)
        _Breath01("Breath", Range(0,1)) = 0
        _Glow("Glow", Range(0.1,2)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "OrbForward"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _LightColor;
                float _Breath01;
                float _Glow;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float rim = pow(1 - saturate(dot(n, v)), 3.2);
                float light = saturate(dot(n, normalize(float3(-0.5, 0.7, -0.6))));
                // Static soft bands preserve a calm appearance when the cycle is paused.
                float3 p = input.positionOS;
                float bands = sin(p.y * 14 + sin(p.x * 8 + p.z * 5) * 1.2);
                bands = pow(saturate(bands * 0.5 + 0.5), 4);
                float3 color = lerp(_DeepColor.rgb, _LightColor.rgb, light * 0.5);
                color += _LightColor.rgb * bands * (0.06 + 0.1 * _Breath01);
                float highlight = pow(saturate(dot(reflect(-normalize(float3(-0.5, 0.7, -0.6)), n), v)), 40);
                color += float3(0.7, 0.94, 1) * highlight * 0.4;
                color += _LightColor.rgb * rim * (1.2 + _Breath01 * 0.6) * _Glow;
                color *= 0.9 + 0.15 * _Breath01;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
