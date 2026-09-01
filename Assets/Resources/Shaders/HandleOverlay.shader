Shader "Hidden/KD/HandleOverlay"
{
    // Ручки трансформации обязаны читаться и хвататься где угодно: полка внутри
    // корпуса окружена геометрией со всех сторон, и при честном depth-тесте
    // стрелка тонула в соседней детали — видно только её основание, ухватиться
    // нечем. ZTest Always снимает это целиком, поэтому размещение ручек больше
    // не борется с перекрытием сдвигами (см. HandlePlacement).
    //
    // Цвет берётся из _BaseColor, а не из вершин (в отличие от Hidden/OverlayLine):
    // у ручек три материала по осям и один общий меш.
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.vertex.xyz);
                output.vertex = TransformWorldToHClip(worldPos);
                output.normalWS = TransformObjectToWorldNormal(input.normal);
                return output;
            }

            // Свет не из сцены, а фиксированный «из-за плеча камеры»: ручка
            // рисуется поверх всего и не должна темнеть вместе с помещением,
            // но плоская заливка превращает конус в силуэт без формы.
            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 l = normalize(float3(0.3, 0.8, -0.5));
                half shade = 0.65h + 0.35h * saturate(dot(n, l));
                return half4(_BaseColor.rgb * shade, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
