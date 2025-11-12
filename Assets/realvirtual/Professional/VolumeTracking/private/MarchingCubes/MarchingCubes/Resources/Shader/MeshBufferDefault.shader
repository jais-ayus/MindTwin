Shader "Bonzai/MeshBufferDefault" 
{
    Properties
    {
        _Reflection("Reflection", Range(0.001,1)) = 0.001
		_Shininess("Shininess", Range(0.0,1000)) = 0
		_NormalBias("NormalBias", Range(0.5, 4)) = 1
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
			
		
			uniform StructuredBuffer<Triangle> _Buffer;

			
            float4x4 _ObjectToWorldMatrix;
			Texture3D<float> _Densities;
			SamplerState sampler_Densities;

			Texture3D<float4> _Colors;
			SamplerState sampler_Colors;

			SamplerState sampler_point_repeat;


			float3 _Resolution;
			float3 _VoxelSize;
			//float3 _ReductionVector;
			//float3 _LocalOffset;



			float _Reflection;
            float _Shininess;
			float _NormalBias;

			float4 _Color;

			

			float3 VertToUV(float3 vert){
				return (vert.xyz + (_Resolution * _VoxelSize * 0.5)) / ((_Resolution-1)*_VoxelSize);
			}

			

			float SampleTexture(float3 uv){

				return _Densities.SampleLevel(sampler_Densities, uv, 0.0);
			}

			

			float3 SampleNormal(float3 uv){
				float f = _NormalBias;
				float d = 1.0/_Resolution.x * 0.5 * f;
				float dx = (SampleTexture(uv - float3(d,0,0)) - SampleTexture(uv + float3(d,0,0)));
				float dy = (SampleTexture(uv - float3(0,d,0)) - SampleTexture(uv + float3(0,d,0)));
				float dz = (SampleTexture(uv - float3(0,0,d)) - SampleTexture(uv + float3(0,0,d)));

				float3 normal = normalize(float3(dx,dy,dz));

				return normal;

			}


			struct v2f 
			{
				float4 pos : SV_POSITION;
				float3 uv : TEXCOORD0;
				float3 worldPos : TEXCOORD2;
				
			};

			v2f vert(uint id : SV_VertexID, out float pointSize : PSIZE)
			{
                int i = id/3;
                int ti = id % 3;

                Triangle t = _Buffer[i];


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


				float3 uv = VertToUV(vert);

				//vert = vert*_ReductionVector;
				//vert -= _LocalOffset;

				float3 worldPos = mul(_ObjectToWorldMatrix, float4(vert,1)).xyz;

				



				v2f o;
				o.pos = mul(UNITY_MATRIX_VP, float4(worldPos,1));
				o.uv = uv;
				o.worldPos = worldPos;

				pointSize = 10;
				
				return o;
			}

			float4 frag(v2f i) : COLOR
			{

				

				float3 normal = SampleNormal(i.uv);
				float4 color = 1;


				float3 worldNormal = normalize(mul(_ObjectToWorldMatrix, float4(normal, 1)).xyz - mul(_ObjectToWorldMatrix, float4(0,0,0,1)).xyz);


 
                float3 viewDirection = normalize(
                _WorldSpaceCameraPos - i.worldPos.xyz);
                float3 lightDirection;
                float attenuation;
    
                if (0.0 == _WorldSpaceLightPos0.w) // directional light?
                {
					attenuation = 1.0; // no attenuation
					lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                } 
                else // point or spot light
                {
					float3 vertexToLightSource = 
						_WorldSpaceLightPos0.xyz - i.worldPos.xyz;
					float distance = length(vertexToLightSource);
					attenuation = 1.0 / distance; // linear attenuation 
					lightDirection = normalize(vertexToLightSource);
                }
    
                float3 ambientLighting = 
                UNITY_LIGHTMODEL_AMBIENT.rgb * _Color.rgb * color;
    
                float3 diffuseReflection = 
                attenuation * _LightColor0.rgb * _Color.rgb * color
                * max(0.0, dot(worldNormal, lightDirection));

                float4 _SpecColor = float4(1,1,1,1);

    
                float3 specularReflection;
                if (dot(worldNormal, lightDirection) <= 0.0 || _Shininess == 0) 
                // light source on the wrong side?
                {
                	specularReflection = float3(0.0, 0.0, 0.0); 
                    // no specular reflection
                }
                else // light source on the right side
                {
                	specularReflection = attenuation * _LightColor0.rgb * _SpecColor.rgb * pow(max(0.0, dot(reflect(-lightDirection, worldNormal), viewDirection)), _Shininess);
                }

                half3 worldRefl = reflect(-viewDirection, worldNormal);
                half4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, worldRefl);
                half3 skyColor = DecodeHDR (skyData, unity_SpecCube0_HDR);
                diffuseReflection *= lerp(1, skyColor, _Reflection);

                return float4(ambientLighting + diffuseReflection 
                + specularReflection, 1.0);

















			
			}

			ENDCG

		}
	}
}