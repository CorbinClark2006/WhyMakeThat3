Shader "Convoy/Nature Billboard"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _NightTint ("Night tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _NightTint;
            CBUFFER_END
            struct Attributes
            {
                float3 center : POSITION;
                float2 uv : TEXCOORD0;
                float2 offset : TEXCOORD1;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fog : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 center = TransformObjectToWorld(input.center);
                float2 direction = center.xz - GetCameraPositionWS().xz;
                direction *= rsqrt(max(dot(direction, direction), 0.000001));
                float3 right = float3(direction.y, 0, -direction.x);
                float3 position = center + right * input.offset.x + float3(0, input.offset.y, 0);
                output.position = TransformWorldToHClip(position);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.position.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(color.a - 0.001);
                color.rgb = MixFog(color.rgb * _NightTint.rgb, input.fog);
                return color;
            }
            ENDHLSL
        }
    }
}
