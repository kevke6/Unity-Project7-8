using System.Collections.Generic;
using UnityEngine;

#if WATER_SIMD
using vector4 = Mono.Simd.Vector4f;
#else
using vector4 = UnityEngine.Vector4;
#endif

namespace PlayWay.Water
{
	public class CpuFFTTask
	{
		private SpectrumResolverCPU.SpectrumLevel targetSpectrum;
		private float time;
		
		static private Dictionary<int, FFTBuffers> buffersCache = new Dictionary<int, FFTBuffers>();

		public void Compute(SpectrumResolverCPU.SpectrumLevel targetSpectrum, float time, int outputBufferIndex)
		{
			this.targetSpectrum = targetSpectrum;
			this.time = time;

			int resolution = targetSpectrum.Resolution;
			FFTBuffers buffers;

			if(!buffersCache.TryGetValue(resolution, out buffers))
				buffersCache[resolution] = buffers = new FFTBuffers(resolution);

			float tileSize = targetSpectrum.windWaves.TileSizes[targetSpectrum.scaleIndex];
			Vector3[] kMap = buffers.GetPrecomputedK(tileSize);

			if(targetSpectrum.directionalSpectrumDirty)
			{
				ComputeDirectionalSpectra(targetSpectrum.scaleIndex, targetSpectrum.directionalSpectrum, kMap);
				targetSpectrum.directionalSpectrumDirty = false;
            }

			ComputeTimedSpectra(targetSpectrum.directionalSpectrum, buffers.timed, kMap);
			ComputeFFT(buffers.timed, targetSpectrum.displacements[outputBufferIndex], targetSpectrum.forceAndHeight[outputBufferIndex], buffers.indices, buffers.weights, buffers.pingPongA, buffers.pingPongB);
		}

		private void ComputeDirectionalSpectra(int scaleIndex, Vector2[] directional, Vector3[] kMap)
		{
			int resolution = targetSpectrum.Resolution;
			int halfResolution = resolution / 2;
			float directionality = 1.0f - targetSpectrum.water.Directionality;
			var cachedSpectra = targetSpectrum.windWaves.SpectrumResolver.GetCachedSpectraDirect();
			int resolutionSqr = resolution * resolution;

			Vector2 windDirection = targetSpectrum.windWaves.SpectrumResolver.WindDirection;

			for(int i = 0; i < resolutionSqr; ++i)
			{
				directional[i].x = 0.0f;
				directional[i].y = 0.0f;
			}

			foreach(var spectrum in cachedSpectra.Values)
			{
				float w = spectrum.Weight;

				if(w <= 0.005f)
					continue;

				int index = 0;
				Vector3[,] omnidirectional = spectrum.GetSpectrumValues(resolution)[scaleIndex];

				for(int y = 0; y < resolution; ++y)
				{
					for(int x = 0; x < resolution; ++x)
					{
						float nkx = kMap[index].x;
						float nky = kMap[index].y;
						
						if(nkx == 0.0f && nky == 0.0f)
						{
							nkx = windDirection.x;
							nky = windDirection.y;
						}

						float dp = windDirection.x * nkx + windDirection.y * nky;
						float phi = Mathf.Acos(dp * 0.999f);

						float directionalFactor = Mathf.Sqrt(1.0f + omnidirectional[x, y].z * Mathf.Cos(2.0f * phi));

						if(dp < 0)
							directionalFactor *= directionality;

						directional[index].x += omnidirectional[x, y].x * directionalFactor * w;
						directional[index++].y += omnidirectional[x, y].y * directionalFactor * w;
					}
				}
			}
		}

