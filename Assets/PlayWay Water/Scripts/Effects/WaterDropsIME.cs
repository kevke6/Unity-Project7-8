using UnityEngine;

namespace PlayWay.Water
{
	[RequireComponent(typeof(UnderwaterIME))]
	[ExecuteInEditMode]
	public class WaterDropsIME : MonoBehaviour, IWaterImageEffect
	{
		[HideInInspector]
		[SerializeField]
		private Shader waterDropsShader;

		[SerializeField]
		private Texture2D normalMap;

		[SerializeField]
		private float intensity = 1.0f;

		private Material overlayMaterial;
		private RenderTexture maskA;
		private RenderTexture maskB;
		private WaterCamera waterCamera;
		private UnderwaterIME underwaterIME;
		private float disableTime;
		
		void Awake()
		{
			waterCamera = GetComponent<WaterCamera>();
			underwaterIME = GetComponent<UnderwaterIME>();
			OnValidate();
		}
		
		public float Intensity
		{
			get { return intensity; }
			set { intensity = value; }
		}

		public Texture2D NormalMap
		{
			get { return normalMap; }
			set
			{
				normalMap = value;

				if(overlayMaterial != null)
					overlayMaterial.SetTexture("_NormalMap", normalMap);
			}
		}

		void OnValidate()
		{
			if(waterDropsShader == null)
				waterDropsShader = Shader.Find("PlayWay Water/IME/Water Drops");
		}
		
		void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			CheckResources();

			Graphics.Blit(maskA, maskB, overlayMaterial, 0);

			overlayMaterial.SetFloat("_Intensity", intensity);
			overlayMaterial.SetTexture("_Mask", maskB);
			overlayMaterial.SetTexture("_WaterMask", waterCamera.ContainingWater != null ? waterCamera.ContainingWater.Renderer.Mask : null);

#if UNITY_EDITOR
			overlayMaterial.SetTexture("_NormalMap", normalMap);
#endif

			Graphics.Blit(source, destination, overlayMaterial, 1);

			SwapMasks();
		}

		private void CheckResources()
		{
			if(overlayMaterial == null)
			{
				overlayMaterial = new Material(waterDropsShader);
				overlayMaterial.hideFlags = HideFlags.DontSave;
				overlayMaterial.SetTexture("_NormalMap", normalMap);
			}

			if(maskA == null || maskA.width != Screen.width >> 1 || maskA.height != Screen.height >> 1)
			{
				maskA = CreateMaskRT();
				maskB = CreateMaskRT();
			}
		}

		private RenderTexture CreateMaskRT()
		{
			var renderTexture = new RenderTexture(Screen.width >> 1, Screen.height >> 1, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
			renderTexture.hideFlags = HideFlags.DontSave;
			renderTexture.filterMode = FilterMode.Bilinear;

			Graphics.SetRenderTarget(renderTexture);
			GL.Clear(false, true, Color.black);

			return renderTexture;
		}

		private void SwapMasks()
		{
			var t = maskA;
			maskA = maskB;
			maskB = t;
		}

		public void OnWaterCameraEnabled()
		{
			
		}

		public void OnWaterCameraPreCull()
		{
			if(underwaterIME.enabled)
				disableTime = Time.time + 6.0f;

			enabled = intensity > 0 && Time.time <= disableTime;
        }
	}
}
