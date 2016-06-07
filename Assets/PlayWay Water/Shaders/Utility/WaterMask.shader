Shader "PlayWay Water/Utilities/Water Mask"
{
	Properties
	{
		_Intensity ("Intensity", Float) = 1.0
	}

	CGINCLUDE
	
	#include "UnityCG.cginc"

	struct VertexInput
	{
		float4 vertex	: POSITION;
	};

	struct VertexOutput
	{
		float4 pos	: SV_POSITION;
	};

	half _Intensity;

	VertexOutput vert (VertexInput vi)
	{
		VertexOutput vo;
		vo.pos = mul(UNITY_MATRIX_MVP, vi.vertex);

		return vo;
	}

	half4 frag(VertexOutput vo) : SV_Target
	{
		return half4(1000000, 0, _Intensity, 0);
	}

	ENDCG

	SubShader
	{
		Tags { "RenderType"="Transparent" "PerformanceChecks"="False" "Queue"="Transparent" }

		Pass
		{
			ZTest Always Cull Off ZWrite Off
			Blend One One
			ColorMask RB

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			ENDCG
		}
	}
}