		private void ComputeTimedSpectra(Vector2[] directional, CpuSpectrumValue[] timed, Vector3[] kMap)
		{
			int resolution = targetSpectrum.Resolution;
			int resolutionSquared = resolution * resolution - 1;
			int halfResolution = resolution / 2;
			float gravity = targetSpectrum.water.Gravity;

			Vector2 windDirection = targetSpectrum.windWaves.SpectrumResolver.WindDirection;

			float tileSize = targetSpectrum.windWaves.TileSizes[targetSpectrum.scaleIndex];
			float frequencyScale = 2.0f * Mathf.PI / tileSize;
			int index = 0;

			for(int y = 0; y < resolution; ++y)
			{
				for(int x = 0; x < resolution; ++x)
				{
					float nkx = kMap[index].x;
					float nky = kMap[index].y;
					float k = kMap[index].z;

					if(nkx == 0.0f && nky == 0.0f)
					{
						nkx = windDirection.x;
						nky = windDirection.y;
					}

					int index2 = resolution * ((resolution - y) % resolution) + (resolution - x) % resolution;
					
					Vector2 s1 = directional[index];
					Vector2 s2 = directional[index2];

					float t = time * Mathf.Sqrt(gravity * k);

					float s = Mathf.Sin(t);
					float c = Mathf.Cos(t);
					//float s, c;
					//FastMath.SinCos2048(t, out s, out c);			// inlined below
					//int icx = ((int)(t * 325.949f) & 2047);
					//float s = FastMath.sines[icx];
					//float c = FastMath.cosines[icx];
					
					float sx = (s1.x + s2.x) * c - (s1.y + s2.y) * s;
					float sy = (s1.x - s2.x) * s + (s1.y - s2.y) * c;

					// height
					timed[index].dy0 = sx;
					timed[index].dy1 = sy;

					// displacement
					timed[index].dx0 = sy * nkx;
					timed[index].dx1 = -sx * nkx;
					timed[index].dz0 = sy * nky;
					timed[index].dz1 = -sx * nky;

					// force
					timed[index].fx0 = sx * nkx;
					timed[index].fx1 = sy * nkx;
					timed[index].fz0 = sx * nky;
					timed[index].fz1 = sy * nky;
					timed[index].fy0 = sy;
					timed[index++].fy1 = -sx;
				}
			}
		}

		private void ComputeFFT(CpuSpectrumValue[] data, Vector2[] displacements, vector4[] forceAndHeight, int[][] indices, Vector2[][] weights, CpuSpectrumValue[] pingPongA, CpuSpectrumValue[] pingPongB)
		{
			int resolution = pingPongA.Length;
			int index = 0;

			for(int y = resolution - 1; y >= 0; --y)
			{
				index += resolution;

				int index2 = index;

				for(int x = resolution - 1; x >= 0; --x)
					pingPongA[x] = data[--index2];

				FFT(indices, weights, ref pingPongA, ref pingPongB);

				index2 = index;

				for(int x = resolution - 1; x >= 0; --x)
					data[--index2] = pingPongA[x];
			}

			index = resolution * (resolution + 1);

			for(int x = resolution - 1; x >= 0; --x)
			{
				--index;

				int index2 = index;

				for(int y = resolution - 1; y >= 0; --y)
					pingPongA[y] = data[index2 -= resolution];

				FFT(indices, weights, ref pingPongA, ref pingPongB);

				index2 = index;

				for(int y = resolution - 1; y >= 0; --y)
				{
					index2 -= resolution;
					
					forceAndHeight[index2] = new vector4(-pingPongA[y].fx1, pingPongA[y].fy1, -pingPongA[y].fz1, pingPongA[y].dy0);
					displacements[index2] = new Vector2(pingPongA[y].dx0, pingPongA[y].dz0);
				}
			}
		}

