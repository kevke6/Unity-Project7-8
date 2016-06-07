using UnityEngine;

namespace PlayWay.Water
{
	public class TerrainShoreline : MonoBehaviour
	{
		[SerializeField]
		private WindWaves water;

		[SerializeField]
		private Transform center;

		[SerializeField]
		private int spawnPointsCount = 50;

		private SpawnPoint[] spawnPoints;
		private WaterWavesParticleSystem waterParticleSystem;

		void OnEnable()
		{
			waterParticleSystem = water.GetComponent<WaterWavesParticleSystem>();

			if(waterParticleSystem == null)
				throw new System.Exception("TerrainShoreline requires WaterWavesParticleSystem component on target water.");

			//CreateSpawnPoints();
			CreateSpawnPointsFromSpectrum();
        }

		void Update()
		{
			var terrainCollider = GetComponent<TerrainCollider>();
			float deltaTime = Time.deltaTime;

			foreach(var spawnPoint in spawnPoints)
			{
				spawnPoint.timeLeft -= deltaTime;

				if(spawnPoint.timeLeft < 0)
				{
					spawnPoint.timeLeft += spawnPoint.timeInterval;
					waterParticleSystem.Spawn(new WaterWavesParticleSystem.LinearParticle(spawnPoint.position, spawnPoint.direction, spawnPoint.frequency, spawnPoint.amplitude, Random.Range(190.0f, 581.0f), terrainCollider));
				}
			}

			/*if(Random.value < 0.08f)
			{
				var terrainCollider = GetComponent<TerrainCollider>();

				var spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length - 1)];
				waterParticleSystem.Spawn(new WaterWavesParticleSystem.LinearParticle(spawnPoint.position, spawnPoint.direction, spawnPoint.frequency, Random.Range(0.05f, 0.1f), Random.Range(90.0f, 281.0f), terrainCollider));
			}*/
		}

		/*private void CreateSpawnPoints()
		{
			var terrain = GetComponent<Terrain>();
			var terrainData = terrain.terrainData;
			var terrainCollider = GetComponent<TerrainCollider>();

			Vector2 terrainMin = new Vector2(terrain.transform.position.x - terrainData.size.x * 1.65f, terrain.transform.position.z - terrainData.size.z * 1.65f);
			Vector2 terrainMax = new Vector2(terrain.transform.position.x + terrainData.size.x * 1.65f, terrain.transform.position.z + terrainData.size.z * 1.65f);
			float waterY = water.transform.position.y;
			RaycastHit hitInfo;

			spawnPoints = new SpawnPoint[spawnPointsCount];

			for(int i=0; i< spawnPointsCount; ++i)
			{
				for(int ii = 0; ii < 40; ++ii)
				{
					Vector3 point = new Vector3(Random.Range(terrainMin.x, terrainMax.x), waterY + 1000.0f, Random.Range(terrainMin.y, terrainMax.y));

					if(!terrainCollider.Raycast(new Ray(point, Vector3.down), out hitInfo, 1000.0f) || ii == 19)
					{
						point.y = waterY;
						Vector2 closestBeachDir = FindClosestBeachDirection(terrainCollider, point);

						if(!float.IsNaN(closestBeachDir.x))
						{
							spawnPoints[i] = new SpawnPoint(new Vector2(point.x, point.z), closestBeachDir, Random.Range(0.0005f, 0.00125f));
							break;
						}
						else if(ii == 19)
						{
							spawnPoints[i] = new SpawnPoint(new Vector2(point.x, point.z), (terrain.transform.position - point).normalized, Random.Range(0.0005f, 0.00125f));
							break;
						}
					}
				}
			}
		}*/

		private void CreateSpawnPointsFromSpectrum()
		{
			var terrain = GetComponent<Terrain>();
			var terrainData = terrain.terrainData;

			var gerstners = water.SpectrumResolver.FindMostMeaningfulWaves(spawnPointsCount, false);

			spawnPointsCount = gerstners.Length;

			Vector2 centerPos = new Vector2(center.position.x, center.position.z);
			float terrainSize = terrainData.size.x * 0.5f;

			spawnPoints = new SpawnPoint[spawnPointsCount];

			for(int i=0; i<spawnPointsCount; ++i)
			{
				var gerstner = gerstners[i];

				Vector2 point = centerPos - gerstner.direction * terrainSize;
				
				spawnPoints[i] = new SpawnPoint(point, gerstner.direction, gerstner.frequency, Mathf.Abs(gerstner.amplitude * 2.0f), gerstner.speed, water.TileSizes.x);
			}
		}

		private Vector2 FindClosestBeachDirection(TerrainCollider terrainCollider, Vector3 point)
		{
			RaycastHit hitInfo;
			Vector3 closestHit = new Vector3(float.NaN, float.NaN, float.NaN);
			float closestDistance = float.PositiveInfinity;

			for(int i=0; i<16; ++i)
			{
				float f = 2.0f * Mathf.PI * i / 16.0f;
                float s = Mathf.Sin(f);
				float c = Mathf.Cos(f);

				Ray ray = new Ray(point, new Vector3(s, 0.0f, c));

				if(terrainCollider.Raycast(ray, out hitInfo, 100000.0f))
				{
					float distance = hitInfo.distance;

					if(closestDistance > distance)
					{
						closestDistance = distance;
						closestHit = hitInfo.point;
					}
				}
			}

			if(!float.IsNaN(closestHit.x))
				return new Vector2(closestHit.x - point.x, closestHit.z - point.z).normalized;
			else
				return new Vector2(float.NaN, float.NaN);
		}

		class SpawnPoint
		{
			public Vector2 position;
			public Vector2 direction;
			public float frequency;
			public float amplitude;
			public float timeInterval;
			public float timeLeft;

			public SpawnPoint(Vector2 position, Vector2 direction, float frequency, float amplitude, float speed, float tileSize)
			{
				this.position = position;
				this.direction = direction;
				this.frequency = frequency;
				this.amplitude = amplitude;

				this.timeInterval = 2.0f * Mathf.PI / speed;
				//this.timeInterval = (2.0f * Mathf.PI / frequency) / speed;
				this.timeLeft = Random.Range(0.0f, timeInterval);
			}
		}
	}
}
