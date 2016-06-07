using UnityEngine;

namespace PlayWay.Water
{
	abstract public class WaterVolumeBase : MonoBehaviour
	{
		[SerializeField]
		private Water water;

		[SerializeField]
		private WaterVolumeMode mode = WaterVolumeMode.PhysicsAndRendering;

		private Collider[] colliders;
		private MeshRenderer[] volumeRenderers;
		private float radius;

		void OnEnable()
		{
			colliders = GetComponents<Collider>();

			Register(water);

			if(mode == WaterVolumeMode.PhysicsAndRendering && water != null)
				CreateRenderers();
		}

		void OnDisable()
		{
			DisposeRenderers();
			Unregister(water);
		}

		public Water Water
		{
			get { return water; }
		}

		public WaterVolumeMode Mode
		{
			get { return mode; }
		}

		public MeshRenderer[] VolumeRenderers
		{
			get { return volumeRenderers; }
		}

		abstract protected void Register(Water water);
		abstract protected void Unregister(Water water);

		void OnValidate()
		{
			colliders = GetComponents<Collider>();

			foreach(var collider in colliders)
			{
				if(!collider.isTrigger) collider.isTrigger = true;
			}
		}

		public void AssignTo(Water water)
		{
			if(this.water == water)
				return;

			DisposeRenderers();
			Unregister(water);
			this.water = water;
			Register(water);

			if(mode == WaterVolumeMode.PhysicsAndRendering && water != null)
				CreateRenderers();
		}

		internal void SetLayer(int layer)
		{
			if(volumeRenderers != null)
			{
				foreach(var renderer in volumeRenderers)
					renderer.gameObject.layer = layer;
			}
		}

		public bool IsPointInside(Vector3 point)
		{
			foreach(var collider in colliders)
			{
				if(collider.IsPointInside(point))
					return true;
			}

			return false;
		}

		private void DisposeRenderers()
		{
			if(volumeRenderers != null)
			{
				foreach(var renderer in volumeRenderers)
				{
					if(renderer != null)
						Destroy(renderer.gameObject);
				}

				volumeRenderers = null;
			}
		}

		virtual protected void CreateRenderers()
		{
			int numVolumes = colliders.Length;
			volumeRenderers = new MeshRenderer[numVolumes];

			var material = water.WaterVolumeMaterial;

			for(int i = 0; i < numVolumes; ++i)
			{
				var collider = colliders[i];

				GameObject rendererGo;

				if(collider is BoxCollider)
				{
					rendererGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
					rendererGo.transform.localScale = (collider as BoxCollider).size;
				}
				else if(collider is MeshCollider)
				{
					rendererGo = new GameObject();
					rendererGo.hideFlags = HideFlags.DontSave;

					var mf = rendererGo.AddComponent<MeshFilter>();
					mf.sharedMesh = (collider as MeshCollider).sharedMesh;

					rendererGo.AddComponent<MeshRenderer>();
				}
				else if(collider is SphereCollider)
				{
					float d = (collider as SphereCollider).radius * 2;

					rendererGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
					rendererGo.transform.localScale = new Vector3(d, d, d);
				}
				else if(collider is CapsuleCollider)
				{
					var capsuleCollider = collider as CapsuleCollider;
					float height = capsuleCollider.height * 0.5f;
					float radius = capsuleCollider.radius * 2.0f;

					rendererGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);

					switch(capsuleCollider.direction)
					{
						case 0:
						{
							rendererGo.transform.localEulerAngles = new Vector3(0.0f, 0.0f, 90.0f);
							rendererGo.transform.localScale = new Vector3(height, radius, radius);
							break;
						}

						case 1:
						{
							rendererGo.transform.localScale = new Vector3(radius, height, radius);
							break;
						}

						case 2:
						{
							rendererGo.transform.localEulerAngles = new Vector3(90.0f, 0.0f, 0.0f);
							rendererGo.transform.localScale = new Vector3(radius, radius, height);
							break;
						}
					}
				}
				else
					throw new System.InvalidOperationException("Unsupported collider type.");

				rendererGo.hideFlags = HideFlags.DontSave;
				rendererGo.name = "Volume Renderer";
				rendererGo.layer = WaterProjectSettings.Instance.WaterLayer;
				rendererGo.transform.SetParent(transform, false);

				Destroy(rendererGo.GetComponent<Collider>());

				var renderer = rendererGo.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = material;
				renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				renderer.receiveShadows = false;

				volumeRenderers[i] = renderer;
			}
		}

		public enum WaterVolumeMode
		{
			Physics,
			PhysicsAndRendering
		}
	}
}
