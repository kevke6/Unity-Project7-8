using UnityEngine;
using System.Collections.Generic;

namespace PlayWay.Water
{
	public class WaterPhysics : MonoBehaviour
	{
		[Space]
		[Tooltip("Controls precision of the simulation. Keep it low (1 - 2) for small and not important objects. Prefer high values (15 - 30) for ships etc.")]
		[Range(1, 30)]
		[SerializeField]
		private int sampleCount = 20;

		[Range(0.0f, 3.0f)]
		[Tooltip("Controls drag force. Determined experimentally in wind tunnels. Example values:\n https://en.wikipedia.org/wiki/Drag_coefficient#General")]
		[SerializeField]
		private float dragCoefficient = 0.9f;

		[Space]
		[Range(0.125f, 1.0f)]
		[Tooltip("Determines how many waves will be used in computations. Set it low for big objects, larger than most of the waves. Set it high for smaller objects of size comparable to many waves.")]
		[SerializeField]
		private float precision = 0.5f;

		[Tooltip("Adjust buoyancy proportionally, if your collider is bigger or smaller than the actual object. Lowering this may fix some weird behaviour of objects with extremely low density like beach balls or baloons.")]
		[SerializeField]
		private float buoyancyIntensity = 1.0f;

		[Tooltip("Horizontal flow force intensity.")]
		[SerializeField]
		private float flowIntensity = 1.0f;

		private Vector3[] cachedSamplePositions;
		private int cachedSampleIndex;
		private int cachedSampleCount;

		private Collider localCollider;
		private Rigidbody rigidBody;

		private float volume;
		private float area;

		private WaterSample[] samples;

		// precomputed stuff
		private float numSamplesInv;
		private Vector3 buoyancyPart;
		private float dragPart;
		private float flowPart;
		private WaterVolumeProbe waterProbe;

		private Ray rayUp;
		private Ray rayDown;

		void Awake()
		{
			localCollider = GetComponent<Collider>();
			rigidBody = GetComponentInParent<Rigidbody>();

			rayUp = new Ray(Vector3.zero, Vector3.up);
			rayDown = new Ray(Vector3.zero, Vector3.down);

			if(localCollider == null || rigidBody == null)
			{
				Debug.LogError("WaterPhysics component is attached to an object without any Collider and/or RigidBody.");
				enabled = false;
				return;
			}

			OnValidate();
			PrecomputeSamples();
		}

		public float BuoyancyIntensity
		{
			get { return buoyancyIntensity; }
			set
			{
				buoyancyIntensity = value;

				if(waterProbe != null && waterProbe.CurrentWater != null)
					PrecomputeBuoyancy();
			}
		}

		public float DragCoefficient
		{
			get { return dragCoefficient; }
			set
			{
				dragCoefficient = value;

				if(waterProbe != null && waterProbe.CurrentWater != null)
					PrecomputeDrag();
			}
		}

		public float FlowIntensity
		{
			get { return flowIntensity; }
			set
			{
				flowIntensity = value;

				if(waterProbe != null && waterProbe.CurrentWater != null)
					PrecomputeFlow();
			}
		}

		public float GetTotalBuoyancy(float fluidDensity = 999.8f)
		{
#if UNITY_EDITOR
			if(!Application.isPlaying && !ValidateForEditor())
				return 0.0f;
#endif

			return Physics.gravity.magnitude * volume * buoyancyIntensity * fluidDensity / rigidBody.mass;
		}

		private bool ValidateForEditor()
		{
			if(localCollider == null)
			{
				localCollider = GetComponent<Collider>();
				rigidBody = GetComponentInParent<Rigidbody>();
				OnValidate();
			}

			if(localCollider == null || rigidBody == null)
				return false;

			return true;
		}

		void OnEnable()
		{
			waterProbe = WaterVolumeProbe.CreateProbe(rigidBody.transform, localCollider.bounds.extents.magnitude);
			waterProbe.Enter.AddListener(OnWaterEnter);
			waterProbe.Leave.AddListener(OnWaterLeave);
		}

		void OnDisable()
		{
			if(waterProbe != null)
			{
				waterProbe.gameObject.Destroy();
				waterProbe = null;
			}

			OnWaterLeave();
		}

		void OnValidate()
		{
			numSamplesInv = 1.0f / sampleCount;

			if(localCollider != null)
			{
				volume = localCollider.ComputeVolume();
				area = localCollider.ComputeArea();
			}

			if(flowIntensity < 0) flowIntensity = 0;
			if(buoyancyIntensity < 0) buoyancyIntensity = 0;

			if(waterProbe != null && waterProbe.CurrentWater != null)
			{
				PrecomputeBuoyancy();
				PrecomputeDrag();
				PrecomputeFlow();
			}
		}

