Shader "PlayWay Water/Volumes/Back"
{
	Properties
	{
		_Id("Id", Float) = 1
	}
		SubShader
	{
		Tags{ "CustomType" = "WaterVolume" }

		Pass
		{
			Cull Front
			ZTest Greater
			ZWrite On
			ColorMask RGB

			CGPROGRAM
			#define _WAVES_FFT 1
			#define _VERTICAL_OFFSET 0

			#pragma target 3.0

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "../Water/WaterDisplace.cginc"

			sampler2D	_VolumesFrontDepth;
			sampler2D	_WaterDepthTexture;
			float4x4	UNITY_MATRIX_VP_INVERSE;
			float		_Id;
			float2		_VolumeChannelMask;
			
			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex		: SV_POSITION;
				float4 screenPos	: TEXCOORD0;
				float4 heightmapUv	: TEXCOORD1;
				float height		: TEXCOORD2;
			};

			inline float2 LinearEyeDepth2(float2 z)
			{
				return 1.0 / (_ZBufferParams.z * z + _ZBufferParams.w);
			}

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
				float3 posWorld = mul(_Object2World, v.vertex);
				o.height.x = posWorld.y;
				//o.heightmapUv.xy = (posWorld.xz + _LocalMapsCoords.xy) * _LocalMapsCoords.zz;
				//o.heightmapUv.zw = (_WorldSpaceCameraPos.xz + _LocalMapsCoords.xy) * _LocalMapsCoords.zz;
				o.heightmapUv.xy = posWorld.xz;
				o.heightmapUv.zw = _WorldSpaceCameraPos.xz;
				o.screenPos = ComputeScreenPos(o.vertex);
				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				float3 frontDepthPack = tex2Dproj(_VolumesFrontDepth, i.screenPos);
				float frontDepth = frontDepthPack.x;
				float frontAboveWater = frontDepthPack.z;
				float waterHeight = GetDisplacedHeight4(i.heightmapUv.xy + _SurfaceOffset.xz);

				float2 depths = LinearEyeDepth2(float2(frontDepth, i.screenPos.z / i.screenPos.w));
				float cameraDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_WaterDepthTexture, i.screenPos));

				float cameraWaterHeight = GetDisplacedHeight4(i.heightmapUv.zw + _SurfaceOffset.xz);

				if (abs(frontAboveWater - 0.5) < 0.35 || depths.x > depths.y)
					frontAboveWater = (cameraWaterHeight < _WorldSpaceCameraPos.y ? 1 : 0);

				float surfaceMask = depths.x <= cameraDepth && depths.y >= cameraDepth ? 1 : 0;
				float backfillMask = surfaceMask;

				if (cameraWaterHeight > _WorldSpaceCameraPos.y)
					backfillMask = 1.0 - backfillMask;

				if (i.height <= waterHeight)
				{
					if (cameraWaterHeight < _WorldSpaceCameraPos.y && frontAboveWater > 0.5)
						surfaceMask = 1.0;

					backfillMask = 1.0;
				}

				if (cameraWaterHeight < _WorldSpaceCameraPos.y && frontAboveWater < 0.5)
					backfillMask = 0;

				float frontfillMask = frontAboveWater < 0.5 && i.height > waterHeight ? 1 : 0;
				float surfaceMaskFinal = surfaceMask > 0.5 ? depths.y * 1.05 : 0;

				return float4(surfaceMaskFinal, backfillMask, surfaceMask > 0.5 ? 1 : 0, 0);
			}
			ENDCG
		}
	}
}
