#include "UnityCG.cginc"
#include "UnityStandardCore.cginc"

struct v2f
{
	float4 pos			: SV_POSITION;
	float2 depth		: TEXCOORD0;
	half4 screenPos		: TEXCOORD1;
#if _CLIP_ABOVE
	float3 worldPos		: TEXCOORD2;
#endif
};

struct VertexInput2
{
	float4 vertex	: POSITION;
};

v2f vert(VertexInput2 vi)
{
	v2f o;

	float4 posWorld = GET_WORLD_POS(vi.vertex);

	half2 normal;
	float4 fftUV;
	float4 fftUV2;
	float3 displacement;
	TransformVertex(posWorld, normal, fftUV, fftUV2, displacement);

	o.pos = mul(UNITY_MATRIX_VP, posWorld);
	o.screenPos = ComputeScreenPos(o.pos);
	o.depth = o.pos.zw;

#if _CLIP_ABOVE
	o.worldPos = posWorld.xyz;
#endif

	return o;
}

float4 frag(v2f i) : SV_Target
{
	float depth = i.depth.x / i.depth.y;

	half alpha;
#if _CLIP_ABOVE
	MaskWater(alpha, i.screenPos, i.worldPos);
#else
	MaskWater(alpha, i.screenPos, 0);
#endif

	clip(alpha - 0.006);

	return depth;
}