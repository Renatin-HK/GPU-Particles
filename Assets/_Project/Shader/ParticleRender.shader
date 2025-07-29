Shader "Unlit/ParticlePointShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

           
            StructuredBuffer<float2> _Positions;
            
            float _ParticleSize;
            fixed4 _Color;

            struct appdata
            {
                uint vertexID : SV_VertexID;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                
                float2 pos = _Positions[v.vertexID];

              
                o.vertex = UnityObjectToClipPos(float4(pos.x, pos.y, 0.0, 1.0));
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
