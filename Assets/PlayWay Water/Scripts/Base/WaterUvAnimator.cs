using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Animates water UV in time to simulate a water flow.
	/// </summary>
	[System.Serializable]
	public class WaterUvAnimator
	{
		private NormalMapAnimation normalMapAnimation1 = new NormalMapAnimation(1.0f, -10.0f, 1.0f, new Vector2(1.0f, 1.0f));
		private NormalMapAnimation normalMapAnimation2 = new NormalMapAnimation(-0.55f, 20.0f, 0.74f, new Vector2(1.5f, 1.5f));

		private Vector2 windOffset1 = new Vector2();
		private Vector2 windOffset2 = new Vector2();

		private Water water;
		private WindWaves windWaves;

		private float lastTime;

		public void Start(Water water)
		{
			this.water = water;
			this.windWaves = water.GetComponent<WindWaves>();
        }

		public Vector2 WindOffset
		{
			get { return windOffset1; }
		}

		public NormalMapAnimation NormalMapAnimation1
		{
			get { return normalMapAnimation1; }
			set { normalMapAnimation1 = value; }
		}

		public NormalMapAnimation NormalMapAnimation2
		{
			get { return normalMapAnimation2; }
			set { normalMapAnimation2 = value; }
		}

		public void Update()
		{
			float deltaTime = water.Time - lastTime;
			lastTime = water.Time;

			// apply offset
			Vector2 windSpeed = GetWindSpeed();

			Vector2 ws1 = Quaternion.Euler(0, 0, NormalMapAnimation1.Deviation) * windSpeed;
			Vector2 ws2 = Quaternion.Euler(0, 0, NormalMapAnimation2.Deviation) * windSpeed;

			windOffset1 += ws1 * NormalMapAnimation1.Speed * deltaTime * 0.021f;
			windOffset2 += ws2 * NormalMapAnimation2.Speed * deltaTime * 0.021f;

			// apply to material
			var waterMaterial = water.WaterMaterial;

			waterMaterial.SetTextureOffset("_BumpMap", new Vector2(-windOffset1.x * NormalMapAnimation1.Tiling.x, -windOffset1.y * NormalMapAnimation1.Tiling.y) * 0.065f);
			waterMaterial.SetTextureOffset("_DetailAlbedoMap", new Vector2(-windOffset2.x * NormalMapAnimation1.Tiling.x, -windOffset2.y * NormalMapAnimation1.Tiling.y) * 0.04f);
		}

		private Vector2 GetWindSpeed()
		{
			if(windWaves != null)
				return windWaves.WindSpeed;
			else
				return new Vector2(1.0f, 0.0f);
		}
	}
}
