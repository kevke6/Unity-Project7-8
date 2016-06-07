using UnityEngine;
using UnityEngine.Rendering;

namespace PlayWay.Water
{
	[RequireComponent(typeof(Camera))]
	[RequireComponent(typeof(WaterCamera))]
	public class UnderwaterIME : MonoBehaviour, IWaterImageEffect
	{
		[HideInInspector]
		[SerializeField]
		private Shader underwaterMaskShader;

		[HideInInspector]
		[SerializeField]
		private Shader imeShader;

		[HideInInspector]
		[SerializeField]
		private Shader noiseShader;

		[HideInInspector]
		[SerializeField]
		private Shader composeUnderwaterMaskShader;

		[SerializeField]
		private Blur blur;

		[SerializeField]
		private bool underwaterAudio = true;

		private Material maskMaterial;
		private Material imeMaterial;
		private Material noiseMaterial;
		private Material composeUnderwaterMaskMaterial;

		private Camera localCamera;
		private WaterCamera localWaterCamera;

		private AudioReverbFilter reverbFilter;
		private WaterSample waterSample;

		private CommandBuffer maskCommandBuffer;

		private float intensity = float.NaN;
		private bool renderUnderwaterMask;
		private bool effectEnabled = true;
		private int maskRT, maskRT2;

		void Awake()
		{
			localCamera = GetComponent<Camera>();
			localWaterCamera = GetComponent<WaterCamera>();

			maskRT = Shader.PropertyToID("_UnderwaterMask");
			maskRT2 = Shader.PropertyToID("_UnderwaterMask2");

			OnValidate();

			maskMaterial = new Material(underwaterMaskShader);
			maskMaterial.hideFlags = HideFlags.DontSave;

			imeMaterial = new Material(imeShader);
			imeMaterial.hideFlags = HideFlags.DontSave;

			noiseMaterial = new Material(noiseShader);
			noiseMaterial.hideFlags = HideFlags.DontSave;

			composeUnderwaterMaskMaterial = new Material(composeUnderwaterMaskShader);
			composeUnderwaterMaskMaterial.hideFlags = HideFlags.DontSave;

			reverbFilter = GetComponent<AudioReverbFilter>();

			if(reverbFilter == null && underwaterAudio)
				reverbFilter = gameObject.AddComponent<AudioReverbFilter>();
		}
		
		public float Intensity
		{
			get { return intensity; }
		}

		public bool EffectEnabled
		{
			get { return effectEnabled; }
			set { effectEnabled = value; }
		}

		// Called by WaterCamera.cs
		public void OnWaterCameraEnabled()
		{
			var waterCamera = GetComponent<WaterCamera>();
			waterCamera.WaterVolumeProbe.Enter.AddListener(OnWaterEnter);
			waterCamera.WaterVolumeProbe.Leave.AddListener(OnWaterLeave);
		}
		
		// Called by WaterCamera.cs, to update this effect when it's disabled
		public void OnWaterCameraPreCull()
		{
			if(waterSample == null || !effectEnabled)
			{
				enabled = false;
				return;
			}
			
			float vfovrad = localCamera.fieldOfView * Mathf.Deg2Rad;
			float nearPlaneSizeY = localCamera.nearClipPlane * Mathf.Tan(vfovrad * 0.5f);

			float waterLevel = waterSample.GetAndReset(transform.position, WaterSample.ComputationsMode.Stabilized).y;
            float verticalDistance = transform.position.y - waterLevel;

			if(!localWaterCamera.ContainingWater.Volume.Boundless)
			{
				enabled = true;
				renderUnderwaterMask = true;
			}
			else if(verticalDistance - nearPlaneSizeY > 0.25f + localWaterCamera.ContainingWater.MaxVerticalDisplacement)
			{
				enabled = false;
			}
			else if(verticalDistance + nearPlaneSizeY < -0.25f - localWaterCamera.ContainingWater.MaxVerticalDisplacement)
			{
				enabled = true;
				renderUnderwaterMask = false;
			}
			else
			{
				enabled = true;
				renderUnderwaterMask = true;
			}

			float intensity = (-verticalDistance + nearPlaneSizeY) * 0.25f;

			SetEffectsIntensity(intensity);
		}
		
		void OnDisable()
		{
			if(maskCommandBuffer != null)
				maskCommandBuffer.Clear();
		}

