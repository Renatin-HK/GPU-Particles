Shader "Unlit/ParticlePointShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geo
            #pragma fragment frag

            #include "UnityCG.cginc"

            // Buffer com as posições das partículas
            StructuredBuffer<float2> _Positions;
            
            // Variáveis controladas pelo script em C#
            float _ParticleSize;
            fixed4 _Color;

            // Estrutura de dados passada do Vertex para o Geometry Shader
            struct v2g
            {
                //(object space)
                float4 pos : POSITION;
            };

            // Estrutura de dados passada do Geometry para o Fragment Shader
            struct g2f
            {
                float4 vertex : SV_POSITION;
            };
            
            v2g vert (uint vertexID : SV_VertexID)
            {
                v2g o;
                // Pega a posição 2D da partícula e a coloca em um float4
                o.pos = float4(_Positions[vertexID], 0.0, 1.0);
                return o;
            }

            // Geometry Shader: Expande cada ponto em um quadrado (quad).
            [maxvertexcount(4)]
            void geo(point v2g p[1], inout TriangleStream<g2f> triStream)
            {
                float4 center = p[0].pos;
                float halfSize = _ParticleSize * 0.5;

                // Define os 4 cantos do quadrado em object space
                float4 v[4];
                v[0] = float4(center.x - halfSize, center.y - halfSize, 0, 1);
                v[1] = float4(center.x + halfSize, center.y - halfSize, 0, 1);
                v[2] = float4(center.x - halfSize, center.y + halfSize, 0, 1);
                v[3] = float4(center.x + halfSize, center.y + halfSize, 0, 1);

                g2f o;

                // Constrói o quad com dois triângulos e envia para o pipeline
                o.vertex = UnityObjectToClipPos(v[2]);
                triStream.Append(o);

                o.vertex = UnityObjectToClipPos(v[3]);
                triStream.Append(o);

                o.vertex = UnityObjectToClipPos(v[0]);
                triStream.Append(o);

                o.vertex = UnityObjectToClipPos(v[1]);
                triStream.Append(o);
            }

            // Fragment Shader: Simplesmente colore o pixel.
            fixed4 frag (g2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
