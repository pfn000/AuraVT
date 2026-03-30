// AuraVT — AuraToon Shader
// Universal URP toon shader. Accepts remapped properties from:
//   - Poiyomi Toon 8.x (via PoiyomiRemapper)
//   - lilToon 1.7x     (via LilToonRemapper)
//   - MToon 0.x / 1.0  (via ShaderRemapper.RemapMToon)
//   - Standard Lit      (via ShaderRemapper.RemapStandardToURPLit fallback)
//
// Features:
//   - Hard-step toon shading with softness control
//   - Outline pass (inverted hull method)
//   - Rim light
//   - Emission
//   - Alpha cutout + transparent blend
//   - URP Forward rendering path

Shader "AuraVT/AuraToon"
{
    Properties
    {
        // ── Base ──────────────────────────────────────────────────────────────
        _BaseColor      ("Base Color",    Color) = (1,1,1,1)
        _BaseMap        ("Base Texture",  2D)    = "white" {}

        // ── Toon Shading ──────────────────────────────────────────────────────
        _ShadeColor     ("Shade Color",   Color) = (0.5, 0.5, 0.65, 1)
        _ShadeStep      ("Shade Step",    Range(0,1)) = 0.5
        _ShadeSoftness  ("Shade Softness",Range(0,1)) = 0.05

        // ── Rim Light ─────────────────────────────────────────────────────────
        _RimColor       ("Rim Color",     Color) = (0.6, 0.6, 1.0, 1)
        _RimPower       ("Rim Power",     Range(0.5, 8)) = 3.0
        _RimStrength    ("Rim Strength",  Range(0, 1))   = 0.4

        // ── Emission ──────────────────────────────────────────────────────────
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,0)
        _EmissionMap    ("Emission Map",  2D)    = "black" {}

        // ── Outline ───────────────────────────────────────────────────────────
        _OutlineColor   ("Outline Color", Color) = (0.1, 0.1, 0.1, 1)
        _OutlineWidth   ("Outline Width", Range(0, 0.05)) = 0.002

        // ── Alpha ─────────────────────────────────────────────────────────────
        _Cutoff         ("Alpha Cutoff",  Range(0,1)) = 0.5
        _AlphaClip      ("Alpha Clip On", Float) = 0

        // ── Blend (set by remapper) ────────────────────────────────────────────
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 5   // SrcAlpha
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 10  // OneMinusSrcAlpha
        [HideInInspector] _ZWrite   ("ZWrite",    Float) = 0
    }

    SubShader
    {
        Tags {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PASS 1 — OUTLINE (inverted hull)
        // ═══════════════════════════════════════════════════════════════════════
        Pass
        {
            Name "AuraToon_Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull  Front   // Render back faces only
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex   vert_outline
            #pragma fragment frag_outline

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _ShadeColor;
                float4 _RimColor;
                float4 _EmissionColor;
                float4 _OutlineColor;
                float4 _EmissionMap_ST;
                float  _ShadeStep;
                float  _ShadeSoftness;
                float  _RimPower;
                float  _RimStrength;
                float  _OutlineWidth;
                float  _Cutoff;
                float  _AlphaClip;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };
            struct Varyings {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert_outline(Attributes IN)
            {
                Varyings OUT;
                // Push vertices out along normal in clip space
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 posWS    = TransformObjectToWorld(IN.positionOS.xyz);
                posWS          += normalWS * _OutlineWidth;
                OUT.positionHCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            float4 frag_outline(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PASS 2 — TOON FORWARD SHADING
        // ═══════════════════════════════════════════════════════════════════════
        Pass
        {
            Name "AuraToon_Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull  Back
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma shader_feature _ALPHACLIP_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _ShadeColor;
                float4 _RimColor;
                float4 _EmissionColor;
                float4 _OutlineColor;
                float4 _EmissionMap_ST;
                float  _ShadeStep;
                float  _ShadeSoftness;
                float  _RimPower;
                float  _RimStrength;
                float  _OutlineWidth;
                float  _Cutoff;
                float  _AlphaClip;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
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
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nor = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = pos.positionCS;
                OUT.normalWS    = nor.normalWS;
                OUT.viewDirWS   = GetWorldSpaceViewDir(pos.positionWS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor   = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float4 base    = baseTex * _BaseColor;

                // Alpha clip
                if (_AlphaClip > 0.5)
                    clip(base.a - _Cutoff);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // ── Main light toon shading ──────────────────────────────────
                Light mainLight = GetMainLight();
                float NdotL     = dot(N, mainLight.direction);

                // Smooth step creates the hard toon edge with controllable softness
                float toon  = smoothstep(
                    _ShadeStep - _ShadeSoftness * 0.5,
                    _ShadeStep + _ShadeSoftness * 0.5,
                    NdotL * 0.5 + 0.5   // remap -1..1 → 0..1
                );

                float3 litColor   = base.rgb * mainLight.color;
                float3 shadeColor = base.rgb * _ShadeColor.rgb;
                float3 color      = lerp(shadeColor, litColor, toon);

                // ── Additional lights (point/spot) — simplified toon ─────────
                #ifdef _ADDITIONAL_LIGHTS
                int addCount = GetAdditionalLightsCount();
                for (int i = 0; i < addCount; i++)
                {
                    Light addLight  = GetAdditionalLight(i, IN.positionHCS.xyz);
                    float addNdotL  = saturate(dot(N, addLight.direction));
                    float addToon   = step(0.1, addNdotL);
                    color          += base.rgb * addLight.color * addToon * addLight.distanceAttenuation * 0.5;
                }
                #endif

                // ── Rim light ────────────────────────────────────────────────
                float rim    = 1.0 - saturate(dot(V, N));
                float rimVal = pow(rim, _RimPower) * _RimStrength;
                color       += _RimColor.rgb * rimVal;

                // ── Emission ─────────────────────────────────────────────────
                float4 emTex  = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv);
                color        += emTex.rgb * _EmissionColor.rgb;

                // ── Fog ──────────────────────────────────────────────────────
                color = MixFog(color, IN.fogFactor);

                return float4(color, base.a);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "AuraToonShaderGUI"   // Phase 8 — shader inspector
}