		void OnDestroy()
		{
			if(maskCommandBuffer != null)
			{
				maskCommandBuffer.Dispose();
				maskCommandBuffer = null;
			}

			if(blur != null)
				blur.Dispose();

			Destroy(maskMaterial);
			Destroy(imeMaterial);
		}

		void OnValidate()
		{
			if(underwaterMaskShader == null)
				underwaterMaskShader = Shader.Find("PlayWay Water/Underwater/Screen-Space Mask");

			if(imeShader == null)
				imeShader = Shader.Find("PlayWay Water/Underwater/Base IME");

			if(noiseShader == null)
				noiseShader = Shader.Find("PlayWay Water/Utilities/Noise");

			if(composeUnderwaterMaskShader == null)
				composeUnderwaterMaskShader = Shader.Find("PlayWay Water/Underwater/Compose Underwater Mask");

			if(blur != null)
				blur.Validate("PlayWay Water/Utilities/Blur (Underwater)");
		}
		
		void OnPreCull()
		{
			RenderUnderwaterMask();
		}

		void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			if(localWaterCamera.ContainingWater == null)
			{
				Graphics.Blit(source, destination);
				return;
			}

			source.filterMode = FilterMode.Bilinear;

			using(var temp1 = RenderTexturesCache.GetTemporary(Screen.width, Screen.height, 0, destination != null ? destination.format : source.format, true, false))
			{
				temp1.Texture.filterMode = FilterMode.Bilinear;
				temp1.Texture.wrapMode = TextureWrapMode.Clamp;

				RenderDepthScatter(source, temp1);

				blur.TotalSize = localWaterCamera.ContainingWater.UnderwaterBlurSize;
				blur.Apply(temp1);

				RenderDistortions(temp1, destination);
			}
		}

		private void RenderUnderwaterMask()
		{
			if(maskCommandBuffer == null)
				return;

			maskCommandBuffer.Clear();

			var containingWater = localWaterCamera.ContainingWater;

			if(renderUnderwaterMask || (containingWater != null && containingWater.Renderer.MaskCount > 0))
			{
				maskCommandBuffer.GetTemporaryRT(maskRT, Screen.width >> 2, Screen.height >> 2, 0, FilterMode.Bilinear, RenderTextureFormat.R8, RenderTextureReadWrite.Linear, 1);
				maskCommandBuffer.GetTemporaryRT(maskRT2, Screen.width >> 2, Screen.height >> 2, 0, FilterMode.Point, RenderTextureFormat.R8, RenderTextureReadWrite.Linear, 1);
			}
			else
				maskCommandBuffer.GetTemporaryRT(maskRT, 4, 4, 0, FilterMode.Point, RenderTextureFormat.R8, RenderTextureReadWrite.Linear, 1);
			
			if(renderUnderwaterMask && containingWater != null)
			{
				maskMaterial.CopyPropertiesFromMaterial(containingWater.WaterMaterial);

				maskCommandBuffer.SetRenderTarget(maskRT2);
				maskCommandBuffer.ClearRenderTarget(false, true, Color.black);
				
				Matrix4x4 matrix;
				var meshes = containingWater.Geometry.GetTransformedMeshes(localCamera, out matrix, containingWater.Geometry.GeometryType == WaterGeometry.Type.ProjectionGrid ? WaterGeometryType.RadialGrid : WaterGeometryType.Auto, true);

				foreach(var mesh in meshes)
					maskCommandBuffer.DrawMesh(mesh, matrix, maskMaterial);

				// filter out common artifacts from the mask
				maskCommandBuffer.Blit(maskRT2, maskRT, imeMaterial, 4);
				maskCommandBuffer.ReleaseTemporaryRT(maskRT2);
			}
			else
			{
				maskCommandBuffer.SetRenderTarget(maskRT);
				maskCommandBuffer.ClearRenderTarget(false, true, Color.white);
			}

			if(containingWater != null && containingWater.Renderer.MaskCount != 0)
			{
				var mask = containingWater.Renderer.Mask;

				if(mask != null)
					maskCommandBuffer.Blit((Texture)mask, maskRT, composeUnderwaterMaskMaterial, 0);
			}
		}

