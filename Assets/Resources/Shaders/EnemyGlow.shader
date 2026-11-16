// Additive overlay drawn on top of an enemy sprite by EnemyGlow, ignoring scene lighting:
//   - eyes (pale yellow pixels) and the golem's cracks (pink-lavender pixels) glow; HDR colours
//     so the camera's Bloom gives them a halo
//   - a faint cool rim along the sprite's outline, so the near-black body (lit material, only
//     coloured inside candlelight) still reads as a silhouette in the dark
Shader "Candlepath/EnemyGlow"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        [HDR] _EyeColor ("Eye glow", Color) = (2.6, 2.1, 0.8, 1)
        [HDR] _CrackColor ("Crack glow", Color) = (2.0, 0.8, 2.4, 1)
        _Strength ("Strength", Range(0, 2)) = 1
        _RimColor ("Rim (moonlit edge)", Color) = (0.09, 0.11, 0.17, 1)
        _RimWidth ("Rim width (texels)", Range(1, 8)) = 4
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend One One
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4 _RimColor;
                half _RimWidth;
                half4 _EyeColor;
                half4 _CrackColor;
                half _Strength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                o.color = input.color;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // Eyes: bright, yellow (red and green high, blue clearly lower)
                half eye = smoothstep(0.70, 0.84, min(c.r, c.g))
                         * smoothstep(0.10, 0.20, (c.r + c.g) * 0.5 - c.b);

                // Golem cracks: bright pink-lavender (red and blue high, green lower)
                half crack = smoothstep(0.72, 0.84, c.r)
                           * smoothstep(0.80, 0.88, c.b)
                           * smoothstep(0.04, 0.12, c.r - c.g);

                half3 glow = (_EyeColor.rgb * eye + _CrackColor.rgb * crack) * _Strength;

                // Rim: inside the sprite but close to transparent pixels
                float2 d = _MainTex_TexelSize.xy * _RimWidth;
                half minA = 1;
                minA = min(minA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2( d.x, 0)).a);
                minA = min(minA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-d.x, 0)).a);
                minA = min(minA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0,  d.y)).a);
                minA = min(minA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, -d.y)).a);
                minA = min(minA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + d * 0.7071).a);
                minA = min(minA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - d * 0.7071).a);
                minA = min(minA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(d.x, -d.y) * 0.7071).a);
                minA = min(minA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-d.x, d.y) * 0.7071).a);
                half rim = saturate(c.a - minA);
                // stronger along the top edge, as if the moon were overhead
                half topBias = 1.0 + 0.6 * saturate(c.a - SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, d.y)).a);
                glow += _RimColor.rgb * rim * topBias * (1 - eye);

                // Vertex alpha follows the enemy fading out as it loses health
                return half4(glow * c.a * input.color.a, 0);
            }
            ENDHLSL
        }
    }
}
