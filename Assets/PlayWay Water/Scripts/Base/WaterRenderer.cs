using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Renders water.
	/// <seealso cref="Water.Renderer"/>
	/// </summary>
	[System.Serializable]
	public class WaterRenderer
	{
		[HideInInspector]
		[SerializeField]
		private Shader volumeFrontShader;

		[HideInInspector]
		[SerializeField]
		private Shader volumeBackShader;

		[HideInInspector]
		[SerializeField]
		private Shader volumeFrontAddShader;

		[HideInInspector]
		[SerializeField]
		private Shader volumeBackAddShader;

		[SerializeField]
		private Transform reflectionProbeAnchor;
		
        private Water water;
		private RenderTexture mask;
		private int waterMaskId;
		private List<Renderer> masks = new List<Renderer>();

		internal void OnEnable(Water water)
		{
			this.water = water;

			Camera.onPreCull -= OnSomeCameraPreCull;
			Camera.onPreCull += OnSomeCameraPreCull;

			Camera.onPostRender -= OnSomeCameraPostRender;
			Camera.onPostRender += OnSomeCameraPostRender;

			waterMaskId = Shader.PropertyToID("_WaterMask");
		}

		internal void OnDisable()
		{
			Camera.onPreCull -= OnSomeCameraPreCull;
			Camera.onPostRender -= OnSomeCameraPostRender;

			ReleaseTemporaryBuffers();
        }

		/// <summary>
		/// Accessible between camera's PreCull and PostRender
		/// </summary>
		public RenderTexture Mask
		{
			get { return mask; }
		}

		public int MaskCount
		{
			get { return masks.Count; }
		}

		public Transform ReflectionProbeAnchor
		{
			get { return reflectionProbeAnchor; }
			set { reflectionProbeAnchor = value; }
		}

		public void AddMask(Renderer mask)
		{
			mask.enabled = false;
			masks.Add(mask);
		}

		public void RemoveMask(Renderer mask)
		{
			masks.Remove(mask);
		}

		internal void OnValidate(Water water)
		{
			if(volumeFrontShader == null)
				volumeFrontShader = Shader.Find("PlayWay Water/Volumes/Front");

			if(volumeBackShader == null)
				volumeBackShader = Shader.Find("PlayWay Water/Volumes/Back");

			if(volumeFrontAddShader == null)
				volumeFrontAddShader = Shader.Find("PlayWay Water/Volumes/Front (Additive)");

			if(volumeBackAddShader == null)
				volumeBackAddShader = Shader.Find("PlayWay Water/Volumes/Back (Additive)");
        }

		public void Render(Camera camera, WaterGeometryType geometryType)
		{
			if(water == null || water.WaterMaterial == null || !water.isActiveAndEnabled)
				return;

			if((camera.cullingMask & (1 << water.gameObject.layer)) == 0)
				return;

			var waterCamera = camera.GetComponent<WaterCamera>();

			if((!water.Volume.Boundless && water.Volume.HasAdditiveMasks) && (waterCamera == null || !waterCamera.RenderVolumes))
				return;

			RenderMasks(camera, waterCamera);

			water.OnWaterRender(camera);
			
			Matrix4x4 matrix;
			var meshes = water.Geometry.GetTransformedMeshes(camera, out matrix, geometryType, false, waterCamera != null ? waterCamera.ForcedVertexCount : 0);

			for(int i = 0; i < meshes.Length; ++i)
			{
				Graphics.DrawMesh(meshes[i], matrix, water.WaterMaterial, water.gameObject.layer, camera, 0, null, water.ShadowCastingMode, water.ReceiveShadows, reflectionProbeAnchor == null ? water.transform : reflectionProbeAnchor);

				if(waterCamera == null || waterCamera.ContainingWater != null)
					Graphics.DrawMesh(meshes[i], matrix, water.WaterBackMaterial, water.gameObject.layer, camera, 0, null, water.ShadowCastingMode, water.ReceiveShadows, reflectionProbeAnchor == null ? water.transform : reflectionProbeAnchor);
			}
		}

		public void PostRender(Camera camera)
		{
			ReleaseTemporaryBuffers();

			if(water != null)
				water.OnWaterPostRender(camera);
		}

		private void OnSomeCameraPreCull(Camera camera)
		{
			var waterCamera = camera.GetComponent<WaterCamera>();

			//if((waterCamera == null || !waterCamera.enabled) && !IsSceneViewCamera(camera))
			//	return;

			if(!IsSceneViewCamera(camera))
				return;

			Render(camera, waterCamera != null ? waterCamera.GeometryType : WaterGeometryType.Auto);
		}

		private void OnSomeCameraPostRender(Camera camera)
		{
			//var waterCamera = camera.GetComponent<WaterCamera>();

			//if((waterCamera == null || !waterCamera.enabled) && !IsSceneViewCamera(camera))
			//	return;

			if(!IsSceneViewCamera(camera))
				return;

			PostRender(camera);
		}

		private void ReleaseTemporaryBuffers()
		{
			if(mask != null)
			{
				RenderTexture.ReleaseTemporary(mask);
				mask = null;
			}
		}

		private bool IsSceneViewCamera(Camera camera)
		{
#if UNITY_EDITOR
			foreach(UnityEditor.SceneView sceneView in UnityEditor.SceneView.sceneViews)
			{
				if(sceneView.camera == camera)
				{
					Shader.SetGlobalTexture("_WaterlessDepthTexture", UnityEditor.EditorGUIUtility.whiteTexture);
					return true;
				}
			}
#endif

			return false;
		}

		private void RenderMasks(Camera camera, WaterCamera waterCamera)
		{
			var subtractingVolumes = water.Volume.GetVolumeSubtractorsDirect();
			var boundingVolumes = water.Volume.GetVolumesDirect();

			if(waterCamera == null || !waterCamera.RenderVolumes || (subtractingVolumes.Count == 0 && boundingVolumes.Count == 0 && masks.Count == 0))
				return;
			
			int tempLayer = WaterProjectSettings.Instance.WaterTempLayer;
			int waterLayer = WaterProjectSettings.Instance.WaterLayer;
			
			var effectsCamera = waterCamera.EffectsCamera;

			if(effectsCamera == null)
				return;
			
			effectsCamera.CopyFrom(camera);
			effectsCamera.enabled = false;
			effectsCamera.GetComponent<WaterCamera>().enabled = false;
			effectsCamera.renderingPath = RenderingPath.Forward;
			effectsCamera.depthTextureMode = DepthTextureMode.None;
			effectsCamera.cullingMask = 1 << tempLayer;

			if(mask == null)
				mask = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 16, SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat) ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, 1);

			Graphics.SetRenderTarget(mask);

			if(subtractingVolumes.Count != 0)
			{
				foreach(var subtractingVolume in subtractingVolumes)
					subtractingVolume.SetLayer(tempLayer);

				using(var volumeFrontTexture = RenderTexturesCache.GetTemporary(camera.pixelWidth, camera.pixelHeight, 16, SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat) ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGBHalf, true, false))
				{
					// render front pass of volumetric masks
					effectsCamera.clearFlags = CameraClearFlags.SolidColor;
					effectsCamera.backgroundColor = new Color(0.0f, 0.0f, 0.5f, 0.0f);
					effectsCamera.targetTexture = volumeFrontTexture;
					effectsCamera.RenderWithShader(volumeFrontShader, "CustomType");

					GL.Clear(true, true, new Color(0.0f, 0.0f, 0.0f, 0.0f), 0.0f);

					// render back pass of volumetric masks
					Shader.SetGlobalTexture("_VolumesFrontDepth", volumeFrontTexture);
					effectsCamera.clearFlags = CameraClearFlags.Nothing;
					effectsCamera.targetTexture = mask;
					effectsCamera.RenderWithShader(volumeBackShader, "CustomType");
				}

				foreach(var subtractingVolume in subtractingVolumes)
					subtractingVolume.SetLayer(waterLayer);
			}

			if(boundingVolumes.Count != 0)
			{
				if(subtractingVolumes.Count == 0)
					GL.Clear(true, true, new Color(0.0f, 0.0f, 0.0f, 0.0f), 1.0f);
				else
					GL.Clear(true, false, new Color(0.0f, 0.0f, 0.0f, 0.0f), 1.0f);

				foreach(var boundingVolume in boundingVolumes)
					boundingVolume.SetLayer(tempLayer);

				// render additive volumes
				effectsCamera.clearFlags = CameraClearFlags.Nothing;
				effectsCamera.targetTexture = mask;
				effectsCamera.RenderWithShader(volumeFrontAddShader, "CustomType");

				GL.Clear(true, false, new Color(0.0f, 0.0f, 0.0f, 0.0f), 0.0f);

				effectsCamera.clearFlags = CameraClearFlags.Nothing;
				effectsCamera.targetTexture = mask;
				effectsCamera.RenderWithShader(volumeBackAddShader, "CustomType");

				foreach(var boundingVolume in boundingVolumes)
					boundingVolume.SetLayer(waterLayer);
			}

			if(masks.Count != 0)
			{
				if(subtractingVolumes.Count == 0 && boundingVolumes.Count == 0)
					GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));

				foreach(var m in masks)
					m.enabled = true;

				// render simple "screen-space" masks
				effectsCamera.clearFlags = CameraClearFlags.Nothing;
				effectsCamera.targetTexture = mask;
				effectsCamera.Render();

				foreach(var m in masks)
					m.enabled = false;
			}

			effectsCamera.targetTexture = null;

			water.WaterMaterial.SetTexture(waterMaskId, mask);
			water.WaterBackMaterial.SetTexture(waterMaskId, mask);
			water.WaterVolumeMaterial.SetTexture(waterMaskId, mask);
		}
	}
}
