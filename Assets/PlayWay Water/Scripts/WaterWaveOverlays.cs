using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	public class WaterWaveOverlays : MonoBehaviour, IWaterRenderAware
	{
		private Water water;
		private Dictionary<Camera, WaterOverlays> buffers = new Dictionary<Camera, WaterOverlays>();
		private List<Camera> lostCameras = new List<Camera>();
		private IOverlaysRenderer[] overlayRenderers;

		void OnEnable()
		{
			water = GetComponent<Water>();
			overlayRenderers = GetComponents<IOverlaysRenderer>();
        }

		void Update()
		{
			int frameIndex = Time.frameCount - 3;

			foreach(var kv in buffers)
			{
				if(kv.Value.lastFrameUsed < frameIndex)
				{
					kv.Value.Dispose();
					lostCameras.Add(kv.Key);
				}
			}

			foreach(var camera in lostCameras)
				buffers.Remove(camera);

			lostCameras.Clear();
        }

		public void UpdateMaterial(Water water, WaterQualityLevel qualityLevel)
		{
			
		}

		public void BuildShaderVariant(ShaderVariant variant, Water water, WaterQualityLevel qualityLevel)
		{
			variant.SetWaterKeyword("_WATER_OVERLAYS", enabled);
		}

		public void OnWaterRender(Camera camera)
		{
			var waterCamera = camera.GetComponent<WaterCamera>();

			if(waterCamera == null || waterCamera.IsEffectCamera || !enabled || !Application.isPlaying)
				return;

			var overlays = GetCameraOverlays(camera);
			overlays.lastFrameUsed = Time.frameCount;

			Graphics.SetRenderTarget(overlays.DisplacementMap);
			GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));

			Graphics.SetRenderTarget(overlays.SlopeMap);
			GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));

			foreach(var overlayRenderer in overlayRenderers)
				overlayRenderer.RenderOverlays(overlays);
			
			water.WaterMaterial.SetTexture("_LocalDisplacementMap", overlays.DisplacementMap);
			water.WaterMaterial.SetTexture("_LocalNormalMap", overlays.SlopeMap);
			water.WaterBackMaterial.SetTexture("_LocalDisplacementMap", overlays.DisplacementMap);
			water.WaterBackMaterial.SetTexture("_LocalNormalMap", overlays.SlopeMap);
		}

		public void OnWaterPostRender(Camera camera)
		{
			
		}

		private WaterOverlays GetCameraOverlays(Camera camera)
		{
			WaterOverlays cameraBuffers;

			if(!buffers.TryGetValue(camera, out cameraBuffers))
			{
				int resolution = (Mathf.NextPowerOfTwo(Mathf.Min(camera.pixelWidth, camera.pixelHeight)) >> 1);
				buffers[camera] = cameraBuffers = new WaterOverlays(camera.GetComponent<WaterCamera>(), resolution);
			}

			return cameraBuffers;
        }
	}

	public class WaterOverlays
	{
		private RenderTexture displacementMap;
		private RenderTexture slopeMap;
		private WaterCamera camera;

		internal int lastFrameUsed;

		public WaterOverlays(WaterCamera camera, int resolution)
		{
			this.camera = camera;

			displacementMap = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
			displacementMap.hideFlags = HideFlags.DontSave;
			displacementMap.filterMode = FilterMode.Bilinear;
			displacementMap.wrapMode = TextureWrapMode.Clamp;

			slopeMap = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear);
			slopeMap.hideFlags = HideFlags.DontSave;
			slopeMap.filterMode = FilterMode.Bilinear;
			slopeMap.wrapMode = TextureWrapMode.Clamp;
		}

		public void Dispose()
		{
			displacementMap.Destroy();
			slopeMap.Destroy();
		}

		public RenderTexture DisplacementMap
		{
			get { return displacementMap; }
		}

		public RenderTexture SlopeMap
		{
			get { return slopeMap; }
		}

		public WaterCamera Camera
		{
			get { return camera; }
		}
	}
}
