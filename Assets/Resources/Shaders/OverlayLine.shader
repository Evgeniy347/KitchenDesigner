Shader "Hidden/OverlayLine"
{
    // Как Hidden/GridLine, но с ZTest Always: разметка рулетки должна читаться
    // и внутри корпуса, и за стеной — иначе замер «пропадает» в геометрии.
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.vertex.xyz);
                output.vertex = TransformWorldToHClip(worldPos);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}