		void FixedUpdate()
		{
			var currentWater = waterProbe.CurrentWater;

			if(currentWater == null) return;

			var bounds = localCollider.bounds;
			float min = bounds.min.y;
			float max = bounds.max.y;

			Vector3 velocity, sqrVelocity, dragForce;
            Vector3 force;
			float height = max - min + 80.0f;
			float fixedDeltaTime = Time.fixedDeltaTime;
			float forceToVelocity = fixedDeltaTime * (1.0f - rigidBody.drag * fixedDeltaTime) / rigidBody.mass;
			float precompMaxF = rigidBody.mass * numSamplesInv / fixedDeltaTime;
			float waterDensity = currentWater.Density;

			/*
			 * Compute new samples.
			 */
			for(int i = 0; i < sampleCount; ++i)
			{
				Vector3 point = transform.TransformPoint(cachedSamplePositions[cachedSampleIndex]);
				Vector3 flowForce;
				Vector3 displaced = samples[i].GetAndReset(point.x, point.z, WaterSample.ComputationsMode.Normal, out flowForce);

				float waterHeight = displaced.y;
				displaced.y = min - 20.0f;
				rayUp.origin = displaced;

				RaycastHit hitInfo;

				if(localCollider.Raycast(rayUp, out hitInfo, height))
				{
					float low = hitInfo.point.y;
					Vector3 normal = hitInfo.normal;

					displaced.y = max + 20.0f;
					rayDown.origin = displaced;
					localCollider.Raycast(rayDown, out hitInfo, height);

					float high = hitInfo.point.y;

					float frc = (waterHeight - low) / (high - low);

					if(frc <= 0.0f)
						continue;

					if(frc > 1.0f)
						frc = 1.0f;

					// buoyancy
					force = buoyancyPart * waterDensity * frc;

					// hydrodynamic drag
					displaced.y = Mathf.Lerp(low, high, frc * 0.5f);

					Vector3 pointVelocity = rigidBody.GetPointVelocity(displaced);
					velocity.x = pointVelocity.x + force.x * forceToVelocity;
					velocity.y = pointVelocity.y + force.y * forceToVelocity;
					velocity.z = pointVelocity.z + force.z * forceToVelocity;
					sqrVelocity.x = velocity.x * velocity.x;
					sqrVelocity.y = velocity.y * velocity.y;
					sqrVelocity.z = velocity.z * velocity.z;

					if(velocity.x > 0.0f) sqrVelocity.x = -sqrVelocity.x;
					if(velocity.y > 0.0f) sqrVelocity.y = -sqrVelocity.y;
					if(velocity.z > 0.0f) sqrVelocity.z = -sqrVelocity.z;

					float t = dragPart * waterDensity;
					dragForce.x = t * sqrVelocity.x;
					dragForce.y = t * sqrVelocity.y;
					dragForce.z = t * sqrVelocity.z;

					// limit drag to prevent backward motion
					//float maxF = pointVelocity.magnitude * precompMaxF + force.magnitude;
					//float prop = maxF / dragForce.magnitude;
					//if(prop < 1.0f) dragForce *= prop;

					force += frc * dragForce;

					// flow force
					float d = -Vector3.Dot(normal, flowForce.normalized) + 0.5f;

					if(d > 0)
						force += flowForce * (d * flowPart);

					// apply forces
					rigidBody.AddForceAtPosition(force, displaced, ForceMode.Force);

#if UNITY_EDITOR
					if(WaterProjectSettings.Instance.DebugPhysics)
					{
						displaced.y = waterHeight;
						Debug.DrawLine(displaced, displaced + force / rigidBody.mass, Color.white, 0.0f, false);
					}
#endif
				}

				if(++cachedSampleIndex >= cachedSampleCount)
					cachedSampleIndex = 0;
			}
		}

		private void OnWaterEnter()
		{
			CreateWaterSamplers();
			PrecomputeBuoyancy();
			PrecomputeDrag();
			PrecomputeFlow();
        }

		private void OnWaterLeave()
		{
			if(samples != null)
			{
				for(int i = 0; i < sampleCount; ++i)
					samples[i].Stop();

				samples = null;
			}
		}

		private void PrecomputeSamples()
		{
			var samplePositions = new List<Vector3>();

			float offset = 0.5f;
			float step = 1.0f;
			int targetPoints = sampleCount * 18;
			var transform = this.transform;

			Vector3 min, max;
			ColliderExtensions.GetLocalMinMax(localCollider, out min, out max);

			for(int i = 0; i < 4 && samplePositions.Count < targetPoints; ++i)
			{
				for(float x = offset; x <= 1.0f; x += step)
				{
					for(float y = offset; y <= 1.0f; y += step)
					{
						for(float z = offset; z <= 1.0f; z += step)
						{
							Vector3 p = new Vector3(Mathf.Lerp(min.x, max.x, x), Mathf.Lerp(min.y, max.y, y), Mathf.Lerp(min.z, max.z, z));

							if(localCollider.IsPointInside(transform.TransformPoint(p)))
								samplePositions.Add(p);
						}
					}
				}

				step = offset;
				offset *= 0.5f;
			}

			cachedSamplePositions = samplePositions.ToArray();
			cachedSampleCount = cachedSamplePositions.Length;
			Shuffle(cachedSamplePositions);
		}

		private void CreateWaterSamplers()
		{
			if(samples == null || samples.Length != sampleCount)
				samples = new WaterSample[sampleCount];

			for(int i = 0; i < sampleCount; ++i)
			{
				samples[i] = new WaterSample(waterProbe.CurrentWater, WaterSample.DisplacementMode.HeightAndForces, precision);
				samples[i].Start(cachedSamplePositions[cachedSampleIndex]);

				if(++cachedSampleIndex >= cachedSampleCount)
					cachedSampleIndex = 0;
			}
		}

		private void PrecomputeBuoyancy()
		{
			buoyancyPart = -Physics.gravity * (numSamplesInv * volume * buoyancyIntensity);
		}

		private void PrecomputeDrag()
		{
			dragPart = 0.5f * dragCoefficient * area * numSamplesInv;
		}

		private void PrecomputeFlow()
		{
			flowPart = flowIntensity * dragCoefficient * area * numSamplesInv * 240.0f;
        }

		private void Shuffle<T>(T[] array)
		{
			int n = array.Length;

			while(n > 1)
			{
				int k = Random.Range(0, n--);

				var t = array[n];
				array[n] = array[k];
				array[k] = t;
			}
		}
	}
}
