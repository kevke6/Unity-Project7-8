using System.Collections.Generic;
using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Simulates wave particles.
	/// </summary>
	[RequireComponent(typeof(WaterWaveOverlays))]
	public class WaterWavesParticleSystem : MonoBehaviour, IOverlaysRenderer
	{
		[HideInInspector]
		[SerializeField]
		private Shader waterWavesParticlesShader;

		[SerializeField]
		private int maxParticles = 15000;

		private List<LinearParticle> linearParticles;
		private List<RadialParticle> radialParticles;
		
		private Material waterWavesParticlesMaterial;
		private Mesh linearParticlesMesh;
		private Mesh radialParticlesMesh;
		private Vector3[] linearParticlesVertices;
		private Vector4[] meshWaveData;
		private bool meshWaveDataDirty;
		private bool meshWaveDataChanged;

		private int lastLinearParticleCostlyUpdate;

		public bool Spawn(LinearParticle particle)
		{
			if(linearParticles.Count >= maxParticles)
				return false;

			particle.positionY = transform.position.y;

			linearParticles.Add(particle);
			particle.CostlyUpdate();

			UpdateLinearParticleData(linearParticles.Count - 1);

			return true;
		}

		void OnEnable()
		{
			CheckResources();
		}

		public void RenderOverlays(WaterOverlays overlays)
		{
			RenderParticles(overlays);
		}

		void OnDisable()
		{
			FreeResources();
		}

		void OnValidate()
		{
			if(waterWavesParticlesShader == null)
				waterWavesParticlesShader = Shader.Find("PlayWay Water/Particles/Particles");
        }

		void LateUpdate()
		{
			UpdateSimulation();
		}

		private void UpdateSimulation()
		{
			float time = Time.time;

			for(int i=0; i<20; ++i)
			{
				if(lastLinearParticleCostlyUpdate >= linearParticles.Count)
				{
					lastLinearParticleCostlyUpdate = 0;
					break;
				}

				var particle = linearParticles[lastLinearParticleCostlyUpdate];

				if(particle.collider == null)
					continue;

				if(particle.CostlyUpdate())
				{
					if(particle.depth < 0.0f)
						DestroyParticle(lastLinearParticleCostlyUpdate--);
					else
						UpdateLinearParticleData(lastLinearParticleCostlyUpdate);
				}

				++lastLinearParticleCostlyUpdate;
            }

			int numLinearParticles = linearParticles.Count;

			for(int particleIndex = 0; particleIndex < numLinearParticles; ++particleIndex)
			{
				var particle = linearParticles[particleIndex];
				particle.position += particle.direction * particle.speed * Time.deltaTime;

				int vertexIndex = particleIndex << 2;
				Vector3 vertexPosition = particle.position;
				vertexPosition.z = particle.amplitude * Mathf.Min(1.0f, time - particle.spawnTime);
                linearParticlesVertices[vertexIndex++] = vertexPosition;
				linearParticlesVertices[vertexIndex++] = vertexPosition;
				linearParticlesVertices[vertexIndex++] = vertexPosition;
				linearParticlesVertices[vertexIndex] = vertexPosition;
			}

			linearParticlesMesh.vertices = linearParticlesVertices;

			if(meshWaveDataDirty)
			{
				for(int i = 0; i < numLinearParticles; ++i)
					UpdateLinearParticleData(i);

				meshWaveDataDirty = false;
				meshWaveDataChanged = true;
            }

			if(meshWaveDataChanged)
			{
				linearParticlesMesh.tangents = meshWaveData;
				meshWaveDataChanged = false;
            }
		}

		private void RenderParticles(WaterOverlays overlays)
		{
			Vector2 origin = overlays.Camera.LocalMapsOrigin;
			
			Graphics.SetRenderTarget(new RenderBuffer[] { overlays.DisplacementMap.colorBuffer, overlays.SlopeMap.colorBuffer }, overlays.DisplacementMap.depthBuffer);
			waterWavesParticlesMaterial.SetVector("_ParticleFieldCoords", new Vector4(-origin.x, -origin.y, overlays.Camera.LocalMapsSizeInv, 0.0f));
			waterWavesParticlesMaterial.SetPass(0);
            Graphics.DrawMeshNow(linearParticlesMesh, Matrix4x4.identity, 0);
		}

		private void DestroyParticle(int particleIndex)
		{
			linearParticles.RemoveAt(particleIndex);
			meshWaveDataDirty = true;

			int vertexIndex = particleIndex << 2;
			linearParticlesVertices[vertexIndex++].x = float.NaN;
			linearParticlesVertices[vertexIndex++].x = float.NaN;
			linearParticlesVertices[vertexIndex++].x = float.NaN;
			linearParticlesVertices[vertexIndex].x = float.NaN;
		}

		private void CheckResources()
		{
			if(waterWavesParticlesMaterial == null)
			{
				waterWavesParticlesMaterial = new Material(waterWavesParticlesShader);
				waterWavesParticlesMaterial.hideFlags = HideFlags.DontSave;
            }

			if(linearParticles == null)
				linearParticles = new List<LinearParticle>();

			if(linearParticlesMesh == null)
			{
				linearParticlesVertices = new Vector3[maxParticles * 4];

				for(int i = 0; i < linearParticlesVertices.Length; ++i)
					linearParticlesVertices[i].x = float.NaN;

				meshWaveData = new Vector4[maxParticles * 4];

				linearParticlesMesh = new Mesh();
				linearParticlesMesh.hideFlags = HideFlags.DontSave;

				var uvs = new Vector2[maxParticles * 4];

				for(int i = 0; i < uvs.Length;)
				{
					uvs[i++] = new Vector2(0.0f, 0.0f);
					uvs[i++] = new Vector2(0.0f, 1.0f);
					uvs[i++] = new Vector2(1.0f, 1.0f);
					uvs[i++] = new Vector2(1.0f, 0.0f);
				}

				var indices = new int[maxParticles * 4];

				for(int i = 0; i < indices.Length; ++i)
					indices[i] = i;

				linearParticlesMesh.vertices = linearParticlesVertices;
                linearParticlesMesh.uv = uvs;
				linearParticlesMesh.tangents = meshWaveData;
                linearParticlesMesh.SetIndices(indices, MeshTopology.Quads, 0);
			}

			if(radialParticlesMesh == null)
			{
				radialParticlesMesh = new Mesh();
				radialParticlesMesh.hideFlags = HideFlags.DontSave;
			}
		}

		private void UpdateLinearParticleData(int index)
		{
			var particle = linearParticles[index];
			var particleData = new Vector4(particle.direction.x * 2.0f * Mathf.PI / particle.frequency, particle.direction.y * 2.0f * Mathf.PI / particle.frequency, particle.width);

			int waveDataIndex = index << 2;
			meshWaveData[waveDataIndex++] = particleData;
			meshWaveData[waveDataIndex++] = particleData;
			meshWaveData[waveDataIndex++] = particleData;
			meshWaveData[waveDataIndex] = particleData;

			meshWaveDataChanged = true;
		}

		private void FreeResources()
		{
			if(waterWavesParticlesMaterial != null)
			{
				waterWavesParticlesMaterial.Destroy();
				waterWavesParticlesMaterial = null;
			}

			if(linearParticlesMesh != null)
			{
				linearParticlesMesh.Destroy();
				linearParticlesMesh = null;
			}

			if(radialParticlesMesh != null)
			{
				radialParticlesMesh.Destroy();
				radialParticlesMesh = null;
			}
		}

		sealed public class LinearParticle
		{
			public Vector2 position;
			public Vector2 direction;
			public float speed;
			public float baseFrequency;
			public float frequency;
			public float baseAmplitude;
			public float amplitude;
            public float width;
			public float spawnTime;
			public float depth;
			public float positionY;
			public Collider collider;

			public LinearParticle(Vector2 position, Vector2 direction, float frequency, float amplitude, float width, Collider collider = null)
			{
				this.position = position;
				this.direction = direction;
				this.baseFrequency = frequency;
				this.baseAmplitude = amplitude;
				this.width = width;
				this.spawnTime = Time.time;
				this.depth = (collider is TerrainCollider ? 40.0f : 1000.0f);
				this.collider = collider;

				UpdateWaveParameters();
            }

			public bool CostlyUpdate()
			{
				RaycastHit hitInfo;
				Ray ray = new Ray(new Vector3(position.x, collider.bounds.max.y + 0.5f, position.y), Vector3.down);

				if(collider.Raycast(ray, out hitInfo, 100000.0f))
				{
					depth = Mathf.Lerp(depth, positionY - hitInfo.point.y, 0.05f);
					UpdateWaveParameters();
					
					return true;
				}

				return false;
			}

			private void UpdateWaveParameters()
			{
				float tanh = Mathf.Max(0.045f, (float)System.Math.Tanh(baseFrequency * depth));
				frequency = baseFrequency / tanh;
				amplitude = baseAmplitude / tanh;
				
				speed = Mathf.Sqrt(Physics.gravity.magnitude * frequency * (float)System.Math.Tanh(frequency * depth)) / frequency;
			}
		}

		sealed public class RadialParticle
		{
			public Vector2 position;
			public float size;
		}
	}
}
