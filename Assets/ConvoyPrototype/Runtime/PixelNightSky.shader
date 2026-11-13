Shader "Convoy/Pixel Night Sky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Varyings { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(float3 position : POSITION)
            {
                Varyings output;
                output.position = TransformObjectToHClip(position);
                output.direction = position;
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 ray = normalize(input.direction);
                half3 dark = half3(46,34,47) / 255.0;
                half3 haze = half3(69,41,63) / 255.0;
                half3 cream = half3(242,236,139) / 255.0;
                half3 sage = half3(176,169,135) / 255.0;
                float band = floor(saturate(ray.y * 2.5) * 5.0) / 5.0;
                half3 color = lerp(haze, dark, band);
                float2 sky = float2(atan2(ray.x, ray.z), asin(ray.y));
                float2 cell = floor(sky * 140.0);
                float2 pixel = frac(sky * 140.0);
                float star = step(0.991, Hash(cell)) * step(0.45, ray.y) *
                    step(0.32, pixel.x) * step(pixel.x, 0.68) * step(0.32, pixel.y) * step(pixel.y, 0.68);
                color = lerp(color, sage, star);
                float3 moon = normalize(float3(0.28, 0.25, -1));
                float3 right = normalize(cross(float3(0,1,0), moon));
                float3 up = cross(moon, right);
                float2 uv = floor(float2(dot(ray, right), dot(ray, up)) * 180.0) / 180.0;
                float radius = length(uv);
                float facing = step(0.9, dot(ray, moon));
                color = lerp(color, haze * 1.25, facing * step(radius, 0.135) * 0.35);
                float disc = facing * step(radius, 0.085);
                float crater = step(length(uv - float2(-0.025, 0.025)), 0.017) +
                    step(length(uv - float2(0.035, -0.018)), 0.022) +
                    step(length(uv - float2(-0.012, -0.043)), 0.012);
                half3 moonColor = lerp(cream, sage, saturate(crater) * 0.65);
                moonColor = lerp(moonColor, sage, step(0.069, radius) * 0.35);
                color = lerp(color, moonColor, disc);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                color = SRGBToLinear(color);
                #endif
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
