Shader "SereneNight/ForestWind" {
 Properties { _BaseMap("Texture",2D)="white"{} _BaseColor("Color",Color)=(1,1,1,1) _Cutoff("Cutout",Range(0,1))=.4 _WindStrength("Wind",Float)=.12 }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"} Cull Off
 Pass { Tags {"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseMap_ST,_BaseColor;float _Cutoff,_WindStrength;
 CBUFFER_END
 struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 normalWS:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A a) {V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
 float3 p=TransformObjectToWorld(a.positionOS.xyz);float phase=_Time.y*.8+p.x*.37+p.z*.29;
 float weight=saturate(a.positionOS.y/5);p.xz+=float2(sin(phase),cos(phase*.73))*weight*weight*_WindStrength;
 o.positionCS=TransformWorldToHClip(p);o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);return o;}
 half4 frag(V i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;clip(c.a-_Cutoff);
 Light l=GetMainLight();half light=.4+.6*saturate(dot(normalize(i.normalWS),l.direction));return half4(c.rgb*light,1);}
 ENDHLSL
 } }
}
