using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace PlayWay.Water
{
	/// <summary>
	/// Main water component.
	/// </summary>
	[ExecuteInEditMode]
	[AddComponentMenu("Water/Water (Base Component)", -1)]
	public class Water : MonoBehaviour, IShaderCollectionClient
	{
		[SerializeField]
		private WaterProfile profile;

		[SerializeField]
		private Shader waterShader;

		[SerializeField]
		private Shader waterVolumeShader;

		[SerializeField]
		private bool refraction = true;

		[SerializeField]
		private bool blendEdges = true;

		[SerializeField]
		private bool volumetricLighting = true;

		[Tooltip("Affects direct light specularity and diffuse (mainly foam).")]
		[SerializeField]
		private bool receiveShadows;

		[SerializeField]
		private ShaderCollection shaderCollection;

		[SerializeField]
		private ShadowCastingMode shadowCastingMode;

		[SerializeField]
		private bool useCubemapReflections = true;

		[Tooltip("Set it to anything else than 0 if your game has multiplayer functionality or you want your water to behave the same way each time your game is played (good for intro etc.).")]
		[SerializeField]
		private int seed = 0;

		[Tooltip("May hurt performance on some systems.")]
		[Range(0.0f, 1.0f)]
		[SerializeField]
		private float tesselationFactor = 1.0f;

		[SerializeField]
		private WaterUvAnimator uvAnimator;

		[SerializeField]
		private WaterVolume volume;

		[SerializeField]
		private WaterGeometry geometry;

		[SerializeField]
		private WaterRenderer waterRenderer;

		[SerializeField]
		private WaterEvent profilesChanged;

		[SerializeField]
		private Material waterMaterialPrefab;

		[SerializeField]
		private Material waterVolumeMaterialPrefab;

#if UNITY_EDITOR
#pragma warning disable 0414
		[SerializeField]
		private float version = 1.0f;
#pragma warning restore 0414

		// used to identify this water object for the purpose of shader collection building
		[SerializeField]
		private int sceneHash = -1;

		private int instanceId = -1;
#endif

		private WeightedProfile[] profiles;
		private bool profilesDirty;

		private Material waterMaterial;
		private Material waterBackMaterial;
		private Material waterVolumeMaterial;

		private float horizontalDisplacementScale;
		private float gravity;
		private float directionality;
		private float density;
		private float underwaterBlurSize;
		private float underwaterDistortionsIntensity;
		private float underwaterDistortionAnimationSpeed;
		private float time = -1;
		private Color underwaterAbsorptionColor;
		private float maxHorizontalDisplacement;
		private float maxVerticalDisplacement;
		private int surfaceOffsetId;
		private int activeSamplesCount;
		private bool firstFrame = true;
		private Vector2 surfaceOffset = new Vector2(float.NaN, float.NaN);
		private IWaterRenderAware[] renderAwareComponents;
		private IWaterDisplacements[] displacingComponents;

		static private string[] parameterNames = new string[] {
			"_AbsorptionColor", "_Color", "_SpecColor", "_DepthColor", "_EmissionColor", "_ReflectionColor", "_DisplacementsScale", "_Glossiness",
			"_SubsurfaceScatteringPack", "_WrapSubsurfaceScatteringPack", "_RefractionDistortion", "_SpecularFresnelBias", "_DetailFadeFactor",
			"_DisplacementNormalsIntensity", "_EdgeBlendFactorInv", "_PlanarReflectionPack", "_BumpScale", "_FoamTiling", "_LightSmoothnessMul",
			"_BumpMap", "_FoamTex", "_FoamNormalMap"
		};

		static private string[] disallowedVolumeKeywords = new string[] {
			"_WAVES_FFT_SLOPE", "_WAVES_GERSTNER", "_WATER_FOAM_WS", "_PLANAR_REFLECTIONS", "_PLANAR_REFLECTIONS_HQ",
			"_INCLUDE_SLOPE_VARIANCE", "_NORMALMAP", "_PROJECTION_GRID", "_WATER_OVERLAYS", "_WAVES_ALIGN", "_TRIANGLES",
			"_BOUNDED_WATER"
		};

		static private string[] hardwareDependendKeywords = new string[] {
			"_INCLUDE_SLOPE_VARIANCE", "_WATER_FOAM_WS"
		};

		private int[] parameterHashes;

		private VectorParameterOverride[] vectorOverrides;
		private ColorParameterOverride[] colorOverrides;
		private FloatParameterOverride[] floatOverrides;
		private TextureParameterOverride[] textureOverrides;

		void Awake()
		{
			bool inserted = (volume == null);

			CreateWaterManagers();

			if(inserted)
			{
#if UNITY_EDITOR
				// add default components only in editor, out of play mode
				if(!Application.isPlaying)
					AddDefaultComponents();

				version = WaterProjectSettings.CurrentVersion;
#endif
			}

			CreateParameterHashes();
			renderAwareComponents = GetComponents<IWaterRenderAware>();
			displacingComponents = GetComponents<IWaterDisplacements>();

			if(!Application.isPlaying)
				return;

			if(profile == null)
			{
				Debug.LogError("Water profile is not set. You may assign it in the inspector.");
				gameObject.SetActive(false);
				return;
			}

			try
			{
				CreateMaterials();

				if(profiles == null)
				{
					profiles = new WeightedProfile[] { new WeightedProfile(profile, 1.0f) };
					ResolveProfileData(profiles);
				}

				uvAnimator.Start(this);
				profilesChanged.AddListener(OnProfilesChanged);
			}
			catch(System.Exception e)
			{
				Debug.LogError(e);
				gameObject.SetActive(false);
			}
		}

		void Start()
		{
			SetupMaterials();

			if(profiles != null)
				ResolveProfileData(profiles);

			profilesDirty = true;
		}

		public Material WaterMaterial
		{
			get
			{
				if(waterMaterial == null)
					CreateMaterials();

				return waterMaterial;
			}
		}

		public Material WaterBackMaterial
		{
			get
			{
				if(waterBackMaterial == null)
					CreateMaterials();

				return waterBackMaterial;
			}
		}

		public Material WaterVolumeMaterial
		{
			get
			{
				if(waterVolumeMaterial == null)
					CreateMaterials();

				return waterVolumeMaterial;
			}
		}

		/// <summary>
		/// Currently set water profiles with their associated weights.
		/// </summary>
		public WeightedProfile[] Profiles
		{
			get { return profiles; }
		}

		public float HorizontalDisplacementScale
		{
			get { return horizontalDisplacementScale; }
		}

		public bool ReceiveShadows
		{
			get { return receiveShadows; }
		}

		public ShadowCastingMode ShadowCastingMode
		{
			get { return shadowCastingMode; }
		}

		public float Gravity
		{
			get { return gravity; }
		}

		public float Directionality
		{
			get { return directionality; }
		}

		public Color UnderwaterAbsorptionColor
		{
			get { return underwaterAbsorptionColor; }
		}

		public bool VolumetricLighting
		{
			get { return volumetricLighting; }
		}

		public bool FinalVolumetricLighting
		{
			get { return volumetricLighting && WaterQualitySettings.Instance.AllowVolumetricLighting; }
		}

		/// <summary>
		/// Count of WaterSample instances targetted on this water.
		/// </summary>
		public int ComputedSamplesCount
		{
			get { return activeSamplesCount; }
		}

		/// <summary>
		/// Event invoked when profiles change.
		/// </summary>
		public WaterEvent ProfilesChanged
		{
			get { return profilesChanged; }
		}

		/// <summary>
		/// Retrieves a WaterVolume of this water. It's one of the classes providing basic water functionality.
		/// </summary>
		public WaterVolume Volume
		{
			get { return volume; }
		}

		/// <summary>
		/// Retrieves a WaterGeometry of this water. It's one of the classes providing basic water functionality.
		/// </summary>
		public WaterGeometry Geometry
		{
			get { return geometry; }
		}

		/// <summary>
		/// Retrieves a WaterRenderer of this water. It's one of the classes providing basic water functionality.
		/// </summary>
		public WaterRenderer Renderer
		{
			get { return waterRenderer; }
		}

		public int Seed
		{
			get { return seed; }
			set { seed = value; }
		}

		public float Density
		{
			get { return density; }
		}

		public float UnderwaterBlurSize
		{
			get { return underwaterBlurSize; }
		}

		public float UnderwaterDistortionsIntensity
		{
			get { return underwaterDistortionsIntensity; }
		}

		public float UnderwaterDistortionAnimationSpeed
		{
			get { return underwaterDistortionAnimationSpeed; }
		}

		public ShaderCollection ShaderCollection
		{
			get { return shaderCollection; }
		}

		public float MaxHorizontalDisplacement
		{
			get { return maxHorizontalDisplacement; }
		}

		public float MaxVerticalDisplacement
		{
			get { return maxVerticalDisplacement; }
		}

		public float Time
		{
			get { return time == -1 ? UnityEngine.Time.time : time; }
			set { time = value; }
		}

		public Vector2 SurfaceOffset
		{
			get { return float.IsNaN(surfaceOffset.x) ? new Vector2(-transform.position.x, -transform.position.z) : surfaceOffset; }
			set { surfaceOffset = value; }
		}

		void OnEnable()
		{
			CreateParameterHashes();
			ValidateShaders();

#if UNITY_EDITOR
			if(!IsNotCopied())
				shaderCollection = null;

			instanceId = GetInstanceID();
#else
			if(Application.isPlaying)
				shaderCollection = null;
#endif

			CreateMaterials();

			if(profiles == null && profile != null)
			{
				profiles = new WeightedProfile[] { new WeightedProfile(profile, 1.0f) };
				ResolveProfileData(profiles);
			}

			WaterQualitySettings.Instance.Changed -= OnQualitySettingsChanged;
			WaterQualitySettings.Instance.Changed += OnQualitySettingsChanged;

			WaterGlobals.Instance.AddWater(this);

			if(geometry != null)
			{
				geometry.OnEnable(this);
				waterRenderer.OnEnable(this);
				volume.OnEnable(this);
			}

			//if(Application.isPlaying)
			//	SetupMaterials();
		}

		void OnDisable()
		{
			WaterGlobals.Instance.RemoveWater(this);

			geometry.OnDisable();
			waterRenderer.OnDisable();
			volume.OnDisable();
		}

		void OnDestroy()
		{
			WaterQualitySettings.Instance.Changed -= OnQualitySettingsChanged;
		}

		/// <summary>
		/// Computes water displacement vector at a given coordinates. WaterSample class does the same thing asynchronously and is recommended.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="z"></param>
		/// <param name="spectrumStart"></param>
		/// <param name="spectrumEnd"></param>
		/// <param name="time"></param>
		/// <returns></returns>
		public Vector3 GetDisplacementAt(float x, float z, float spectrumStart, float spectrumEnd, float time, ref bool completed)
		{
			Vector3 result = new Vector3();

			for(int i = 0; i < displacingComponents.Length; ++i)
				result += displacingComponents[i].GetDisplacementAt(x, z, spectrumStart, spectrumEnd, time, ref completed);

			return result;
		}

		/// <summary>
		/// Computes horizontal displacement vector at a given coordinates. WaterSample class does the same thing asynchronously and is recommended.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="z"></param>
		/// <param name="spectrumStart"></param>
		/// <param name="spectrumEnd"></param>
		/// <param name="time"></param>
		/// <returns></returns>
		public Vector2 GetHorizontalDisplacementAt(float x, float z, float spectrumStart, float spectrumEnd, float time, ref bool completed)
		{
			Vector2 result = new Vector2();

			for(int i = 0; i < displacingComponents.Length; ++i)
				result += displacingComponents[i].GetHorizontalDisplacementAt(x, z, spectrumStart, spectrumEnd, time, ref completed);

			return result;
		}

		/// <summary>
		/// Computes height at a given coordinates. WaterSample class does the same thing asynchronously and is recommended.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="z"></param>
		/// <param name="spectrumStart"></param>
		/// <param name="spectrumEnd"></param>
		/// <param name="time"></param>
		/// <returns></returns>
		public float GetHeightAt(float x, float z, float spectrumStart, float spectrumEnd, float time, ref bool completed)
		{
			float result = 0.0f;

			for(int i = 0; i < displacingComponents.Length; ++i)
				result += displacingComponents[i].GetHeightAt(x, z, spectrumStart, spectrumEnd, time, ref completed);

			return result;
		}

		/// <summary>
		/// Computes forces and height at a given coordinates. WaterSample class does the same thing asynchronously and is recommended.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="z"></param>
		/// <param name="spectrumStart"></param>
		/// <param name="spectrumEnd"></param>
		/// <param name="time"></param>
		/// <returns></returns>
		public Vector4 GetHeightAndForcesAt(float x, float z, float spectrumStart, float spectrumEnd, float time, ref bool completed)
		{
			Vector4 result = Vector4.zero;

			for(int i = 0; i < displacingComponents.Length; ++i)
				result += displacingComponents[i].GetForceAndHeightAt(x, z, spectrumStart, spectrumEnd, time, ref completed);

			return result;
		}

		/// <summary>
		/// Caches profiles for later use to avoid hiccups.
		/// </summary>
		/// <param name="profiles"></param>
		public void CacheProfiles(params WaterProfile[] profiles)
		{
			var windWaves = GetComponent<WindWaves>();

			if(windWaves != null)
			{
				foreach(var profile in profiles)
					windWaves.SpectrumResolver.CacheSpectrum(profile.Spectrum);
			}
		}

		public void SetProfiles(params WeightedProfile[] profiles)
		{
			ValidateProfiles(profiles);

			this.profiles = profiles;
			profilesDirty = true;
		}

		public void InvalidateMaterialKeywords()
		{

		}

		private void CreateMaterials()
		{
			if(waterMaterial == null)
			{
				if(waterMaterialPrefab == null)
					waterMaterial = new Material(waterShader);
				else
					waterMaterial = Instantiate(waterMaterialPrefab);

				waterMaterial.hideFlags = HideFlags.DontSave;
			}

			if(waterBackMaterial == null)
			{
				if(waterMaterialPrefab == null)
					waterBackMaterial = new Material(waterShader);
				else
					waterBackMaterial = Instantiate(waterMaterialPrefab);

				waterBackMaterial.hideFlags = HideFlags.DontSave;
				UpdateBackMaterial();
			}

			if(waterVolumeMaterial == null)
			{
				if(waterVolumeMaterialPrefab == null)
					waterVolumeMaterial = new Material(waterVolumeShader);
				else
					waterVolumeMaterial = Instantiate(waterVolumeMaterialPrefab);

				waterVolumeMaterial.hideFlags = HideFlags.DontSave;
				UpdateWaterVolumeMaterial();
			}
		}

		private void SetupMaterials()
		{
			var waterQualitySettings = WaterQualitySettings.Instance;

			// front and back material
			var variant = new ShaderVariant();
			BuildShaderVariant(variant, waterQualitySettings.CurrentQualityLevel);
			
			ValidateShaderCollection(variant);

			if(shaderCollection != null)
				waterMaterial.shader = shaderCollection.GetShaderVariant(variant.GetWaterKeywords(), variant.GetUnityKeywords(), variant.GetKeywordsString(), false);
			else
				waterMaterial.shader = ShaderCollection.GetRuntimeShaderVariant(variant.GetKeywordsString(), false);

			waterMaterial.shaderKeywords = variant.GetUnityKeywords();
			UpdateMaterials();
			UpdateBackMaterial();

			// volume material
			foreach(string keyword in disallowedVolumeKeywords)
				variant.SetWaterKeyword(keyword, false);
			
			if(shaderCollection != null)
				waterVolumeMaterial.shader = shaderCollection.GetShaderVariant(variant.GetWaterKeywords(), variant.GetUnityKeywords(), variant.GetKeywordsString(), true);
			else
				waterVolumeMaterial.shader = ShaderCollection.GetRuntimeShaderVariant(variant.GetKeywordsString(), true);
			
			UpdateWaterVolumeMaterial();
			waterVolumeMaterial.shaderKeywords = variant.GetUnityKeywords();
		}

		private void SetShader(ref Material material, Shader shader)
		{

		}

		private void ValidateShaderCollection(ShaderVariant variant)
		{
#if UNITY_EDITOR
			if(!Application.isPlaying && shaderCollection != null && !shaderCollection.ContainsShaderVariant(variant.GetKeywordsString()))
				RebuildShaders();
#endif
		}

		[ContextMenu("Rebuild Shaders")]
		private void RebuildShaders()
		{
#if UNITY_EDITOR
			if(shaderCollection == null)
			{
				Debug.LogError("You have to create a shader collection first.");
				return;
			}

			shaderCollection.Build();
#endif
		}

		private void UpdateBackMaterial()
		{
			if(waterBackMaterial != null)
			{
				waterBackMaterial.shader = waterMaterial.shader;
				waterBackMaterial.CopyPropertiesFromMaterial(waterMaterial);
				waterBackMaterial.EnableKeyword("_WATER_BACK");
				waterBackMaterial.SetFloat("_Cull", 1);
			}
		}

		private void UpdateWaterVolumeMaterial()
		{
			if(waterVolumeMaterial != null)
			{
				waterVolumeMaterial.CopyPropertiesFromMaterial(waterMaterial);
				waterVolumeMaterial.renderQueue = (refraction || blendEdges) ? 2991 : 2000;
			}
		}

		public void SetVector(int materialPropertyId, Vector4 vector)
		{
			waterMaterial.SetVector(materialPropertyId, vector);
			waterBackMaterial.SetVector(materialPropertyId, vector);
			waterVolumeMaterial.SetVector(materialPropertyId, vector);
		}

		public void SetFloat(int materialPropertyId, float value)
		{
			waterMaterial.SetFloat(materialPropertyId, value);
			waterBackMaterial.SetFloat(materialPropertyId, value);
			waterVolumeMaterial.SetFloat(materialPropertyId, value);
		}

		public bool SetKeyword(string keyword, bool enable)
		{
			if(waterMaterial != null)
			{
				if(enable)
				{
					if(!waterMaterial.IsKeywordEnabled(keyword))
					{
						waterMaterial.EnableKeyword(keyword);
						waterBackMaterial.EnableKeyword(keyword);
						waterVolumeMaterial.EnableKeyword(keyword);
						return true;
					}
				}
				else
				{
					if(waterMaterial.IsKeywordEnabled(keyword))
					{
						waterMaterial.DisableKeyword(keyword);
						waterBackMaterial.DisableKeyword(keyword);
						waterVolumeMaterial.DisableKeyword(keyword);
						return true;
					}
				}
			}

			return false;
		}

		public void OnValidate()
		{
			ValidateShaders();

			renderAwareComponents = GetComponents<IWaterRenderAware>();
			displacingComponents = GetComponents<IWaterDisplacements>();
			gameObject.layer = 4;

			if(waterMaterial == null)
				return;                 // wait for OnEnable

			CreateParameterHashes();

			if(profiles != null && profiles.Length != 0)
				ResolveProfileData(profiles);
			else if(profile != null)
				ResolveProfileData(new WeightedProfile[] { new WeightedProfile(profile, 1.0f) });

			geometry.OnValidate(this);
			waterRenderer.OnValidate(this);

			SetupMaterials();
		}

		private void ValidateShaders()
		{
			if(waterShader == null)
				waterShader = Shader.Find("PlayWay Water/Standard");

			if(waterVolumeShader == null)
				waterVolumeShader = Shader.Find("PlayWay Water/Standard Volume");
		}

		private void ResolveProfileData(WeightedProfile[] profiles)
		{
			WaterProfile topProfile = profiles[0].profile;
			float topWeight = 0.0f;

			foreach(var weightedProfile in profiles)
			{
				if(topProfile == null || topWeight < weightedProfile.weight)
				{
					topProfile = weightedProfile.profile;
					topWeight = weightedProfile.weight;
				}
			}

			horizontalDisplacementScale = 0.0f;
			gravity = 0.0f;
			directionality = 0.0f;
			density = 0.0f;
			underwaterBlurSize = 0.0f;
			underwaterDistortionsIntensity = 0.0f;
			underwaterDistortionAnimationSpeed = 0.0f;
			underwaterAbsorptionColor = new Color(0.0f, 0.0f, 0.0f);

			Color absorptionColor = new Color(0.0f, 0.0f, 0.0f);
			Color diffuseColor = new Color(0.0f, 0.0f, 0.0f);
			Color specularColor = new Color(0.0f, 0.0f, 0.0f);
			Color depthColor = new Color(0.0f, 0.0f, 0.0f);
			Color emissionColor = new Color(0.0f, 0.0f, 0.0f);
			Color reflectionColor = new Color(0.0f, 0.0f, 0.0f);

			float smoothness = 0.0f;
			float ambientSmoothness = 0.0f;
			float subsurfaceScattering = 0.0f;
			float refractionDistortion = 0.0f;
			float fresnelBias = 0.0f;
			float detailFadeDistance = 0.0f;
			float displacementNormalsIntensity = 0.0f;
			float edgeBlendFactor = 0.0f;
			float directionalWrapSSS = 0.0f;
			float pointWrapSSS = 0.0f;

			Vector3 planarReflectionPack = new Vector3();
			Vector2 foamTiling = new Vector2();
			var normalMapAnimation1 = new NormalMapAnimation();
			var normalMapAnimation2 = new NormalMapAnimation();

			foreach(var weightedProfile in profiles)
			{
				var profile = weightedProfile.profile;
				float weight = weightedProfile.weight;

				horizontalDisplacementScale += profile.HorizontalDisplacementScale * weight;
				gravity -= profile.Gravity * weight;
				directionality += profile.Directionality * weight;
				density += profile.Density * weight;
				underwaterBlurSize += profile.UnderwaterBlurSize * weight;
				underwaterDistortionsIntensity += profile.UnderwaterDistortionsIntensity * weight;
				underwaterDistortionAnimationSpeed += profile.UnderwaterDistortionAnimationSpeed * weight;
				underwaterAbsorptionColor += profile.UnderwaterAbsorptionColor * weight;

				absorptionColor += profile.AbsorptionColor * weight;
				diffuseColor += profile.DiffuseColor * weight;
				specularColor += profile.SpecularColor * weight;
				depthColor += profile.DepthColor * weight;
				emissionColor += profile.EmissionColor * weight;
				reflectionColor += profile.ReflectionColor * weight;

				smoothness += profile.Smoothness * weight;
				ambientSmoothness += profile.AmbientSmoothness * weight;
				subsurfaceScattering += profile.SubsurfaceScattering * weight;
				refractionDistortion += profile.RefractionDistortion * weight;
				fresnelBias += profile.FresnelBias * weight;
				detailFadeDistance += profile.DetailFadeDistance * weight;
				displacementNormalsIntensity += profile.DisplacementNormalsIntensity * weight;
				edgeBlendFactor += profile.EdgeBlendFactor * weight;
				directionalWrapSSS += profile.DirectionalWrapSSS * weight;
				pointWrapSSS += profile.PointWrapSSS * weight;

				planarReflectionPack.x += profile.PlanarReflectionIntensity * weight;
				planarReflectionPack.y += profile.PlanarReflectionFlatten * weight;
				planarReflectionPack.z += profile.PlanarReflectionVerticalOffset * weight;

				foamTiling += profile.FoamTiling * weight;
				normalMapAnimation1 += profile.NormalMapAnimation1 * weight;
				normalMapAnimation2 += profile.NormalMapAnimation2 * weight;
			}

			var windWaves = GetComponent<WindWaves>();

			if(windWaves != null && windWaves.FinalRenderMode == WaveSpectrumRenderMode.GerstnerAndFFTSlope)
				displacementNormalsIntensity *= 0.5f;

			// apply to materials
			waterMaterial.SetColor(parameterHashes[0], absorptionColor);                    // _AbsorptionColor
			waterMaterial.SetColor(parameterHashes[1], diffuseColor);                       // _Color
			waterMaterial.SetColor(parameterHashes[2], specularColor);                      // _SpecColor
			waterMaterial.SetColor(parameterHashes[3], depthColor);                         // _DepthColor
			waterMaterial.SetColor(parameterHashes[4], emissionColor);                      // _EmissionColor
			waterMaterial.SetColor(parameterHashes[5], reflectionColor);                    // _ReflectionColor
			waterMaterial.SetFloat(parameterHashes[6], horizontalDisplacementScale);        // _DisplacementsScale

			waterMaterial.SetFloat(parameterHashes[7], ambientSmoothness);                         // _Glossiness
			waterMaterial.SetVector(parameterHashes[8], new Vector4(subsurfaceScattering, 0.15f, 1.65f, 0.0f));             // _SubsurfaceScatteringPack
			waterMaterial.SetVector(parameterHashes[9], new Vector4(directionalWrapSSS, 1.0f / (1.0f + directionalWrapSSS), pointWrapSSS, 1.0f / (1.0f + pointWrapSSS)));           // _WrapSubsurfaceScatteringPack
			waterMaterial.SetFloat(parameterHashes[10], refractionDistortion);               // _RefractionDistortion
			waterMaterial.SetFloat(parameterHashes[11], fresnelBias);                       // _SpecularFresnelBias
			waterMaterial.SetFloat(parameterHashes[12], detailFadeDistance);                // _DetailFadeFactor
			waterMaterial.SetFloat(parameterHashes[13], displacementNormalsIntensity);      // _DisplacementNormalsIntensity
			waterMaterial.SetFloat(parameterHashes[14], 1.0f / edgeBlendFactor);            // _EdgeBlendFactorInv
			waterMaterial.SetVector(parameterHashes[15], planarReflectionPack);             // _PlanarReflectionPack
			waterMaterial.SetVector(parameterHashes[16], new Vector4(normalMapAnimation1.Intensity, normalMapAnimation2.Intensity, -(normalMapAnimation1.Intensity + normalMapAnimation2.Intensity) * 0.5f, 0.0f));             // _BumpScale
			waterMaterial.SetTextureScale("_BumpMap", normalMapAnimation1.Tiling);
			waterMaterial.SetTextureScale("_DetailAlbedoMap", normalMapAnimation2.Tiling);
			waterMaterial.SetVector(parameterHashes[17], new Vector2(foamTiling.x / normalMapAnimation1.Tiling.x, foamTiling.y / normalMapAnimation1.Tiling.y));                    // _FoamTiling
			waterMaterial.SetFloat(parameterHashes[18], smoothness / ambientSmoothness);    // _LightSmoothnessMul

			waterMaterial.SetTexture(parameterHashes[19], topProfile.NormalMap);            // _BumpMap
			waterMaterial.SetTexture(parameterHashes[20], topProfile.FoamDiffuseMap);       // _FoamTex
			waterMaterial.SetTexture(parameterHashes[21], topProfile.FoamNormalMap);        // _FoamNormalMap

			uvAnimator.NormalMapAnimation1 = normalMapAnimation1;
			uvAnimator.NormalMapAnimation2 = normalMapAnimation2;

			SetKeyword("_EMISSION", emissionColor.grayscale != 0);

			UpdateBackMaterial();
			UpdateWaterVolumeMaterial();
		}

		void Update()
		{
			if(!Application.isPlaying) return;

			transform.eulerAngles = new Vector3(0.0f, transform.eulerAngles.y, 0.0f);

			UpdateStatisticalData();

			uvAnimator.Update();
			geometry.Update();

			FireEvents();

			if(firstFrame)
			{
				SetupMaterials();
				firstFrame = false;
			}

#if WATER_DEBUG
			if(Input.GetKeyDown(KeyCode.F10))
				WaterDebug.WriteAllMaps(this);
#endif
		}

		public void OnWaterRender(Camera camera)
		{
			if(!isActiveAndEnabled) return;

			Vector2 surfaceOffset2d = SurfaceOffset;
			Vector3 surfaceOffset = new Vector3(surfaceOffset2d.x, transform.position.y, surfaceOffset2d.y);
			waterMaterial.SetVector(surfaceOffsetId, surfaceOffset);
			waterBackMaterial.SetVector(surfaceOffsetId, surfaceOffset);
			waterVolumeMaterial.SetVector(surfaceOffsetId, surfaceOffset);

			foreach(var component in renderAwareComponents)
			{
				if(((MonoBehaviour)component) != null && ((MonoBehaviour)component).enabled)
					component.OnWaterRender(camera);
			}
		}

		public void OnWaterPostRender(Camera camera)
		{
			foreach(var component in renderAwareComponents)
			{
				if(((MonoBehaviour)component) != null && ((MonoBehaviour)component).enabled)
					component.OnWaterPostRender(camera);
			}
		}

		internal void OnSamplingStarted()
		{
			++activeSamplesCount;
		}

		internal void OnSamplingStopped()
		{
			--activeSamplesCount;
		}

		private void AddDefaultComponents()
		{
			if(GetComponent<WaterPlanarReflection>() == null)
				gameObject.AddComponent<WaterPlanarReflection>();

			if(GetComponent<WindWaves>() == null)
				gameObject.AddComponent<WindWaves>();

			if(GetComponent<WaterFoam>() == null)
				gameObject.AddComponent<WaterFoam>();
		}

		private bool IsNotCopied()
		{
#if UNITY_EDITOR
#if UNITY_5_2 || UNITY_5_1 || UNITY_5_0
			if(string.IsNullOrEmpty(UnityEditor.EditorApplication.currentScene))
#else
			if(!gameObject.scene.path.StartsWith("Assets"))         // check if that's not a temporary scene used for a build
#endif
				return true;

#if UNITY_5_2 || UNITY_5_1 || UNITY_5_0
			string sceneName = UnityEditor.EditorApplication.currentScene + "#" + name;
#else
			string sceneName = gameObject.scene.name;
#endif

			var md5 = System.Security.Cryptography.MD5.Create();
			var hash = md5.ComputeHash(System.Text.Encoding.ASCII.GetBytes(sceneName));
			return instanceId == GetInstanceID() || sceneHash == System.BitConverter.ToInt32(hash, 0);
#else
			return true;
#endif
		}

		private void OnQualitySettingsChanged()
		{
			OnValidate();
			profilesDirty = true;
		}

		private void FireEvents()
		{
			if(profilesDirty)
			{
				profilesDirty = false;
				profilesChanged.Invoke(this);
			}
		}

		void OnProfilesChanged(Water water)
		{
			ResolveProfileData(profiles);
		}

		private void ValidateProfiles(WeightedProfile[] profiles)
		{
			if(profiles.Length == 0)
				throw new System.ArgumentException("Water has to use at least one profile.");

			float tileSize = profiles[0].profile.TileSize;

			for(int i = 1; i < profiles.Length; ++i)
			{
				if(profiles[i].profile.TileSize != tileSize)
				{
					Debug.LogError("TileSize varies between used water profiles. It is the only parameter that you should keep equal on all profiles used at a time.");
					break;
				}
			}
		}

		private void CreateParameterHashes()
		{
			if(parameterHashes != null)
				return;

			surfaceOffsetId = Shader.PropertyToID("_SurfaceOffset");

			int numParameters = parameterNames.Length;
			parameterHashes = new int[numParameters];

			for(int i = 0; i < numParameters; ++i)
				parameterHashes[i] = Shader.PropertyToID(parameterNames[i]);
		}

		private void BuildShaderVariant(ShaderVariant variant, WaterQualityLevel qualityLevel)
		{
			if(waterRenderer == null)
				return;             // still not properly initialized

			bool blendEdges = this.blendEdges && qualityLevel.allowAlphaBlending;
			bool refraction = this.refraction && qualityLevel.allowAlphaBlending;
			bool alphaBlend = (refraction || blendEdges);

			foreach(var component in renderAwareComponents)
				component.BuildShaderVariant(variant, this, qualityLevel);

			variant.SetWaterKeyword("_WATER_REFRACTION", refraction);
			variant.SetWaterKeyword("_VOLUMETRIC_LIGHTING", volumetricLighting && qualityLevel.allowVolumetricLighting);
			variant.SetWaterKeyword("_CUBEMAP_REFLECTIONS", useCubemapReflections);
			variant.SetWaterKeyword("_NORMALMAP", waterMaterial.GetTexture("_BumpMap") != null);

			//variant.SetWaterKeyword("_ALPHATEST_ON", false);
			variant.SetWaterKeyword("_ALPHABLEND_ON", alphaBlend);
			variant.SetWaterKeyword("_ALPHAPREMULTIPLY_ON", !alphaBlend);

			variant.SetUnityKeyword("_BOUNDED_WATER", !volume.Boundless && volume.HasAdditiveMasks);
			variant.SetUnityKeyword("_TRIANGLES", geometry.Triangular);
		}

		private void UpdateMaterials()
		{
			var qualityLevel = WaterQualitySettings.Instance.CurrentQualityLevel;

			foreach(var component in renderAwareComponents)
				component.UpdateMaterial(this, qualityLevel);

			bool blendEdges = this.blendEdges && qualityLevel.allowAlphaBlending;
			bool refraction = this.refraction && qualityLevel.allowAlphaBlending;
			bool alphaBlend = (refraction || blendEdges);

			waterMaterial.SetFloat("_Cull", 2);

			waterMaterial.SetOverrideTag("RenderType", alphaBlend ? "Transparent" : "Opaque");
			waterMaterial.SetFloat("_Mode", alphaBlend ? 2 : 0);
			waterMaterial.SetInt("_SrcBlend", (int)(alphaBlend ? BlendMode.SrcAlpha : BlendMode.One));
			waterMaterial.SetInt("_DstBlend", (int)(alphaBlend ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));

			waterMaterial.renderQueue = alphaBlend ? 2990 : 2000;       // 2000 - geometry, 3000 - transparent

			float maxTesselationFactor = Mathf.Sqrt(2000000.0f / geometry.TesselatedBaseVertexCount);
			waterMaterial.SetFloat("_TesselationFactor", Mathf.Lerp(1.0f, maxTesselationFactor, Mathf.Min(tesselationFactor, qualityLevel.maxTesselationFactor)));
		}

		private void AddShaderVariants(ShaderCollection collection)
		{
			foreach(var qualityLevel in WaterQualitySettings.Instance.GetQualityLevelsDirect())
			{
				var variant = new ShaderVariant();

				// main shader
				BuildShaderVariant(variant, qualityLevel);

				collection.GetShaderVariant(variant.GetWaterKeywords(), variant.GetUnityKeywords(), variant.GetKeywordsString(), false);

				AddFallbackVariants(variant, collection, false, 0);

				// volume shader
				foreach(string keyword in disallowedVolumeKeywords)
					variant.SetWaterKeyword(keyword, false);

				collection.GetShaderVariant(variant.GetWaterKeywords(), variant.GetUnityKeywords(), variant.GetKeywordsString(), true);

				AddFallbackVariants(variant, collection, true, 0);
			}
		}

		private void AddFallbackVariants(ShaderVariant variant, ShaderCollection collection, bool volume, int index)
		{
			if(index < hardwareDependendKeywords.Length)
			{
				string keyword = hardwareDependendKeywords[index];

				AddFallbackVariants(variant, collection, volume, index + 1);

				if(variant.IsWaterKeywordEnabled(keyword))
				{
					variant.SetWaterKeyword(keyword, false);
					AddFallbackVariants(variant, collection, volume, index + 1);
					variant.SetWaterKeyword(keyword, true);
				}
			}
			else
			{
				collection.GetShaderVariant(variant.GetWaterKeywords(), variant.GetUnityKeywords(), variant.GetKeywordsString(), volume);
			}
		}

		private void CreateWaterManagers()
		{
			if(uvAnimator == null)
				uvAnimator = new WaterUvAnimator();

			if(volume == null)
				volume = new WaterVolume();

			if(geometry == null)
				geometry = new WaterGeometry();

			if(waterRenderer == null)
				waterRenderer = new WaterRenderer();

			if(profilesChanged == null)
				profilesChanged = new WaterEvent();
		}

		public void Write(ShaderCollection collection)
		{
			if(collection == shaderCollection && waterMaterial != null)
				AddShaderVariants(collection);
		}

		private void UpdateStatisticalData()
		{
			maxHorizontalDisplacement = 0.0f;
			maxVerticalDisplacement = 0.0f;

			foreach(var displacingComponent in displacingComponents)
			{
				maxHorizontalDisplacement += displacingComponent.MaxHorizontalDisplacement;
				maxVerticalDisplacement += displacingComponent.MaxVerticalDisplacement;
			}
		}

		[ContextMenu("Print Used Keywords")]
		protected void PrintUsedKeywords()
		{
			Debug.Log(waterMaterial.shader.name);
		}

		[System.Serializable]
		public class WaterEvent : UnityEvent<Water> { };

		public struct WeightedProfile
		{
			public WaterProfile profile;
			public float weight;

			public WeightedProfile(WaterProfile profile, float weight)
			{
				this.profile = profile;
				this.weight = weight;
			}
		}

		public struct VectorParameterOverride
		{
			public int hash;
			public Vector4 value;
		}

		public struct ColorParameterOverride
		{
			public int hash;
			public Color value;
		}

		public struct FloatParameterOverride
		{
			public int hash;
			public float value;
		}

		public struct TextureParameterOverride
		{
			public int hash;
			public Texture value;
		}

		public enum ColorParameters
		{
			AbsorptionColor = 0,
			DiffuseColor = 1,
			SpecularColor = 2,
			DepthColor = 3,
			EmissionColor = 4,
			ReflectionColor = 5
		}

		public enum FloatParameters
		{
			DisplacementScale = 6,
			Glossiness = 7,
			RefractionDistortion = 10,
			SpecularFresnelBias = 11,
			DisplacementNormalsIntensity = 13,
			EdgeBlendFactorInv = 14,
			LightSmoothnessMultiplier = 18
		}

		public enum VectorParameters
		{
			SubsurfaceScatteringPack = 8,
			WrapSubsurfaceScatteringPack = 9,
			DetailFadeFactor = 12,
			PlanarReflectionPack = 15,
			BumpScale = 16,
			FoamTiling = 17
		}

		public enum TextureParameters
		{
			BumpMap = 19,
			FoamTex = 20,
			FoamNormalMap = 21
		}
	}
}
