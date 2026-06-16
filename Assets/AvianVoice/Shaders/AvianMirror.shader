Shader "AvianVoice/MirrorRT"
{
    Properties
    {
        _MainTex ("Mirror Render Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Brightness ("Brightness", Range(0.5, 2)) = 1.15
        _Vignette ("Vignette Strength", Range(0, 1)) = 0.03
        _MirrorFlipU ("Flip Reflection Horizontally", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Cull Off
        ZWrite Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _Tint;
            float _Brightness;
            float _Vignette;
            float4x4 _ReflectionVP;
            float _UseProjectedUv;
            float _MirrorFlipU;

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.worldPos = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = input.uv;
                return output;
            }

            float2 ProjectedReflectionUV(float3 worldPos, out bool valid)
            {
                float4 clip = mul(_ReflectionVP, float4(worldPos, 1.0));
                valid = clip.w > 0.0001;
                float2 uv = clip.xy / clip.w;
                uv = uv * 0.5 + 0.5;
                return uv;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                if (_UseProjectedUv > 0.5)
                {
                    bool valid;
                    float2 projected = ProjectedReflectionUV(input.worldPos, valid);
                    if (valid)
                        uv = projected;
                }

                if (_MirrorFlipU > 0.5)
                    uv.x = 1.0 - uv.x;

                uv = saturate(uv);
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Tint;
                col.rgb *= _Brightness;
                float2 centered = uv - 0.5;
                float vignette = 1.0 - dot(centered, centered) * _Vignette * 4.0;
                col.rgb *= saturate(vignette);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
