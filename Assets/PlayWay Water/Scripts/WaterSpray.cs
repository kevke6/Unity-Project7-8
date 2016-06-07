using UnityEngine;

namespace PlayWay.Water
{
	[RequireComponent(typeof(Water))]
	[RequireComponent(typeof(WindWaves))]
	[AddComponentMenu("Water/Spray", 1)]
	public class WaterSpray : MonoBehaviour
	{
		[HideInInspector]
		[SerializeField]
		private Shader sprayGeneratorShader;

		[HideInInspector]
		[SerializeField]
		private ComputeShader sprayControllerShader;

		[SerializeField]
		private Material sprayMaterial;

		[Range(16, 327675)]
		[SerializeField]
		private int maxParticles = 65535;
		
		private float spawnThreshold = 1.0f;
		private float spawnSkipRatio = 0.9f;
		private float scale = 1.0f;
		
		private Water water;
		private WindWaves windWaves;
		private Material sprayGeneratorMaterial;
		private Transform probeAnchor;

		private RenderTexture blankOutput;
		private ComputeBuffer particlesA;
		private ComputeBuffer particlesB;
		private ComputeBuffer particlesBInfo;
		private int resolution;
		private Mesh mesh;
		private bool supported;
		private bool resourcesReady;
		private int[] countBuffer = new int[4];
		private float finalSpawnSkipRatio;
		private float skipRatioPrecomp;
		private MaterialPropertyBlock[] propertyBlocks;

		void Start()
		{
			water = GetComponent<Water>();
			windWaves = GetComponent<WindWaves>();
			windWaves.ResolutionChanged.AddListener(OnResolutionChanged);
			
			supported = CheckSupport();

			if(!supported)
			{
				enabled = false;
				return;
			}
		}

		public int MaxParticles
		{
			get { return maxParticles; }
		}

		public int SpawnedParticles
		{
			get
			{
				if(particlesA != null)
				{
					ComputeBuffer.CopyCount(particlesA, particlesBInfo, 0);
					particlesBInfo.GetData(countBuffer);
					return countBuffer[0];
				}
				else
					return 0;
			}
		}
		
		private bool CheckSupport()
		{
			return SystemInfo.supportsComputeShaders && sprayGeneratorShader != null && sprayGeneratorShader.isSupported;
		}

