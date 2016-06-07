using System;
using UnityEngine;

namespace PlayWay.Water
{
	[RequireComponent(typeof(Water))]
	[RequireComponent(typeof(WindWaves))]
	[AddComponentMenu("Water/Foam", 1)]
	public class WaterFoam : MonoBehaviour, IWaterRenderAware
	{
		[HideInInspector]
		[SerializeField]
		private Shader foamSimulationShader;
		
		[Tooltip("Foam map supersampling in relation to the waves simulator resolution. Has to be a power of two (0.25, 0.5, 1, 2, etc.)")]
		[SerializeField]
		private float supersampling = 2.0f;

		private float foamIntensity = 1.0f;
		private float foamThreshold = 1.0f;
		private float foamFadingFactor = 0.85f;

		private RenderTexture foamMapA;
		private RenderTexture foamMapB;
		private Material foamSimulationMaterial;
		private Vector2 lastCameraPos;
		private Vector2 deltaPosition;
		private Water water;
		private WindWaves windWaves;
		private int resolution;
		private bool firstFrame;

		private int foamParametersId;
		private int foamIntensityId;

		void Start()
		{
			water = GetComponent<Water>();
			windWaves = GetComponent<WindWaves>();

			foamParametersId = Shader.PropertyToID("_FoamParameters");
			foamIntensityId = Shader.PropertyToID("_FoamIntensity");

			windWaves.ResolutionChanged.AddListener(OnResolutionChanged);

			resolution = Mathf.RoundToInt(windWaves.FinalResolution * supersampling);
			
			foamSimulationMaterial = new Material(foamSimulationShader);
			foamSimulationMaterial.hideFlags = HideFlags.DontSave;

			firstFrame = true;
		}

		void OnEnable()
		{
			water = GetComponent<Water>();
			water.ProfilesChanged.AddListener(OnProfilesChanged);
			OnProfilesChanged(water);
        }

		void OnDisable()
		{
			water.InvalidateMaterialKeywords();
			water.ProfilesChanged.RemoveListener(OnProfilesChanged);
		}

		public Texture FoamMap
		{
			get { return foamMapA; }
		}

		public void OnWaterRender(Camera camera)
		{

		}

		public void OnWaterPostRender(Camera camera)
		{

		}

		public void BuildShaderVariant(ShaderVariant variant, Water water, WaterQualityLevel qualityLevel)
		{
			variant.SetWaterKeyword("_WATER_FOAM_WS", enabled && CheckPreresquisites());
		}

		public void UpdateMaterial(Water water, WaterQualityLevel qualityLevel)
		{

		}

		private void SetupFoamMaterials()
		{
			if(foamSimulationMaterial != null)
			{
				float t = foamThreshold * resolution / 2048.0f * 220.0f * 0.7f;
				foamSimulationMaterial.SetVector(foamParametersId, new Vector4(foamIntensity * 0.6f, 0.0f, 0.0f, foamFadingFactor));
				foamSimulationMaterial.SetVector(foamIntensityId, new Vector4(t / windWaves.TileSizes.x, t / windWaves.TileSizes.y, t / windWaves.TileSizes.z, t / windWaves.TileSizes.w));
            }
		}

		private void SetKeyword(Material material, string name, bool val)
		{
			if(val)
				material.EnableKeyword(name);
			else
				material.DisableKeyword(name);
		}

		private void SetKeyword(Material material, int index, params string[] names)
		{
			foreach(var name in names)
				material.DisableKeyword(name);

			material.EnableKeyword(names[index]);
		}

		void OnValidate()
		{
			if(foamSimulationShader == null)
				foamSimulationShader = Shader.Find("PlayWay Water/Foam/Global");
			
			supersampling = Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(supersampling * 4096)) / 4096.0f;

			water = GetComponent<Water>();
			windWaves = GetComponent<WindWaves>();
        }
		
		private void Dispose(bool completely)
		{
			if(foamMapA != null)
			{
				Destroy(foamMapA);
				Destroy(foamMapB);

				foamMapA = null;
				foamMapB = null;
			}
		}

		void OnDestroy()
		{
			Dispose(true);
		}

		void LateUpdate()
		{
			if(!firstFrame)
				UpdateFoamMap();
			else
				firstFrame = false;

			SwapRenderTargets();
		}

		private void CheckResources()
		{
			if(foamMapA == null)
			{
				foamMapA = CreateRT(0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, FilterMode.Trilinear, TextureWrapMode.Repeat);
				foamMapB = CreateRT(0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, FilterMode.Trilinear, TextureWrapMode.Repeat);

				RenderTexture.active = null;
			}
		}
		
		private RenderTexture CreateRT(int depth, RenderTextureFormat format, RenderTextureReadWrite readWrite, FilterMode filterMode, TextureWrapMode wrapMode)
		{
			var renderTexture = new RenderTexture(resolution, resolution, depth, format, readWrite);
			renderTexture.hideFlags = HideFlags.DontSave;
			renderTexture.filterMode = filterMode;
			renderTexture.wrapMode = wrapMode;
			renderTexture.useMipMap = true;
			renderTexture.generateMips = true;

			RenderTexture.active = renderTexture;
			GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));

			return renderTexture;
		}

		private void UpdateFoamMap()
		{
			if(!CheckPreresquisites())
				return;

			CheckResources();
			SetupFoamMaterials();
			
			foamSimulationMaterial.SetTexture("_DisplacementMap0", windWaves.WaterWavesFFT.GetDisplacementMap(0));
			foamSimulationMaterial.SetTexture("_DisplacementMap1", windWaves.WaterWavesFFT.GetDisplacementMap(1));
			foamSimulationMaterial.SetTexture("_DisplacementMap2", windWaves.WaterWavesFFT.GetDisplacementMap(2));
			foamSimulationMaterial.SetTexture("_DisplacementMap3", windWaves.WaterWavesFFT.GetDisplacementMap(3));
			Graphics.Blit(foamMapA, foamMapB, foamSimulationMaterial, 0);

			water.WaterMaterial.SetTexture("_FoamMapWS", foamMapB);
		}

		private void OnResolutionChanged(WindWaves windWaves)
		{
			resolution = Mathf.RoundToInt(windWaves.FinalResolution * supersampling);

			Dispose(false);
		}

		private bool CheckPreresquisites()
		{
			return windWaves != null && windWaves.enabled && windWaves.FinalRenderMode == WaveSpectrumRenderMode.FullFFT;
		}

		private void OnProfilesChanged(Water water)
		{
			var profiles = water.Profiles;

			foamIntensity = 0.0f;
			foamThreshold = 0.0f;
			foamFadingFactor = 0.0f;

			if(profiles != null)
			{
				foreach(var weightedProfile in profiles)
				{
					var profile = weightedProfile.profile;
					float weight = weightedProfile.weight;

					foamIntensity += profile.FoamIntensity * weight;
					foamThreshold += profile.FoamThreshold * weight;
					foamFadingFactor += profile.FoamFadingFactor * weight;
				}
			}
		}

		private Vector2 RotateVector(Vector2 vec, float angle)
		{
			float s = Mathf.Sin(angle);
			float c = Mathf.Cos(angle);

			return new Vector2(c * vec.x + s * vec.y, c * vec.y + s * vec.x);
		}
		
		private void SwapRenderTargets()
		{
			var t = foamMapA;
			foamMapA = foamMapB;
			foamMapB = t;
		}
	}
}