		private void RenderDepthScatter(RenderTexture source, RenderTexture target)
		{
			imeMaterial.CopyPropertiesFromMaterial(localWaterCamera.ContainingWater.WaterMaterial);

			Vector2 surfaceOffset2D = localWaterCamera.ContainingWater.SurfaceOffset;
            imeMaterial.SetVector("_SurfaceOffset", new Vector3(surfaceOffset2D.x, localWaterCamera.ContainingWater.transform.position.y, surfaceOffset2D.y));
			imeMaterial.SetColor("_AbsorptionColor", localWaterCamera.ContainingWater.UnderwaterAbsorptionColor);
			imeMaterial.SetMatrix("UNITY_MATRIX_VP_INVERSE", Matrix4x4.Inverse(localCamera.projectionMatrix * localCamera.worldToCameraMatrix));

			var sss = imeMaterial.GetVector("_SubsurfaceScatteringPack");
			sss.y = 1.0f;
			sss.z = 2.0f;
			imeMaterial.SetVector("_SubsurfaceScatteringPack", sss);

			Graphics.Blit(source, target, imeMaterial, 2);
		}

		private void RenderDistortions(RenderTexture source, RenderTexture target)
		{
			float distortionIntensity = localWaterCamera.ContainingWater.UnderwaterDistortionsIntensity;

			if(distortionIntensity > 0.0f)
			{
				using(var distortionTex = RenderTexturesCache.GetTemporary(Screen.width >> 2, Screen.height >> 2, 0, RenderTextureFormat.ARGB32, true, false, false))
				{
					RenderDistortionMap(distortionTex);

					imeMaterial.SetTexture("_DistortionTex", distortionTex);
					imeMaterial.SetFloat("_DistortionIntensity", distortionIntensity);
					Graphics.Blit(source, target, imeMaterial, 3);
				}
			}
			else
				Graphics.Blit(source, target);
		}

		private void RenderDistortionMap(RenderTexture target)
		{
			noiseMaterial.SetVector("_Offset", new Vector4(0.0f, 0.0f, Time.time * localWaterCamera.ContainingWater.UnderwaterDistortionAnimationSpeed, 0.0f));
			noiseMaterial.SetVector("_Period", new Vector4(4, 4, 4, 4));
			Graphics.Blit(null, target, noiseMaterial, 1);
		}

		private void OnWaterEnter()
		{
			var waterCamera = GetComponent<WaterCamera>();

			waterSample = new WaterSample(waterCamera.ContainingWater, WaterSample.DisplacementMode.Height, 0.15f);
			waterSample.Start(transform.position);

			if(maskCommandBuffer == null)
			{
				maskCommandBuffer = new CommandBuffer();
				maskCommandBuffer.name = "Render Underwater Mask";
			}

			var camera = GetComponent<Camera>();
			camera.AddCommandBuffer(camera.actualRenderingPath == RenderingPath.Forward ? CameraEvent.AfterDepthTexture : CameraEvent.AfterLighting, maskCommandBuffer);
		}

		private void OnWaterLeave()
		{
			waterSample.Stop();
			waterSample = null;

			if(maskCommandBuffer != null)
			{
				var camera = GetComponent<Camera>();
				camera.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, maskCommandBuffer);
				camera.RemoveCommandBuffer(CameraEvent.AfterLighting, maskCommandBuffer);
			}
		}
		
		private void SetEffectsIntensity(float intensity)
		{
			if(localCamera == null)          // start wasn't called yet
				return;

			intensity = Mathf.Clamp01(intensity);

			if(this.intensity == intensity)
				return;
			
			this.intensity = intensity;

			if(reverbFilter != null && underwaterAudio)
			{
				float reverbIntensity = intensity > 0.05f ? Mathf.Clamp01(intensity + 0.7f) : intensity;

				reverbFilter.dryLevel = -2000 * reverbIntensity;
				reverbFilter.room = -10000 * (1.0f - reverbIntensity);
				reverbFilter.roomHF = Mathf.Lerp(-10000, -4000, reverbIntensity);
				reverbFilter.decayTime = 1.6f * reverbIntensity;
				reverbFilter.decayHFRatio = 0.1f * reverbIntensity;
				reverbFilter.reflectionsLevel = -449.0f * reverbIntensity;
				reverbFilter.reverbLevel = 1500.0f * reverbIntensity;
				reverbFilter.reverbDelay = 0.0259f * reverbIntensity;
			}
		}
	}
}
