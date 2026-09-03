Shader "Hidden/KitchenDesigner/ScreenSpaceGI"
{
    // Экранный сбор непрямого освещения (одно приближённое отражение): для каждого
    // пикселя собираем свет с соседних поверхностей в его верхней полусфере и
    // добавляем к цвету. Даёт подсветку тёмных ниш цветом соседей и цветовые
    // переливы. Не ray-march — устойчиво, без чёрного экрана.
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "ScreenSpaceGI"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _SsgiStrength;   // сила добавляемого непрямого света
            float _SsgiRadius;     // радиус сбора, метры
            int   _SsgiSamples;    // число выборок

            static const float GOLDEN = 2.399963f; // золотой угол, рад

            float3 WorldPos(float2 uv, float rawDepth)
            {
                return ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
            }

            bool IsSky(float rawDepth)
            {
            #if UNITY_REVERSED_Z
                return rawDepth <= 1e-6;
            #else
                return rawDepth >= 1.0 - 1e-6;
            #endif
            }

            half Luma(half3 c) { return dot(c, half3(0.2126, 0.7152, 0.0722)); }

            // Псевдослучайное [0..1) на пиксель — для поворота спирали (джиттер),
            // чтобы структурные полосы выборки превратились в мелкий шум.
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);

                float rawD = SampleSceneDepth(uv);
                if (IsSky(rawD)) return col;

                float3 P = WorldPos(uv, rawD);
                // Нормаль восстанавливаем из глубины (без отдельного prepass).
                float3 N = normalize(cross(ddy(P), ddx(P)));

                float eyeD = LinearEyeDepth(rawD, _ZBufferParams);
                float uvRadius = saturate(_SsgiRadius / max(eyeD, 0.2)) * 0.4;

                float rot = Hash(uv * _ScreenParams.xy) * 6.2831853;
                float cs = cos(rot), sn = sin(rot);

                int samples = max(_SsgiSamples, 1);
                half3 indirect = 0;
                half wsum = 0;

                UNITY_LOOP
                for (int i = 0; i < samples; i++)
                {
                    float t = (i + 0.5) / samples;
                    float ang = i * GOLDEN;
                    float2 b = float2(cos(ang), sin(ang));
                    float2 dir = float2(b.x * cs - b.y * sn, b.x * sn + b.y * cs);
                    float2 suv = uv + dir * (uvRadius * sqrt(t));

                    if (suv.x < 0 || suv.x > 1 || suv.y < 0 || suv.y > 1) continue;

                    float sd = SampleSceneDepth(suv);
                    if (IsSky(sd)) continue;

                    float3 Ps = WorldPos(suv, sd);
                    float3 d = Ps - P;
                    float dist = length(d);
                    if (dist < 1e-4 || dist > _SsgiRadius) continue;

                    float3 dn = d / dist;
                    float ndl = dot(N, dn);
                    if (ndl <= 0.05) continue;

                    // Отсекаем просачивание через большой разрыв глубины: если сосед
                    // «улетел» далеко по лучу камеры относительно радиуса — это другой
                    // объект за краем, а не соседняя поверхность.
                    float eyeDs = LinearEyeDepth(sd, _ZBufferParams);
                    if (abs(eyeDs - eyeD) > _SsgiRadius) continue;

                    half3 sc = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, suv, 0).rgb;
                    float falloff = 1.0 - saturate(dist / _SsgiRadius);
                    half w = ndl * falloff * falloff;
                    indirect += sc * w;
                    wsum += w;
                }

                indirect /= (half)samples;

                // Заполняем в основном тёмные участки, чтобы яркие поверхности не мылить.
                half mask = saturate(1.0 - Luma(col.rgb) * 0.8);
                half3 outc = col.rgb + indirect * (_SsgiStrength * mask);
                return half4(outc, col.a);
            }
            ENDHLSL
        }

        // Проход 1: билатеральное размытие по глубине — гасит зерно SSGI на
        // плоских поверхностях, но сохраняет геометрические края (разрывы глубины).
        Pass
        {
            Name "ScreenSpaceGIBlur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            int _SsgiBlurRadius;   // радиус денойза в пикселях, 0 — денойз выключен

            bool IsSky(float rawDepth)
            {
            #if UNITY_REVERSED_Z
                return rawDepth <= 1e-6;
            #else
                return rawDepth >= 1.0 - 1e-6;
            #endif
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 center = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);

                int R = _SsgiBlurRadius;
                if (R <= 0) return center;

                float rawC = SampleSceneDepth(uv);
                if (IsSky(rawC)) return center;
                float eyeC = LinearEyeDepth(rawC, _ZBufferParams);

                float2 texel = 1.0 / _ScreenParams.xy;
                half3 accum = center.rgb;
                half wsum = 1.0;

                UNITY_LOOP
                for (int y = -R; y <= R; y++)
                {
                    UNITY_LOOP
                    for (int x = -R; x <= R; x++)
                    {
                        if (x == 0 && y == 0) continue;
                        float2 suv = uv + float2(x, y) * texel;
                        float rawS = SampleSceneDepth(suv);
                        if (IsSky(rawS)) continue;
                        float eyeS = LinearEyeDepth(rawS, _ZBufferParams);

                        float spatial = exp(-(x * x + y * y) * 0.12);
                        float depthW = exp(-abs(eyeS - eyeC) * 12.0); // разрыв глубины → вес ~0
                        half w = spatial * depthW;
                        accum += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, suv, 0).rgb * w;
                        wsum += w;
                    }
                }
                return half4(accum / wsum, center.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
