
struct ParticleData
{
	float3 position;
	float3 velocity;
	float2 lifetime;
	float offset;
	float maxIntensity;
};

half4 _ParticleParams;
half3 _CameraUp;
uint _ParticleOffset;

StructuredBuffer<ParticleData> _Particles : register(t0);

struct vs_out {
	float4 pos : SV_POSITION;
	half4 params : TEXCOORD0;
};

struct vs_in {
	float4 vertex : POSITION;
	uint id : SV_VertexID;
};

vs_out geomVert (vs_in vi)			// vs_out geomVert (uint id : SV_VertexID)
{
	uint particleId = vi.id + _ParticleOffset;

	vs_out o;
	o.pos = float4(_Particles[particleId].position, 1);
	o.params.xy = _Particles[particleId].lifetime.xy;
	o.params.z = _Particles[particleId].offset;
	o.params.w = _Particles[particleId].maxIntensity;
	//o.pos = vi.vertex;
	return o;
}

void MakeInput(inout VertexInput vi, float3 vertex, half2 uv, half2 uv1, half3 normal, half4 tangent, float2 particleData)
{
	vi.vertex = float4(vertex, 1.0);
	vi.normal = normal;
	vi.particleData = particleData;
	vi.uv0 = uv;
	vi.uv1 = uv1;
#ifdef _TANGENT_TO_WORLD
	vi.tangent = tangent;
#endif
}

void SpawnBillboard(float3 position, float size, half alpha, half texBlend, inout TriangleStream<VertexOutputForwardBase> outStream)
{
	half3 diff = _WorldSpaceCameraPos - position;

	//if (length(diff) < _ParticleParams.w)
	//	return;

	//texBlend = 0;		// !!!
	half frame = texBlend * 2;			// 0..2
	half frameFrac = frac(frame);		// 0..1
	half2 uvOffset = half2(step(0.5, frameFrac), frame - frameFrac) * 0.5;

	frame += 0.5;
	frame = fmod(frame, 2.0);
	frameFrac = frac(frame);
	half2 uvOffset2 = half2(step(0.5, frameFrac), frame - frameFrac) * 0.5;

	texBlend = frac(texBlend * 4);

	half3 forward = normalize(diff);
	//half3 right = cross(forward, _CameraUp);
	half3 right = cross(forward, half3(0, 1, 0));
	half3 up = cross(forward, right);
	right = cross(forward, up);

	half3 normal = forward;
	half4 tangent = half4(right, 1);
	float2 particleData = float2(alpha, texBlend);

	VertexInput vi;

	right *= size * _ParticleParams.z;
	up *= size;

	MakeInput(vi, position + (-up - right), uvOffset, uvOffset2, normal, tangent, particleData);
	outStream.Append(vertForwardBase(vi));

	MakeInput(vi, position + (-up + right), uvOffset + half2(0.5, 0), uvOffset2 + half2(0.5, 0), normal, tangent, particleData);
	outStream.Append(vertForwardBase(vi));

	MakeInput(vi, position + (up - right), uvOffset + half2(0, 0.5), uvOffset2 + half2(0, 0.5), normal, tangent, particleData);
	outStream.Append(vertForwardBase(vi));

	MakeInput(vi, position + (up + right), uvOffset + half2(0.5, 0.5), uvOffset2 + half2(0.5, 0.5), normal, tangent, particleData);
	outStream.Append(vertForwardBase(vi));

	outStream.RestartStrip();
}

[maxvertexcount(4)]
void geom (point vs_out input[1], inout TriangleStream<VertexOutputForwardBase> outStream)
{
	if(input[0].params.x <= 0)
		return;

	float size = _ParticleParams.x;
	float3 position = input[0].pos;
	half maxIntensity = input[0].params.w;

	half remainingLife = input[0].params.x;
	half lifetime = input[0].params.y - remainingLife;

	half alpha = min(sqrt(lifetime) * _ParticleParams.y, 1.0) * maxIntensity;
	half texBlend = lifetime / input[0].params.y;

	texBlend = frac(input[0].params.z + texBlend * 3);

	size *= pow(lifetime, 0.75) * maxIntensity;
	alpha *= min(1, remainingLife);
	alpha = 1;

	SpawnBillboard(position, size, alpha, texBlend, outStream);
}

fixed4 debugFrag(VertexOutputForwardBase vo) : SV_Target
{
	return 1.0;
}
