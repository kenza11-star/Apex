Shader "APEX/URP Stream"
{
    Properties
    {
        _Deep("Deep water", Color) = (0.035, 0.18, 0.19, 1)
        _Edge("Shallows", Color) = (0.14, 0.32, 0.27, 1)
        _Sky("Sky reflection", Color) = (0.42, 0.59, 0.67, 1)
        _Current("Flow speed", Float) = 1.0
        _Fall("Waterfall", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "StreamForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Deep, _Edge, _Sky;
            float _Current, _Fall;
            CBUFFER_END

            struct Data { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Pixel { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float2 uv : TEXCOORD1; half fog : TEXCOORD2; };

            Pixel Vert(Data input)
            {
                Pixel output;
                float3 at = input.positionOS.xyz;
                float bank = saturate(1 - abs(input.uv.x * 2 - 1));
                at.y += (1 - _Fall) * bank * 0.04 * sin(input.uv.y * 1.7 - _Time.y * 2.8);
                VertexPositionInputs pos = GetVertexPositionInputs(at);
                output.positionCS = pos.positionCS;
                output.world = pos.positionWS;
                output.uv = input.uv;
                output.fog = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 Frag(Pixel input) : SV_Target
            {
                float2 phase = input.uv * float2(28, 2.1) - float2(0, _Time.y * 2.6 * _Current);
                half a = sin(phase.x + phase.y);
                half b = sin(phase.x * 0.53 - phase.y * 1.42);
                half3 normalWS = normalize(half3(a * 0.16, 1, b * 0.14));
                half3 eye = normalize(GetWorldSpaceViewDir(input.world));
                half fresnel = pow(1 - saturate(dot(eye, normalWS)), 4);
                half bank = smoothstep(0.59, 1, abs(input.uv.x * 2 - 1));
                half foam = smoothstep(0.54, 0.92, a * b) * (bank * .53 + .10);
                half3 flow = lerp(_Deep.rgb, _Edge.rgb, bank);
                flow = lerp(flow, _Sky.rgb, fresnel * 0.72);
                flow = lerp(flow, half3(.82, .88, .85), foam);
                half3 falling = lerp(_Edge.rgb, half3(.89, .92, .92), saturate(.49 + a * .32 + b * .21));
                return half4(MixFog(lerp(flow, falling, _Fall), input.fog), 1);
            }
            ENDHLSL
        }
    }
}
