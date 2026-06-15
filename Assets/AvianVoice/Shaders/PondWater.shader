Shader "AvianVoice/PondWater"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.35, 0.72, 0.62, 0.45)
        _DeepColor ("Deep Color", Color) = (0.08, 0.28, 0.38, 0.55)
        _ReflectionTex ("Reflection Texture", 2D) = "black" {}
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.88
        _ReflectionBrightness ("Reflection Brightness", Range(0.5, 2)) = 1.15
        _ReflectionScale ("Reflection Scale", Range(0.5, 2.5)) = 1.5
        _OverheadReflection ("Overhead Reflection", Range(0, 1)) = 0.82
        _WaterTintStrength ("Water Tint Over Reflection", Range(0, 1)) = 0.35
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 2)) = 0.42
        _RippleScale ("Ripple Tiling", Range(1, 24)) = 9
        _RippleStrength ("Ripple Specular", Range(0, 1)) = 0.38
        _WaveAmplitude ("Wave Height", Range(0, 0.15)) = 0.035
        _WaveFrequency ("Wave Frequency", Range(0.1, 4)) = 1.35
        _ReflectionDistortion ("Reflection Distortion", Range(0, 0.08)) = 0.018
        _WaveSpeed ("Wave Speed", Range(0, 3)) = 0.55
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.5
        _PondEdgeStart ("Pond Edge Fade Start", Range(0.4, 1)) = 0.72
        _PondEdgeWidth ("Pond Edge Fade Width", Range(0.02, 0.35)) = 0.16
        _FoamColor ("Shore Foam", Color) = (0.85, 0.95, 0.9, 1)
        _FoamDistance ("Foam Distance", Range(0.01, 3)) = 0.55
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
                float4 screenPos : TEXCOORD5;
                float pondMask : TEXCOORD6;
            };

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            float4 _ShallowColor;
            float4 _DeepColor;
            float _ReflectionStrength;
            float _ReflectionBrightness;
            float _ReflectionScale;
            float _OverheadReflection;
            float _WaterTintStrength;
            float _BumpScale;
            float _RippleScale;
            float _RippleStrength;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _ReflectionDistortion;
            float _WaveSpeed;
            float _FresnelPower;
            float _PondEdgeStart;
            float _PondEdgeWidth;
            float4 _FoamColor;
            float _FoamDistance;

            float SampleWaveHeight(float2 xz, float time)
            {
                float2 p = xz * _WaveFrequency;
                float waveA = sin(p.x * 1.12 + time * 0.95) * cos(p.y * 0.88 - time * 0.72);
                float waveB = sin(p.x * 0.67 - time * 0.58 + 1.7) * sin(p.y * 1.31 + time * 0.81);
                return (waveA * 0.65 + waveB * 0.35) * _WaveAmplitude;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                float time = _Time.y * _WaveSpeed;
                float wave = SampleWaveHeight(positionOS.xz, time);
                positionOS.y += wave;

                VertexPositionInputs posInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.tangentWS = normInputs.tangentWS;
                output.bitangentWS = normInputs.bitangentWS;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(posInputs.positionCS);

                float2 pondUv = input.uv * 2.0 - 1.0;
                float pondRadius = length(pondUv);
                output.pondMask = 1.0 - smoothstep(_PondEdgeStart, _PondEdgeStart + _PondEdgeWidth, pondRadius);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (input.pondMask < 0.01)
                    discard;

                float time = _Time.y * _WaveSpeed;
                float2 waveUv1 = input.positionWS.xz * (_RippleScale * 0.1) + time * float2(0.05, 0.04);
                float2 waveUv2 = input.positionWS.xz * (_RippleScale * 0.08) - time * float2(0.035, 0.055);
                float2 waveUv3 = input.uv * _RippleScale + time * float2(-0.025, 0.03);
                float3 bump1 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, waveUv1));
                float3 bump2 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, waveUv2));
                float3 bump3 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, waveUv3));
                float3 bump = normalize(float3(
                    bump1.xy + bump2.xy + bump3.xy * 0.6,
                    bump1.z * bump2.z * bump3.z));
                bump.xy *= _BumpScale;

                float3x3 tbn = float3x3(
                    normalize(input.tangentWS),
                    normalize(input.bitangentWS),
                    normalize(input.normalWS));
                float3 normalWS = normalize(mul(bump, tbn));

                float2 screenUv = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUv), _ZBufferParams);
                float surfaceDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float depthDiff = saturate((sceneDepth - surfaceDepth) / max(_FoamDistance, 0.001));

                half3 waterColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, depthDiff);
                float alpha = lerp(_DeepColor.a, _ShallowColor.a, depthDiff);
                alpha *= input.pondMask;

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), _FresnelPower);
                float overhead = saturate(dot(normalWS, viewDir));
                float2 reflUv = screenUv + bump.xy * _ReflectionDistortion;
                reflUv = (reflUv - 0.5) / max(_ReflectionScale, 0.01) + 0.5;
                half3 reflection = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, reflUv).rgb
                    * _ReflectionBrightness;
                float reflectMix = _ReflectionStrength * saturate(
                    _OverheadReflection * overhead + fresnel * (1.0 - _OverheadReflection * 0.35));
                half3 color = lerp(waterColor, reflection, reflectMix);
                color = lerp(color, waterColor, _WaterTintStrength * (1.0 - reflectMix));

                float shoreFoam = 1.0 - saturate(depthDiff);
                float pondEdgeFoam = 1.0 - smoothstep(0.0, 0.35, input.pondMask);
                float foam = saturate(shoreFoam + pondEdgeFoam * 0.85);
                color = lerp(color, _FoamColor.rgb, foam * _FoamColor.a);

                Light mainLight = GetMainLight();
                float rippleSpec = pow(saturate(dot(reflect(-viewDir, normalWS), mainLight.direction)), 48.0);
                color += mainLight.color * rippleSpec * (_RippleStrength + 0.12);
                float sunSpec = pow(saturate(dot(reflect(-viewDir, normalWS), mainLight.direction)), 96.0);
                color += mainLight.color * sunSpec * 0.18;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
