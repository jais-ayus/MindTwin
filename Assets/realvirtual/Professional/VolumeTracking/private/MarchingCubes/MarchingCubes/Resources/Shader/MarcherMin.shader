Shader "Bonzai/MarcherMin" 
{
    Properties
    {
        
		_Color("Color", Color) = (1,1,1,1)
    }
	
	SubShader 
	{
		Pass 
		{
			Tags {"LightMode"="ForwardBase"}
			Cull Back

			CGPROGRAM
			#include "UnityCG.cginc"
			#include "UnityLightingCommon.cginc"
			#pragma target 5.0
			#pragma vertex vert
			#pragma fragment frag

            struct Triangle 
            {
                float3 p1;
                float3 p2;
                float3 p3;		
            };
			
		
			uniform StructuredBuffer<Triangle> _MeshBuffer;

			
            float4x4 _ObjectToWorldMatrix;


			float3 _Resolution;
			
		
			float4 _Color;


			struct v2f 
			{
				float4 pos : SV_POSITION;
				float3 worldPos : TEXCOORD2;
				
			};

			v2f vert(uint id : SV_VertexID, out float pointSize : PSIZE)
			{
                int i = id/3;
                int ti = id % 3;

                Triangle t = _MeshBuffer[i];


				float3 vert;
                
                if(ti == 0){
                    vert = t.p1;
                }
                if(ti == 1){
                    vert = t.p2;
                }
                if(ti == 2){
                    vert = t.p3;
                }


				float3 worldPos = mul(_ObjectToWorldMatrix, float4(vert,1)).xyz;

				



				v2f o;
				o.pos = mul(UNITY_MATRIX_VP, float4(worldPos,1));
				o.worldPos = worldPos;

				pointSize = 10;
				
				return o;
			}

			float4 frag(v2f i) : COLOR
			{

				

				float4 color = _Color;


                float3 view = _WorldSpaceCameraPos - i.worldPos.xyz;

 

                return color;















			
			}

			ENDCG

		}
	}
}