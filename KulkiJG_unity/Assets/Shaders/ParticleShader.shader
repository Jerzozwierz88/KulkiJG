Shader "Unlit/Particle" {
	Properties {
		
	}
	SubShader {

		Tags { "RenderType"="Transparent" "Queue"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off

		Pass {

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.5

			#include "UnityCG.cginc"
			
			StructuredBuffer<float2> positions;
			StructuredBuffer<float2> velocities;
			StructuredBuffer<float>  densities;
			float scale;
			float4 colA;
			Texture2D<float4> ColourMap;
			SamplerState linear_clamp_sampler;
			float velocityMax;
			float targetDensity;
			float densityRange;
			int what_to_display = 1;

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 colour : TEXCOORD1;
			};

			v2f vert (appdata_full v, uint instanceID : SV_InstanceID)
			{
				float colT = 1; 
				switch (what_to_display)
				{
					case 1:
						float density = densities[instanceID];
						colT = saturate((density - targetDensity) / densityRange);
						break;
					case 2:
						float velocity = length(velocities[instanceID]);
						colT = saturate(velocity / velocityMax);
						break;
				}
				
				float3 centreWorld = float3(positions[instanceID], 0);
				float3 worldVertPos = centreWorld + mul(unity_ObjectToWorld, v.vertex * scale);
				float3 objectVertPos = mul(unity_WorldToObject, float4(worldVertPos.xyz, 1));

				v2f o;
				o.uv = v.texcoord;
				o.pos = UnityObjectToClipPos(objectVertPos);
				o.colour = ColourMap.SampleLevel(linear_clamp_sampler, float2(colT, 0.5), 0);

				return o;
			}


			float4 frag (v2f i) : SV_Target
			{
				float2 centreOffset = (i.uv.xy - 0.5) * 2;
				float sqrDst = dot(centreOffset, centreOffset);
				float delta = fwidth(sqrt(sqrDst));
				float alpha = 1 - smoothstep(1 - delta, 1 + delta, sqrDst);

				float3 colour = i.colour;
				return float4(colour, alpha);
			}

			ENDCG
		}
	}
}