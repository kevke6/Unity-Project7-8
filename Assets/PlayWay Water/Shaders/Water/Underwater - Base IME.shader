Shader "PlayWay Water/Underwater/Base IME"
{
	Properties
	{
		_MainTex ("", 2D) = "" {}
		_DisplacedHeightMaps("", 2D) = "" {}
	}

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM

			#pragma target 3.0

			#pragma vertex vert
			#pragma fragment PropagateMask

			#include "Underwater - Base IME - Code.cginc"

			ENDCG
		}

		Pass
		{
			CGPROGRAM

			#pragma target 3.0

			#pragma vertex vert
			#pragma fragment FinishMask

			#include "Underwater - Base IME - Code.cginc"

			ENDCG
		}

		Pass
		{
			CGPROGRAM

			#pragma target 3.0

			#pragma multi_compile __ _WAVES_FFT
			#pragma multi_compile ____ _WAVES_GERSTNER

			#pragma vertex vert
			#pragma fragment ime

			#include "Underwater - Base IME - Code.cginc"

			ENDCG
		}

		Pass
		{
			CGPROGRAM

			#pragma target 3.0

			#pragma vertex vert
			#pragma fragment ime2

			#include "Underwater - Base IME - Code.cginc"

			ENDCG
		}

		Pass
		{
			CGPROGRAM

			#pragma target 3.0

			#pragma vertex vertMask
			#pragma fragment fragMask

			#include "Underwater - Base IME - Code.cginc"

			ENDCG
		}
	}

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM

			#pragma target 3.0

			#pragma vertex vert
			#pragma fragment PropagateMask

			#include "Underwater - Base IME - Code.cginc"

			ENDCG
		}

		Pass
		{
			CGPROGRAM

			#pragma target 3.0

			#pragma vertex vert
			#pragma fragment FinishMask

			#include "Underwater - Base IME - Code.cginc"

			ENDCG
		}

		Pass
		{
			CGPROGRAM

			#pragma target 3.0

			#pragma multi_compile ____ _WAVES_GERSTNER

			#pragma vertex vert
			#pragma fragment ime

			#include "Underwater - Base IME - Code.cginc"

			ENDCG
		}

		Pass
		{
			CGPROGRAM

			#pragma target 3.0

			#pragma vertex vert
			#pragma fragment ime2

			#include "Underwater - Base IME - Code.cginc"

			ENDCG
		}

		Pass
		{
			CGPROGRAM

			#pragma target 3.0

			#pragma vertex vertMask
			#pragma fragment fragMask

			#include "Underwater - Base IME - Code.cginc"

			ENDCG
		}
	}
}