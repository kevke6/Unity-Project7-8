Shader "PlayWay Water/Particles/Particles"
{
	Properties
	{

	}

	SubShader
	{
		Cull Off ZWrite Off ZTest Always
		Blend One One

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex		: POSITION;
				float2 uv			: TEXCOORD0;
				float4 tangent		: TANGENT;
			};

			struct v2f
			{
				float4 vertex		: SV_POSITION;
				half4 uv			: TEXCOORD0;
				half amplitude		: TEXCOORD1;
				half4 dir			: TEXCOORD2;
			};

			struct PsOutput
			{
				float4 displacement	: COLOR0;
				float4 slope		: COLOR1;
			};

			sampler2D _MainTex;
			float3 _ParticleFieldCoords;

			v2f vert (appdata vi)
			{
				v2f vo;

				float2 forward = vi.tangent.xy * _ParticleFieldCoords.z;
				float2 right = normalize(float2(forward.y, -forward.x));
				float width = vi.tangent.z;

				vi.vertex.xy = (vi.vertex.xy + _ParticleFieldCoords.xy) * _ParticleFieldCoords.zz * 2.0 - 1.0;
				vo.vertex = half4(vi.vertex.xy + forward * (vi.uv.y - 0.5) + right * width * _ParticleFieldCoords.z * (vi.uv.x - 0.5), 0, 1);
				vo.vertex.y = -vo.vertex.y;
				vo.uv = half4(vi.uv * 3.14159, vo.vertex.xy);
				vo.amplitude = vi.vertex.z;
				vo.dir = half4(normalize(vi.tangent.xy), vi.tangent.xy);
				return vo;
			}

			PsOutput frag (v2f vo)
			{
				half2 s, c;
				sincos(vo.uv.xy, s, c);

				half fade = max(0, 1.0 - pow(max(vo.uv.z, vo.uv.w), 6));

				half displacement = c.y * s.x * s.y * vo.amplitude * fade;
				half height = s.x * s.y * vo.amplitude * fade;

				PsOutput po;
				po.displacement = half4(vo.dir.x * displacement, height, vo.dir.y * displacement, 0);
				po.slope = half4(vo.dir.z * displacement, vo.dir.w * displacement, 0, 0) * 0.0005;

				return po;
			}
			ENDCG
		}
	}
}
