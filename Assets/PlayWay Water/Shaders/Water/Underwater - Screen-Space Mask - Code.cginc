#include "UnityCG.cginc"
#include "UnityStandardCore.cginc"

struct VertexInput2
{
	float4 vertex	: POSITION;
};

struct VertexOutput
{
	float4 pos			: SV_POSITION;
};

VertexOutput vert(VertexInput2 vi)
{
	VertexOutput vo;

	float4 posWorld = GET_WORLD_POS(vi.vertex);

	half2 normal;
	float4 fftUV;
	float4 fftUV2;
	float3 displacement;
	TransformVertex(posWorld, normal, fftUV, fftUV2, displacement);

	vo.pos = mul(UNITY_MATRIX_VP, posWorld);

	return vo;
}

fixed4 maskFrag(VertexOutput vo) : SV_Target
{
	return 1.0 / 255.0;
}
