Shader "Hidden/KD/UnlitColor"
{
    // Контур выделения не должен зависеть от освещения: это разметка, а не
    // предмет обстановки, и её цвет обязан быть один и тот же при свете и в
    // темноте. URP/Unlit для этого не годится — им не пользуется ни один
    // материал проекта, поэтому в собранном плеере его нет вовсе (проверено
    // по дереву сборки), Shader.Find возвращает там null, и контур молча
    // уезжал на URP/Lit, то есть начинал темнеть вместе с комнатой. Здесь
    // шейдер лежит в Resources — стриппинг такое не трогает.
    //
    // Глубина ЧЕСТНАЯ, в отличие от Hidden/KD/HandleOverlay: контур описывает
    // деталь и обязан прятаться вместе с ней, поверх всего рисуются только
    // ручки.
    Properties
    {
        // [MainColor] обязателен: ElementOutline пишет цвет и через SetColor, и
        // через Material.color, а тот без этого атрибута ищет _Color, которого
        // здесь нет.
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
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
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.vertex = TransformWorldToHClip(TransformObjectToWorld(input.vertex.xyz));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
