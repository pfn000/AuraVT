// AuraVT — AuraCore Shader
// Transparent URP shader that renders avatars against a fully clear background.
// Supports alpha cutout and standard blending.
// Compatible with Unity URP 14+ (Unity 2022 LTS)

Shader "AuraVT/AuraCore"
{
    Properties
    {
        _BaseColor   ("Base Color", Color) = (1, 1, 1, 1)
        _BaseMap     ("Base Texture", 2D) = "white" {}
        _Cutoff      ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Emission    ("Emission Color", Color) = (0, 0, 0, 0)
        _EmissionMap ("Emission Map", 2D) = "black" {}

        // Rim light for avatar edge glow (overlay-friendly)
        _RimColor    ("Rim Color", Color) = (0.5, 0.5, 1.0, 1.0)
        _RimPower    ("Rim Power", Range(0.5, 8.0)) = 3.0
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.3

        // Rendering mode (set via script)
        [HideInInspector] _SrcBlend   ("Src Blend",   Float) = 1
        [HideInInspector] _DstBlend   ("Dst Blend",   Float) = 0
        [HideInInspector] _ZWrite     ("ZWrite",      Float) = 1
    }

    SubShader
    {
        Tags {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
        }

        // ── MAIN PASS ──────────────────────────────────────────────────────
        Pass
        {
            Name "AuraCoreForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend   [_SrcBlend] [_DstBlend]
            ZWrite  [_ZWrite]
            Cull    Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _Emission;
                float4 _EmissionMap_ST;
                float4 _RimColor;
                float  _Cutoff;
                float  _RimPower;
                float  _RimStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   norInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.normalWS    = norInputs.normalWS;
                OUT.viewDirWS   = GetWorldSpaceViewDir(posInputs.positionWS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor   = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Sample base texture
                float4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float4 color   = baseTex * _BaseColor;

                // Alpha test
                clip(color.a - _Cutoff);

                // Lighting
                float3 normal    = normalize(IN.normalWS);
                float3 viewDir   = normalize(IN.viewDirWS);
                Light  mainLight = GetMainLight();
                float  NdotL     = saturate(dot(normal, mainLight.direction));
                float3 lighting  = mainLight.color * NdotL;
                color.rgb *= lighting + 0.2; // 0.2 = ambient floor

                // Rim light (looks great in desktop overlay context)
                float rim    = 1.0 - saturate(dot(viewDir, normal));
                float rimVal = pow(rim, _RimPower) * _RimStrength;
                color.rgb   += _RimColor.rgb * rimVal * _RimColor.a;

                // Emission
                float4 emTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv);
                color.rgb   += emTex.rgb * _Emission.rgb;

                // Fog
                color.rgb = MixFog(color.rgb, IN.fogFactor);

                return color;
            }
            ENDHLSL
        }

        // ── SHADOW CASTER (skipped on low-end via AppBootstrap) ──────────
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
