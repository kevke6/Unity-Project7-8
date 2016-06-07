Shader "PlayWay Water/Foam/Local"
{
	Properties { _MainTex ("", 2D) = "" {} }

	CGINCLUDE
	
	#include "UnityCG.cginc"

	struct VertexInput2
	{
		float4 vertex	: POSITION;
		float2 uv0		: TEXCOORD0;
	};

	struct VertexOutput
	{
		float4 pos			: SV_POSITION;
		float2 uv			: TEXCOORD0;
		float2 distortionUV	: TEXCOORD1;
	};
	
	half4 _DistortionMapCoords;

	sampler2D _MainTex;			// previous foam
	sampler2D _DistortionMapB;
	sampler2D _HeightMap;

	half4 _SampleDir1;
	half2 _DeltaPosition;
	half4 _FoamParameters;		// x = intensity, y = horizonal displacement scale, z = power, w = fading factor

	VertexOutput vertBottom (VertexInput2 vi)
	{
		VertexOutput vo;

		float4 posWorld = mul(_Object2World, vi.vertex);
		vo.pos = mul(UNITY_MATRIX_VP, posWorld);
		vo.uv = vi.uv0;

		half2 deltaPosition = _DeltaPosition;

#if UNITY_UV_STARTS_AT_TOP
		deltaPosition.y = -deltaPosition.y;
#endif

		vo.distortionUV = (vi.uv0 * _DistortionMapCoords.w + _DistortionMapCoords.xy) * _DistortionMapCoords.z;

		return vo;
	}

	half ComputeFoamGain(half2 uv)
	{
		half2 displacement = tex2D(_DistortionMapB, uv);
		half3 j = half3(ddx_fine(displacement.x), ddy_fine(displacement.y), ddx_fine(displacement.y)) * _FoamParameters.y;
		j.xy += 1.0;

		half jacobian = -(j.x * j.y - j.z * j.z);
		half gain = max(0.0, jacobian + 0.94);

		return gain;
	}

	half4 fragTop (VertexOutput vo) : SV_Target
	{
		half2 foamUV = vo.uv - _SampleDir1.zw * 0.000002;				// wind
		
		half foam = tex2D(_MainTex, foamUV) * _FoamParameters.w;		// fade out

		half gain = ComputeFoamGain(vo.distortionUV) * 6;
		foam += gain * _FoamParameters.x;

		// fade near edges
		half2 k = abs(vo.uv - 0.5) * 2;
		foam *= max(0, 1.0 - pow(max(k.x, k.y), 6));

		return half4(foam, 0, 0, 1);
	}

	ENDCG

	SubShader
	{
		Tags { "RenderType"="Opaque" }

		Pass
		{
			Name "Top Pass"
			ZTest Greater Cull Off ZWrite On

			CGPROGRAM
			
			#pragma target 5.0

			#pragma vertex vertBottom
			#pragma fragment fragTop

			ENDCG
		}
	}
}