		private void CheckResources()
		{
			if(sprayGeneratorMaterial == null)
			{
				sprayGeneratorMaterial = new Material(sprayGeneratorShader);
				sprayGeneratorMaterial.hideFlags = HideFlags.DontSave;
			}

			if(blankOutput == null)
			{
				UpdatePrecomputedParams();

				blankOutput = new RenderTexture(resolution, resolution, 0, SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
				blankOutput.filterMode = FilterMode.Point;
				blankOutput.Create();
			}

			if(probeAnchor == null)
			{
				var probeAnchorGo = new GameObject("Spray Probe Anchor");
				probeAnchorGo.hideFlags = HideFlags.HideAndDontSave;
				probeAnchor = probeAnchorGo.transform;
			}

			if(mesh == null)
			{
				int vertexCount = Mathf.Min(maxParticles, 65535);

				mesh = new Mesh();
				mesh.name = "Spray";
				mesh.hideFlags = HideFlags.DontSave;
				mesh.vertices = new Vector3[vertexCount];

				int[] indices = new int[vertexCount];

				for(int i = 0; i < vertexCount; ++i)
					indices[i] = i;
				
				mesh.SetIndices(indices, MeshTopology.Points, 0);
				mesh.bounds = new Bounds(Vector3.zero, new Vector3(10000000.0f, 10000000.0f, 10000000.0f));
			}

			if(propertyBlocks == null)
			{
				int numMeshes = Mathf.CeilToInt(maxParticles / 65535.0f);

				propertyBlocks = new MaterialPropertyBlock[numMeshes];

				for(int i=0; i<numMeshes; ++i)
				{
					var block = propertyBlocks[i] = new MaterialPropertyBlock();
					block.SetFloat("_ParticleOffset", i * 65535);
				}
			}

			if(particlesA == null)
				particlesA = new ComputeBuffer(maxParticles, 40, ComputeBufferType.Append);

			if(particlesB == null)
				particlesB = new ComputeBuffer(maxParticles, 40, ComputeBufferType.Append);

			if(particlesBInfo == null)
			{
				particlesBInfo = new ComputeBuffer(1, 16, ComputeBufferType.DrawIndirect);
				var args = new int[4];
				args[0] = 0;
				args[1] = 1;
				args[2] = 0;
				args[3] = 0;
				particlesBInfo.SetData(args);
			}

			resourcesReady = true;
        }

		private void Dispose()
		{
			if(blankOutput != null)
			{
				Destroy(blankOutput);
				blankOutput = null;
			}

			if(particlesA != null)
			{
				particlesA.Dispose();
				particlesA = null;
			}

			if(particlesB != null)
			{
				particlesB.Dispose();
				particlesB = null;
			}

			if(particlesBInfo != null)
			{
				particlesBInfo.Release();
				particlesBInfo = null;
			}

			if(mesh != null)
			{
				Destroy(mesh);
				mesh = null;
			}

			resourcesReady = false;
		}

		void OnEnable()
		{
			water = GetComponent<Water>();
			water.ProfilesChanged.AddListener(OnProfilesChanged);
			OnProfilesChanged(water);

			Camera.onPreCull -= OnSomeCameraPreCull;
			Camera.onPreCull += OnSomeCameraPreCull;
		}

		void OnDisable()
		{
			Camera.onPreCull -= OnSomeCameraPreCull;

			Dispose();
		}

		void OnValidate()
		{
			if(sprayGeneratorShader == null)
				sprayGeneratorShader = Shader.Find("PlayWay Water/Spray/Generator");
			
#if UNITY_EDITOR
			if(sprayControllerShader == null)
			{
				var guids = UnityEditor.AssetDatabase.FindAssets("\"SprayController\" t:ComputeShader");

				if(guids.Length != 0)
				{
					string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
					sprayControllerShader = (ComputeShader)UnityEditor.AssetDatabase.LoadAssetAtPath(path, typeof(ComputeShader));
					UnityEditor.EditorUtility.SetDirty(this);
				}
			}

			if(sprayMaterial == null)
			{
				var guids = UnityEditor.AssetDatabase.FindAssets("\"Spray\" t:Material");

				if(guids.Length != 0)
				{
					string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
					sprayMaterial = (Material)UnityEditor.AssetDatabase.LoadAssetAtPath(path, typeof(Material));
					UnityEditor.EditorUtility.SetDirty(this);
				}
			}
#endif
			
			UpdatePrecomputedParams();
        }

		void LateUpdate()
		{
			if(Time.frameCount < 10)
				return;

			if(!resourcesReady)
				CheckResources();
			
			SwapParticleBuffers();
			ClearParticles();
			UpdateParticles();

			if(Camera.main != null)
				SpawnParticles(Camera.main.transform);
		}

		void OnSomeCameraPreCull(Camera camera)
		{
			if(!resourcesReady)
				return;

			if(camera.GetComponent<WaterCamera>() != null)
			{
				sprayMaterial.SetBuffer("_Particles", particlesA);
				sprayMaterial.SetVector("_CameraUp", camera.transform.up);
				sprayMaterial.SetFloat("_SpecularFresnelBias", water.WaterMaterial.GetFloat("_SpecularFresnelBias"));
				sprayMaterial.SetVector("_WrapSubsurfaceScatteringPack", water.WaterMaterial.GetVector("_WrapSubsurfaceScatteringPack"));
				sprayMaterial.SetTexture("_WaterMask", water.Renderer.Mask);
				
				probeAnchor.position = camera.transform.position;

				int numMeshes = propertyBlocks.Length;

				for(int i = 0; i < numMeshes; ++i)
                    Graphics.DrawMesh(mesh, Matrix4x4.identity, sprayMaterial, 0, camera, 0, propertyBlocks[i], UnityEngine.Rendering.ShadowCastingMode.Off, false, probeAnchor);
			}
		}
		
		private void SpawnParticles(Transform origin)
		{
			Vector3 originPosition = origin.position;
			float pixelSize = 400.0f / blankOutput.width;
			
			sprayGeneratorMaterial.CopyPropertiesFromMaterial(water.WaterMaterial);
			sprayGeneratorMaterial.SetVector("_SurfaceOffset", -water.SurfaceOffset);
            sprayGeneratorMaterial.SetVector("_Params", new Vector4(spawnThreshold * 0.25835f, skipRatioPrecomp, 0.0f, scale * 0.455f));
			sprayGeneratorMaterial.SetVector("_Coordinates", new Vector4(originPosition.x - 200.0f + Random.value * pixelSize, originPosition.z - 200.0f + Random.value * pixelSize, 400.0f, 400.0f));
			Graphics.SetRandomWriteTarget(1, particlesA);
			Graphics.Blit(null, blankOutput, sprayGeneratorMaterial, 0);
			Graphics.ClearRandomWriteTargets();
        }

		private void UpdateParticles()
		{
			Vector2 windSpeed = windWaves.WindSpeed * 0.0008f;
			Vector3 gravity = Physics.gravity;
			float deltaTime = Time.deltaTime;
			
            sprayControllerShader.SetFloat("deltaTime", deltaTime);
			sprayControllerShader.SetVector("externalForces", new Vector3((windSpeed.x + gravity.x) * deltaTime, gravity.y * deltaTime, (windSpeed.y + gravity.z) * deltaTime));
			sprayControllerShader.SetBuffer(0, "SourceParticles", particlesB);
			sprayControllerShader.SetBuffer(0, "TargetParticles", particlesA);
			sprayControllerShader.Dispatch(0, maxParticles / 128, 1, 1);
		}
		
		private void ClearParticles()
		{
			sprayControllerShader.SetBuffer(1, "TargetParticlesFlat", particlesA);
			sprayControllerShader.Dispatch(1, maxParticles / 128, 1, 1);
		}

		private void SwapParticleBuffers()
		{
			var t = particlesB;
			particlesB = particlesA;
			particlesA = t;
		}

		private void OnResolutionChanged(WindWaves windWaves)
		{
			if(blankOutput != null)
			{
				Destroy(blankOutput);
				blankOutput = null;
			}
			
			resourcesReady = false;
        }

		private void OnProfilesChanged(Water water)
		{
			var profiles = water.Profiles;

			spawnThreshold = 0.0f;
			spawnSkipRatio = 0.0f;
			scale = 0.0f;

			if(profiles != null)
			{
				foreach(var weightedProfile in profiles)
				{
					var profile = weightedProfile.profile;
					float weight = weightedProfile.weight;

					spawnThreshold += profile.SprayThreshold * weight;
					spawnSkipRatio += profile.SpraySkipRatio * weight;
					scale += profile.SpraySize * weight;
				}
			}
		}

		private void UpdatePrecomputedParams()
		{
			if(water != null)
				resolution = windWaves.FinalResolution;
			
			skipRatioPrecomp = Mathf.Pow(spawnSkipRatio, 1024.0f / resolution);
        }
	}
}
