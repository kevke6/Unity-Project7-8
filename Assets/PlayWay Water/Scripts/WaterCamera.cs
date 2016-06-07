using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlayWay.Water
{
	/// <summary>
	/// Each camera supposed to see water needs this component attached. Renders all camera-specific maps for the water:
	/// <list type="bullet">
	/// <item>Depth Maps</item>
	/// <item>Displaced water info map</item>
	/// <item>Volume maps</item>
	/// </list>
	/// </summary>
	[ExecuteInEditMode]
	public class WaterCamera : MonoBehaviour
	{
		[HideInInspector]
		[SerializeField]
		private Shader depthBlitCopyShader;

		[HideInInspector]
		[SerializeField]
		private Shader waterDepthShader;

		[HideInInspector]
		[SerializeField]
		private Shader volumeFrontShader;

		[HideInInspector]
		[SerializeField]
		private Shader volumeBackShader;

		[SerializeField]
		private WaterGeometryType geometryType = WaterGeometryType.Auto;

		[SerializeField]
		private bool renderWaterDepth = true;

		[SerializeField]
		private bool renderVolumes = true;

		[SerializeField]
		private bool sharedCommandBuffers = false;

		[HideInInspector]
		[SerializeField]
		private int forcedVertexCount = 0;

		private RenderTexture waterDepthTexture;
        private CommandBuffer depthRenderCommands;
		private CommandBuffer cleanUpCommands;
		private WaterCamera baseCamera;
        private Camera effectCamera;
		private Camera thisCamera;
		private Material depthMixerMaterial;
        private RenderTextureFormat depthTexturesFormat;
		private Vector2 localMapsOrigin;
		private float localMapsSizeInv;
		private int waterDepthTextureId;
		private int underwaterMaskId;
		private bool isEffectCamera;
		private bool effectsEnabled;
		private WaterVolumeProbe waterProbe;
		private IWaterImageEffect[] imageEffects;
		private Texture2D underwaterWhiteMask;

		void Awake()
		{
			waterDepthTextureId = Shader.PropertyToID("_WaterDepthTexture");
			underwaterMaskId = Shader.PropertyToID("_UnderwaterMask");

			depthTexturesFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat) ? RenderTextureFormat.RFloat : RenderTextureFormat.RHalf;

			OnValidate();
		}

		void OnEnable()
		{
			thisCamera = GetComponent<Camera>();

			if(!isEffectCamera)
			{
				float vfovrad = thisCamera.fieldOfView * Mathf.Deg2Rad;
				float nearPlaneSizeY = thisCamera.nearClipPlane * Mathf.Tan(vfovrad * 0.5f);
				waterProbe = WaterVolumeProbe.CreateProbe(transform, nearPlaneSizeY * 3.0f);

				imageEffects = GetComponents<IWaterImageEffect>();

				foreach(var imageEffect in imageEffects)
					imageEffect.OnWaterCameraEnabled();
			}
        }

		void OnDisable()
		{
			if(waterProbe != null)
			{
				waterProbe.gameObject.Destroy();
				waterProbe = null;
			}

			if(effectCamera != null)
			{
				effectCamera.gameObject.Destroy();
				effectCamera = null;
			}

			if(depthMixerMaterial != null)
			{
				depthMixerMaterial.Destroy();
				depthMixerMaterial = null;
			}

			DisableEffects();
		}

		public bool IsEffectCamera
		{
			get { return isEffectCamera; }
		}

		public WaterGeometryType GeometryType
		{
			get { return geometryType; }
			set { geometryType = value; }
		}

		public Vector2 LocalMapsOrigin
		{
			get { return localMapsOrigin; }
		}
		
		public float LocalMapsSizeInv
		{
			get { return localMapsSizeInv; }
		}

		public int ForcedVertexCount
		{
			get { return forcedVertexCount; }
		}

		public bool RenderVolumes
		{
			get { return renderVolumes; }
		}

		public Water ContainingWater
		{
			get { return baseCamera == null ? waterProbe.CurrentWater : baseCamera.ContainingWater; }
		}

		public WaterVolumeProbe WaterVolumeProbe
		{
			get { return waterProbe; }
		}

		/// <summary>
		/// Ready to render alternative camera for effects.
		/// </summary>
		public Camera EffectsCamera
		{
			get
			{
				if(!isEffectCamera && effectCamera == null)
					CreateEffectsCamera();

				return effectCamera;
			}
		}

		void Update()
		{
			if(!effectsEnabled)
			{
				if(IsWaterPossiblyVisible())
					EnableEffects();
			}
			else if(!IsWaterPossiblyVisible())
				DisableEffects();
		}
		
		void OnValidate()
		{
			if(depthBlitCopyShader == null)
				depthBlitCopyShader = Shader.Find("PlayWay Water/Depth/CopyMix");

			if(waterDepthShader == null)
				waterDepthShader = Shader.Find("PlayWay Water/Depth/Water Depth");

			if(volumeFrontShader == null)
				volumeFrontShader = Shader.Find("PlayWay Water/Volumes/Front");

			if(volumeBackShader == null)
				volumeBackShader = Shader.Find("PlayWay Water/Volumes/Back");
        }
		
		void OnPreCull()
		{
			SetFallbackUnderwaterMask();
			RenderWater();

			if(!effectsEnabled) return;

			SetLocalMapCoordinates();
			
			if(renderWaterDepth)
				RenderWaterDepth();

			if(imageEffects != null && Application.isPlaying)
			{
				foreach(var imageEffect in imageEffects)
					imageEffect.OnWaterCameraPreCull();
			}
        }

		IEnumerator OnPostRender()
		{
			if(waterDepthTexture != null)
			{
				RenderTexture.ReleaseTemporary(waterDepthTexture);
				waterDepthTexture = null;
			}

			yield return new WaitForEndOfFrame();

			foreach(var water in WaterGlobals.Instance.Waters)
				water.Renderer.PostRender(thisCamera);
		}

		private void RenderWater()
		{
			foreach(var water in WaterGlobals.Instance.Waters)
				water.Renderer.Render(thisCamera, geometryType);
		}

		private void RenderWaterDepth()
		{
			if(waterDepthTexture == null)
				waterDepthTexture = RenderTexture.GetTemporary(thisCamera.pixelWidth, thisCamera.pixelHeight, 16, depthTexturesFormat, RenderTextureReadWrite.Linear);

			var effectCamera = EffectsCamera;
			effectCamera.CopyFrom(thisCamera);
			effectCamera.GetComponent<WaterCamera>().enabled = true;
			effectCamera.renderingPath = RenderingPath.Forward;
			effectCamera.clearFlags = CameraClearFlags.SolidColor;
			effectCamera.depthTextureMode = DepthTextureMode.None;
			effectCamera.backgroundColor = Color.white;
			effectCamera.targetTexture = waterDepthTexture;
			effectCamera.cullingMask = (1 << WaterProjectSettings.Instance.WaterLayer);
			effectCamera.RenderWithShader(waterDepthShader, "CustomType");
			effectCamera.targetTexture = null;

			Shader.SetGlobalTexture(waterDepthTextureId, waterDepthTexture);
		}
		
		private void AddDepthRenderingCommands()
		{
			if(depthMixerMaterial == null)
			{
				depthMixerMaterial = new Material(depthBlitCopyShader);
				depthMixerMaterial.hideFlags = HideFlags.DontSave;
			}

			var camera = GetComponent<Camera>();

			if(((camera.depthTextureMode | DepthTextureMode.Depth) != 0 && renderWaterDepth) || renderVolumes)
			{
				int depthRT = Shader.PropertyToID("_CameraDepthTexture2");
				int waterlessDepthRT = Shader.PropertyToID("_WaterlessDepthTexture");

				depthRenderCommands = new CommandBuffer();
				depthRenderCommands.name = "Apply Water Depth";
				depthRenderCommands.GetTemporaryRT(waterlessDepthRT, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, depthTexturesFormat, RenderTextureReadWrite.Linear);
				depthRenderCommands.Blit(BuiltinRenderTextureType.None, waterlessDepthRT, depthMixerMaterial, 0);

				depthRenderCommands.GetTemporaryRT(depthRT, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, depthTexturesFormat, RenderTextureReadWrite.Linear);
				depthRenderCommands.SetRenderTarget(depthRT);
				depthRenderCommands.ClearRenderTarget(true, true, Color.white);
				depthRenderCommands.Blit(BuiltinRenderTextureType.None, depthRT, depthMixerMaterial, 1);
				depthRenderCommands.SetGlobalTexture("_CameraDepthTexture", depthRT);

				cleanUpCommands = new CommandBuffer();
				cleanUpCommands.name = "Clean Water Buffers";
				cleanUpCommands.ReleaseTemporaryRT(depthRT);
				cleanUpCommands.ReleaseTemporaryRT(waterlessDepthRT);

				camera.depthTextureMode |= DepthTextureMode.Depth;

				camera.AddCommandBuffer(camera.actualRenderingPath == RenderingPath.Forward ? CameraEvent.AfterDepthTexture : CameraEvent.BeforeLighting, depthRenderCommands);
				camera.AddCommandBuffer(CameraEvent.AfterEverything, cleanUpCommands);
			}
		}
		
		private void RemoveDepthRenderingCommands()
		{
			if(depthRenderCommands != null)
			{
				thisCamera.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, depthRenderCommands);
				thisCamera.RemoveCommandBuffer(CameraEvent.BeforeLighting, depthRenderCommands);
				depthRenderCommands.Dispose();
				depthRenderCommands = null;
            }

			if(cleanUpCommands != null)
			{
				thisCamera.RemoveCommandBuffer(CameraEvent.AfterEverything, cleanUpCommands);
				cleanUpCommands.Dispose();
				cleanUpCommands = null;
            }

			if(!sharedCommandBuffers)
				thisCamera.RemoveAllCommandBuffers();
        }

		private void EnableEffects()
		{
			if(isEffectCamera)
				return;

			effectsEnabled = true;

			AddDepthRenderingCommands();
        }

		private void DisableEffects()
		{
			effectsEnabled = false;

			RemoveDepthRenderingCommands();
		}
		
		private bool IsWaterPossiblyVisible()
		{
#if UNITY_EDITOR
			if(!Application.isPlaying)
				return true;
#endif

			var waters = WaterGlobals.Instance.Waters;

			return waters.Count != 0;
		}

		private void CreateEffectsCamera()
		{
			var depthCameraGo = new GameObject("Water Depth Camera");
			depthCameraGo.hideFlags = HideFlags.HideAndDontSave;

			effectCamera = depthCameraGo.AddComponent<Camera>();
			effectCamera.enabled = false;

			var depthWaterCamera = depthCameraGo.AddComponent<WaterCamera>();
			depthWaterCamera.isEffectCamera = true;
			depthWaterCamera.baseCamera = this;
            depthWaterCamera.waterDepthShader = waterDepthShader;
        }
		
		private void SetFallbackUnderwaterMask()
		{
			if(underwaterWhiteMask == null)
			{
				underwaterWhiteMask = new Texture2D(2, 2, TextureFormat.ARGB32, false);
				underwaterWhiteMask.hideFlags = HideFlags.DontSave;
				underwaterWhiteMask.SetPixel(0, 0, Color.black);
				underwaterWhiteMask.SetPixel(1, 0, Color.black);
				underwaterWhiteMask.SetPixel(0, 1, Color.black);
				underwaterWhiteMask.SetPixel(1, 1, Color.black);
				underwaterWhiteMask.Apply(false, true);
			}

			Shader.SetGlobalTexture(underwaterMaskId, underwaterWhiteMask);
		}

		private void SetLocalMapCoordinates()
		{
			int resolution = Mathf.NextPowerOfTwo(Mathf.Min(thisCamera.pixelWidth, thisCamera.pixelHeight)) / 4;
			float maxHeight = 0.0f;
			float maxWaterLevel = 0.0f;

			foreach(var water in WaterGlobals.Instance.Waters)
			{
				maxHeight += water.MaxVerticalDisplacement;

				float posY = water.transform.position.y;
				if(maxWaterLevel < posY)
					maxWaterLevel = posY;
			}

			// place camera
			Vector3 thisCameraPosition = thisCamera.transform.position;
			Vector3 screenSpaceDown = WaterUtilities.ViewportWaterPerpendicular(thisCamera);
			Vector3 worldSpaceDown = thisCamera.transform.localToWorldMatrix * WaterUtilities.RaycastPlane(thisCamera, maxWaterLevel, screenSpaceDown);
			worldSpaceDown.y = 0.0f;
			
			Vector3 effectCameraPosition = new Vector3(thisCameraPosition.x, 0.0f, thisCameraPosition.z) + worldSpaceDown * 2.0f;
			float size = Mathf.Max(thisCameraPosition.y * 6.0f, maxHeight * 10.0f, Vector3.Distance(effectCameraPosition, thisCameraPosition));

			float halfPixelSize = size / resolution;
			localMapsOrigin = new Vector2((effectCameraPosition.x - size) + halfPixelSize, (effectCameraPosition.z - size) + halfPixelSize);
			localMapsSizeInv = 0.5f / size;

			Shader.SetGlobalVector("_LocalMapsCoords", new Vector4(-localMapsOrigin.x, -localMapsOrigin.y, localMapsSizeInv, 0.0f));
		}
    }
}
