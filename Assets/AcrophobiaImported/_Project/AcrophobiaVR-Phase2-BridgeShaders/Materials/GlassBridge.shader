Shader "AcrophobiaVR/GlassBridge"
{
    Properties
    {
        _BaseColor ("Base Tint", Color) = (0.6, 0.8, 0.9, 0.25)
        _EdgeColor ("Fresnel Edge Color", Color) = (0.9, 0.95, 1.0, 0.9)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.0
        _Smoothness ("Smoothness", Range(0,1)) = 0.95
        _Alpha ("Base Alpha", Range(0,1)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                float _FresnelPower;
                float _Smoothness;
                float _Alpha;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(GetCameraPositionWS() - IN.positionWS);
                float3 normal = normalize(IN.normalWS);

                // Fresnel: near-grazing angles read more opaque/reflective,
                // straight-down look-through stays clearer — this is what makes
                // the drop below read as "there" without fully occluding it.
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);

                half4 color = lerp(_BaseColor, _EdgeColor, fresnel);
                float alpha = saturate(_Alpha + fresnel * (_EdgeColor.a - _Alpha));

                // Minimal direct-light response so the bridge still reads as a
                // physical surface under your HDRI, not a flat color overlay.
                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(normal, mainLight.direction));
                half3 lit = color.rgb + mainLight.color * ndotl * 0.15 * _Smoothness;

                return half4(lit, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
