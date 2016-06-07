using UnityEngine;

namespace PlayWay.Water
{
	public class WaterFloat : MonoBehaviour
	{
		[SerializeField]
		private Water water;

		[SerializeField]
		private float heightBonus = 0.0f;

		[Range(0.04f, 1.0f)]
		[SerializeField]
		private float precision = 0.2f;

		[SerializeField]
		private WaterSample.DisplacementMode displacementMode = WaterSample.DisplacementMode.Displacement;
		
		private WaterSample sample;

		private Vector3 initialPosition;

		void Start()
		{
			initialPosition = transform.position;

			if(water == null)
				water = FindObjectOfType<Water>();

			sample = new WaterSample(water, displacementMode, precision);
		}

		void OnDisable()
		{
			sample.Stop();
		}

		void LateUpdate()
		{
			Vector3 displaced = sample.GetAndReset(initialPosition.x, initialPosition.z, WaterSample.ComputationsMode.ForceCompletion);
			displaced.y += heightBonus;
            transform.position = displaced;
		}
	}
}
