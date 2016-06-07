Shader "PlayWay Water/Volumes/Front (Additive)"
{
	Properties
	{
		
	}
	
	SubShader
	{
		Tags{ "CustomType" = "WaterVolume" }

		Pass
		{
			Cull Back
			ZTest Less
			ZWrite On
			ColorMask R

			CGPROGRAM
			#pragma target 2.0

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			
			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex		: SV_POSITION;
				float2 depth		: TEXCOORD0;
			};

			inline half LinearEyeDepthHalf(half z)
			{
				return 1.0 / (_ZBufferParams.z * z + _ZBufferParams.w);
			}

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
				o.depth = o.vertex.zw;
				return o;
			}

			half4 frag(v2f i) : SV_Target
			{
				return half4(LinearEyeDepthHalf(i.depth.x / i.depth.y), 0.0, 0.0, 0.0);
			}
			ENDCG
		}
	}
}
