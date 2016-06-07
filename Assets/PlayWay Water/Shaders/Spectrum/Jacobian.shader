Shader "PlayWay Water/Utility/Jacobian"
{
	Properties
	{
		_MainTex ("", 2D) = "white" {}
	}

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#pragma target 3.0

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
				o.uv = v.uv;
				return o;
			}
			
			sampler2D _MainTex;
			float _Scale;

			half4 frag (v2f vo) : SV_Target
			{
				half2 displacement = tex2D(_MainTex, vo.uv);
				half3 j = half3(ddx(displacement.x), ddy(displacement.y), ddx(displacement.y)) * _Scale;
				j.xy += 1.0;

				half jacobian = -(j.x * j.y - j.z * j.z);

				return jacobian;
			}
			ENDCG
		}
	}
}
