using UnityEngine;

namespace PlayWay.Water
{
	public class WaterSample
	{
		private Water water;
		private float x;
		private float z;
		
		private Vector3 displaced;
		private Vector3 previousResult;
		private Vector3 forces;

		private int segmentIndex;

		private bool finished;
		private bool enqueued;
		private bool changed;

		private int numComputedWaveGroups;
		private float numWaveGroupsInv;

		private float time;

		private DisplacementMode displacementMode;

		public WaterSample(Water water, DisplacementMode displacementMode = DisplacementMode.Height, float precision = 1.0f)
		{
			if(precision <= 0.0f || precision > 1.0f) throw new System.ArgumentException("Precision has to be between 0.0 and 1.0.");

			//int avgCpuWaves = water.SpectraRenderer.AvgCpuWaves;
			//int numWaveGroups = Mathf.Clamp(Mathf.RoundToInt(avgCpuWaves / 120.0f), 1, 8);
			int numWaveGroups = 4;

			this.numWaveGroupsInv = 1.0f / numWaveGroups;
            this.numComputedWaveGroups = Mathf.Max(1, Mathf.RoundToInt(numWaveGroups * precision));

			this.water = water;
			this.displacementMode = displacementMode;
			
			this.segmentIndex = 0;
			this.previousResult.x = float.NaN;
        }

		public bool Finished
		{
			get { return finished; }
		}

		public Vector2 Position
		{
			get { return new Vector2(x, z); }
		}

		/// <summary>
		/// Starts water height computations.
		/// </summary>
		/// <param name="origin"></param>
		public void Start(Vector3 origin)
		{
			GetAndReset(origin.x, origin.z, ComputationsMode.Normal);
		}

		/// <summary>
		/// Starts water height computations.
		/// </summary>
		/// <param name="origin"></param>
		public void Start(float x, float z)
		{
			GetAndReset(x, z, ComputationsMode.Normal);
		}

		/// <summary>
		/// Retrieves recently computed displacement and restarts computations on a new position.
		/// </summary>
		/// <param name="origin"></param>
		/// <param name="mode"></param>
		/// <returns></returns>
		public Vector3 GetAndReset(Vector3 origin, ComputationsMode mode = ComputationsMode.Normal)
		{
			return GetAndReset(origin.x, origin.z, mode);
		}

		/// <summary>
		/// Retrieves recently computed displacement and restarts computations on a new position.
		/// </summary>
		/// <param name="x">World space coordinate.</param>
		/// <param name="z">World space coordinate.</param>
		/// <param name="mode">Determines if the computations should be completed on the current thread if necessary. May hurt performance, but setting it to false may cause some 'flickering'.</param>
		/// <returns></returns>
		public Vector3 GetAndReset(float x, float z, ComputationsMode mode = ComputationsMode.Normal)
		{
			Vector3 forces;
			return GetAndReset(x, z, mode, out forces);
		}

		/// <summary>
		/// Retrieves recently computed displacement and restarts computations on a new position.
		/// </summary>
		/// <param name="x">World space coordinate.</param>
		/// <param name="z">World space coordinate.</param>
		/// <param name="mode">Determines if the computations should be completed on the current thread if necessary. May hurt performance, but setting it to false may cause some 'flickering'.</param>
		/// <returns></returns>
		public Vector3 GetAndReset(float x, float z, ComputationsMode mode, out Vector3 forces)
		{
			if(!enqueued)
			{
				WaterAsynchronousTasks.Instance.AddWaterSampleComputations(this);
				enqueued = true;

				water.OnSamplingStarted();
			}

			if(mode == ComputationsMode.ForceCompletion && !finished)
			{
				lock(this)
				{
					while(!finished)
						ComputationStep();
				}
			}

			changed = true;

			int segmentIndex = this.segmentIndex;

			Vector3 result = this.displaced;
			result.y += water.transform.position.y;

			forces = this.forces;

			this.x = x;
			this.z = z;
			this.displaced = new Vector3(x, 0.0f, z);
			this.forces = new Vector3();
            this.time = water.Time;
			this.segmentIndex = 0;
			this.finished = false;

			if(mode == ComputationsMode.Stabilized)
			{
				if(!float.IsNaN(previousResult.x))
					result = Vector3.Lerp(previousResult, result, 0.43f * (float)segmentIndex / numComputedWaveGroups);
				else
					result = new Vector3(x, 0.0f, z);

				previousResult = result;
			}

			return result;
		}

		public Vector3 Stop()
		{
			lock (this)
			{
				if(enqueued)
				{
					if(WaterAsynchronousTasks.HasInstance)
						WaterAsynchronousTasks.Instance.RemoveWaterSampleComputations(this);

					enqueued = false;

					if(water != null)
						water.OnSamplingStopped();
				}
				
				return displaced;
			}
		}

		internal void ComputationStep()
		{
			changed = false;

			if(!this.finished)
			{
				bool finished = true;

				if(displacementMode == DisplacementMode.Height || displacementMode == DisplacementMode.HeightAndForces)
				{
					// compensate horizontal displacement in first step
					if(segmentIndex == 0)
					{
						Vector2 offset = water.GetHorizontalDisplacementAt(x, z, 0, numWaveGroupsInv, time, ref finished);

						if(!changed)
						{
							x -= offset.x;
							z -= offset.y;
						}

						for(int i = 0; i < 2; ++i)
						{
							offset = water.GetHorizontalDisplacementAt(x, z, 0, numWaveGroupsInv, time, ref finished);

							if(!changed)
							{
								x += displaced.x - (x + offset.x);
								z += displaced.z - (z + offset.y);
							}
						}
					}

					if(displacementMode == DisplacementMode.Height)
					{
						// compute height at resultant point
						float result = water.GetHeightAt(x, z, segmentIndex * numWaveGroupsInv, (segmentIndex + 1) * numWaveGroupsInv, time, ref finished);

						if(!changed)
							displaced.y += result;
					}
					else
					{
						Vector4 result = water.GetHeightAndForcesAt(x, z, segmentIndex * numWaveGroupsInv, (segmentIndex + 1) * numWaveGroupsInv, time, ref finished);

						if(!changed)
						{
							displaced.y += result.w;
							forces.x += result.x;
							forces.y += result.y;
							forces.z += result.z;
						}
					}
				}
				else
				{
					Vector3 result = water.GetDisplacementAt(x, z, segmentIndex * numWaveGroupsInv, (segmentIndex + 1) * numWaveGroupsInv, time, ref finished);

					if(!changed)
						displaced += result;
				}
				
				if(!changed && (finished || ++segmentIndex >= numComputedWaveGroups))
					this.finished = true;
			}
		}
		
		public enum DisplacementMode
		{
			Height,
			Displacement,
			HeightAndForces
		}

		public enum ComputationsMode
		{
			Normal,
			Stabilized,
			ForceCompletion
		}
	}
}