		private void FFT(int[][] indices, Vector2[][] weights, ref CpuSpectrumValue[] pingPongA, ref CpuSpectrumValue[] pingPongB)
		{
			int resolution = pingPongA.Length;
			int numButterflies = weights.Length;

			for(int butterflyIndex = 0; butterflyIndex < numButterflies; ++butterflyIndex)
			{
				var localIndices = indices[numButterflies - butterflyIndex - 1];
				var localWeights = weights[butterflyIndex];

				for(int i = resolution - 1; i >= 0; --i)
				{
					int ix = localIndices[i << 1];
					int iy = localIndices[(i << 1) + 1];
					float wx = localWeights[i].x;
					float wy = localWeights[i].y;

					CpuSpectrumValue a1 = pingPongA[ix];
					CpuSpectrumValue b1 = pingPongA[iy];

					pingPongB[i].dx0 = a1.dx0 + wx * b1.dx0 - wy * b1.dx1;
					pingPongB[i].dx1 = a1.dx1 + wy * b1.dx0 + wx * b1.dx1;
					pingPongB[i].dy0 = a1.dy0 + wx * b1.dy0 - wy * b1.dy1;
					pingPongB[i].dy1 = a1.dy1 + wy * b1.dy0 + wx * b1.dy1;
					pingPongB[i].dz0 = a1.dz0 + wx * b1.dz0 - wy * b1.dz1;
					pingPongB[i].dz1 = a1.dz1 + wy * b1.dz0 + wx * b1.dz1;

					pingPongB[i].fx0 = a1.fx0 + wx * b1.fx0 - wy * b1.fx1;
					pingPongB[i].fx1 = a1.fx1 + wy * b1.fx0 + wx * b1.fx1;
					pingPongB[i].fy0 = a1.fy0 + wx * b1.fy0 - wy * b1.fy1;
					pingPongB[i].fy1 = a1.fy1 + wy * b1.fy0 + wx * b1.fy1;
					pingPongB[i].fz0 = a1.fz0 + wx * b1.fz0 - wy * b1.fz1;
					pingPongB[i].fz1 = a1.fz1 + wy * b1.fz0 + wx * b1.fz1;
				}

				var t = pingPongA;
				pingPongA = pingPongB;
				pingPongB = t;
			}
		}
		
		public class FFTBuffers
		{
			public CpuSpectrumValue[] timed;
			public CpuSpectrumValue[] pingPongA;
			public CpuSpectrumValue[] pingPongB;
			public int[][] indices;
			public Vector2[][] weights;
			public Vector4[] precomputedK;			// kx, ky, nkx, nky
			public int numButterflies;
			private int resolution;

			private Dictionary<float, Vector3[]> precomputedKMap = new Dictionary<float, Vector3[]>(new FloatEqualityComparer());

			public FFTBuffers(int resolution)
			{
				this.resolution = resolution;
				timed = new CpuSpectrumValue[resolution * resolution];
				pingPongA = new CpuSpectrumValue[resolution];
				pingPongB = new CpuSpectrumValue[resolution];
				numButterflies = (int)(Mathf.Log((float)resolution) / Mathf.Log(2.0f));
				
				ButterflyFFTUtility.ComputeButterfly(resolution, numButterflies, out indices, out weights);
			}

			public Vector3[] GetPrecomputedK(float tileSize)
			{
				Vector3[] map;

				if(!precomputedKMap.TryGetValue(tileSize, out map))
				{
					int halfResolution = resolution >> 1;
					float frequencyScale = 2.0f * Mathf.PI / tileSize;

					map = new Vector3[resolution * resolution];
					int index = 0;

					for(int y = 0; y < resolution; ++y)
					{
						int v = (y + halfResolution) % resolution;
						float ky = frequencyScale * (v/* + 0.5f*/ - halfResolution);

						for(int x = 0; x < resolution; ++x)
						{
							int u = (x + halfResolution) % resolution;
							float kx = frequencyScale * (u/* + 0.5f*/ - halfResolution);

							float k = Mathf.Sqrt(kx * kx + ky * ky);

							map[index++] = new Vector3(k != 0 ? kx / k : 0.0f, k != 0 ? ky / k : 0.0f, k);
						}
					}

					precomputedKMap[tileSize] = map;
				}

				return map;
            }
		}
	}

	public struct CpuSpectrumValue
	{
		public float dx0, dx1;
		public float dy0, dy1;
		public float dz0, dz1;
		public float fx0, fx1;
		public float fy0, fy1;
		public float fz0, fz1;
	}

	public struct CpuWavemapData
	{
		public float dx, dy, dz;
		public float fx, fy, fz;
	}
}
