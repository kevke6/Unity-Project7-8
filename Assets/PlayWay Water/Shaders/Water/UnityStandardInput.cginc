#ifndef UNITY_STANDARD_INPUT_INCLUDED
#define UNITY_STANDARD_INPUT_INCLUDED

#include "UnityCG.cginc"
#include "UnityShaderVariables.cginc"
#include "UnityStandardConfig.cginc"
#include "UnityPBSLighting.cginc" // TBD: remove
#include "UnityStandardUtils.cginc"

//---------------------------------------
// Directional lightmaps & Parallax require tangent space too
#if (_NORMALMAP || !DIRLIGHTMAP_OFF || _PARALLAXMAP)
	#define _TANGENT_TO_WORLD 1 
#endif

#if (_DETAIL_MULX2 || _DETAIL_MUL || _DETAIL_ADD || _DETAIL_LERP)
	#define _DETAIL 1
#endif

//---------------------------------------
half4		_Color;
half		_Cutoff;

sampler2D	_DetailAlbedoMap;
float4		_DetailAlbedoMap_ST;

sampler2D	_BumpMap;
float4		_BumpMap_ST;
half3		_BumpScale;

sampler2D	_DetailMask;
sampler2D	_DetailNormalMap;
half		_DetailNormalMapScale;

sampler2D	_SpecGlossMap;
sampler2D	_MetallicGlossMap;
half		_Metallic;
half		_Glossiness;

sampler2D	_OcclusionMap;
half		_OcclusionStrength;

half		_UVSec;

half4 		_EmissionColor;
sampler2D	_EmissionMap;

//-------------------------------------------------------------------------------------
// Input functions

struct VertexInput
{
	float4 vertex	: POSITION;
	half3 normal	: NORMAL;
#if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
	half2 uv2		: TEXCOORD2;
#endif
};

half4 TexCoords(half2 uv1, half2 uv2)
{
	half4 texcoord;
	texcoord.xy = TRANSFORM_TEX(uv1, _BumpMap); // Always source from uv0
	texcoord.zw = TRANSFORM_TEX(uv2, _DetailAlbedoMap);
	return texcoord;
}		

half DetailMask(half2 uv)
{
	return tex2D (_DetailMask, uv).a;
}

half3 Albedo(half4 texcoords)
{
	half3 albedo = _Color.rgb;

	// water: removed diffuse detail map sampling

	return albedo;
}

half Alpha(half2 uv)
{
	return _Color.a;
}		

half Occlusion(half2 uv)
{
#if (SHADER_TARGET < 30)
	// SM20: instruction count limitation
	// SM20: simpler occlusion
	return tex2D(_OcclusionMap, uv).g;
#else
	half occ = tex2D(_OcclusionMap, uv).g;
	return LerpOneTo (occ, _OcclusionStrength);
#endif
}

half4 SpecularGloss(half2 uv)
{
	half4 sg;
#ifdef _SPECGLOSSMAP
	sg = tex2D(_SpecGlossMap, uv.xy);
#else
	sg = half4(_SpecColor.rgb, _Glossiness);
#endif
	return sg;
}

half2 MetallicGloss(half2 uv)
{
	half2 mg;
#ifdef _METALLICGLOSSMAP
	mg = tex2D(_MetallicGlossMap, uv.xy).ra;
#else
	mg = half2(_Metallic, _Glossiness);
#endif
	return mg;
}

half3 Emission(half2 uv)
{
#ifndef _EMISSION
	return 0;
#else
	return tex2D(_EmissionMap, uv).rgb * _EmissionColor.rgb;
#endif
}

half2 NormalInTangentSpace(half4 texcoords, half3 i_posWorld, half3 eyeVec, half2 localMapsUv, half2 gerstner)
{
#ifdef _NORMALMAP
#if defined(UNITY_NO_DXT5nm)
	half2 normalTangent = tex2D(_BumpMap, texcoords.xy).xy * _BumpScale.x + tex2D(_BumpMap, texcoords.zw).xy * _BumpScale.y + _BumpScale.z;
#else
	half4 normalTangent = tex2D(_BumpMap, texcoords.xy) * _BumpScale.x + tex2D(_BumpMap, texcoords.zw) * _BumpScale.y;
	normalTangent.xy = normalTangent.wy + _BumpScale.z;
#endif
#else
	half2 normalTangent = 0;
#endif

#if _WAVES_FFT_SLOPE
	half4 uv1 = globalWaterData.fftUV.xyxy * _WaterTileSizeScales.yyzz;
	fixed2 fftWavesNormal = tex2D(_GlobalNormalMap, globalWaterData.fftUV).xy * globalWaterData.totalMask.x;
	fftWavesNormal += tex2D(_GlobalNormalMap, globalWaterData.fftUV2).zw * globalWaterData.totalMask.y;
	fftWavesNormal += tex2D(_GlobalNormalMap1, uv1.xy).xy * globalWaterData.totalMask.z;
	fftWavesNormal += tex2D(_GlobalNormalMap1, uv1.zw).zw;
	normalTangent.xy += fftWavesNormal * _DisplacementNormalsIntensity;
#endif

#if _WAVES_GERSTNER
	normalTangent.xy += gerstner;
#endif

#if _WATER_OVERLAYS
	half2 overlayNormal = tex2D(_LocalNormalMap, localMapsUv).xy;
	normalTangent.xy += overlayNormal * _DisplacementNormalsIntensity;
#endif

	return normalTangent.xy;
}

half4 Parallax (half4 texcoords, half3 viewDir)
{
#if !defined(_PARALLAXMAP) || (SHADER_TARGET < 30)
	// SM20: instruction count limitation
	// SM20: no parallax
	return texcoords;
#else
	half h = tex2D (_ParallaxMap, texcoords.xy).g;
	float2 offset = ParallaxOffset1Step (h, _Parallax, viewDir);
	return float4(texcoords.xy + offset, texcoords.zw + offset);
#endif
}
			
#endif // UNITY_STANDARD_INPUT_INCLUDED
