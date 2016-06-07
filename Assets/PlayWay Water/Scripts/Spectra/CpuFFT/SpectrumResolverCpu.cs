using System.Collections.Generic;
using UnityEngine;

#if WATER_SIMD
using vector4 = Mono.Simd.Vector4f;
#else
using vector4 = UnityEngine.Vector4;
#endif

namespace PlayWay.Water
{
	public class SpectrumResolverCPU
	{
		private Water water;
		private WindWaves windWaves;
		protected Dictionary<WaterWavesSpectrum, WaterWavesSpectrumData> spectraDataCache;
		private List<WaterWavesSpectrumData> spectraDataList;

		private List<CpuFFTTask> workers;
		private SpectrumLevel[] spectraLevels;
		private Vector2 surfaceOffset;
		private Vector2 windDirection;
		private int numScales;
		private float lastFrameTime;

		// statistical data
		private float totalAmplitude;
		private float maxVerticalDisplacement;
		private float maxHorizontalDisplacement;
		
		public SpectrumResolverCPU(WindWaves windWaves, int numScales)
		{
			this.water = windWaves.GetComponent<Water>();
			this.windWaves = windWaves;
			this.spectraDataCache = new Dictionary<WaterWavesSpectrum, WaterWavesSpectrumData>();
			this.spectraDataList = new List<WaterWavesSpectrumData>();
            this.numScales = numScales;

			CreateSpectraLevels();
		}

		public float TotalAmplitude
		{
			get { return totalAmplitude; }
		}

		public float MaxVerticalDisplacement
		{
			get { return maxVerticalDisplacement; }
		}

		public float MaxHorizontalDisplacement
		{
			get { return maxHorizontalDisplacement; }
		}

		public int AvgCpuWaves
		{
			get
			{
				int cpuWavesCount = 0;

				foreach(var spectrumData in spectraDataCache.Values)
					cpuWavesCount += spectrumData.CpuWavesCount;

				return Mathf.RoundToInt(((float)cpuWavesCount) / spectraDataCache.Count);
			}
		}

		public Vector2 WindDirection
		{
			get { return windDirection; }
		}

		public float LastFrameTime
		{
			get { return lastFrameTime; }
		}

		internal void Update()
		{
			surfaceOffset = water.SurfaceOffset;
			lastFrameTime = water.Time;

			int numSamples = water.ComputedSamplesCount;
			int waveThreshold = numSamples * 100;

			bool allowFFT = WaterProjectSettings.Instance.AllowCpuFFT;

			for(int scaleIndex=0; scaleIndex < numScales; ++scaleIndex)
			{
				int numWaves = 0;

				for(int spectrumIndex = 0; spectrumIndex < spectraDataList.Count; ++spectrumIndex)
				{
					var spectrum = spectraDataList[spectrumIndex];
                    var cpuWaves = spectrum.GetCpuWavesDirect();

					if(cpuWaves != null)
						numWaves += (int)(cpuWaves[scaleIndex].Length * spectrum.Weight);
				}

				int fftResolution = numWaves > 160 ? (numWaves > 900 ? 64 : 32) : 16;

				var level = spectraLevels[scaleIndex];
				level.SetResolveMode((numWaves * numSamples) > (((fftResolution * fftResolution) << 2) + numSamples) && allowFFT, fftResolution);
			}

#if WATER_DEBUG
			// debug
			if(Input.GetKeyDown(KeyCode.F10))
			{
				for(int i = 0; i < 4; ++i)
				{
					if(!spectraLevels[i].IsResolvedByFFT) continue;

					int resolution = spectraLevels[i].Resolution;

					lock (spectraLevels[i])
					{
						var tex = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false, true);
						for(int y = 0; y < resolution; ++y)
						{
							for(int x = 0; x < resolution; ++x)
							{
								tex.SetPixel(x, y, new Color(spectraLevels[i].directionalSpectrum[y * resolution + x].x, spectraLevels[i].directionalSpectrum[y * resolution + x].y, 0.0f, 1.0f));
							}
						}

						tex.Apply();
						var bytes = tex.EncodeToPNG();
						System.IO.File.WriteAllBytes("CPU Dir " + i + ".png", bytes);

						tex.Destroy();
					}
				}
				
				for(int i = 0; i < 4; ++i)
				{
					if(!spectraLevels[i].IsResolvedByFFT) continue;

					int resolution = spectraLevels[i].Resolution;

					var tex = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false, true);
					for(int y = 0; y < resolution; ++y)
					{
						for(int x = 0; x < resolution; ++x)
						{
							tex.SetPixel(x, y, new Color(spectraLevels[i].displacements[1][y * resolution + x].x, 0.0f, 0.0f, 1.0f));
						}
					}

					tex.Apply();
					var bytes = tex.EncodeToPNG();
					System.IO.File.WriteAllBytes("CPU FFT " + i + ".png", bytes);

					tex.Destroy();
				}
			}
#endif
		}

		internal void SetWindDirection(Vector2 windDirection)
		{
			this.windDirection = windDirection;
			InvalidateDirectionalSpectrum();
		}
		
		public WaterWavesSpectrumData GetSpectrumData(WaterWavesSpectrum spectrum)
		{
			WaterWavesSpectrumData spectrumData;

			if(!spectraDataCache.TryGetValue(spectrum, out spectrumData))
			{
				spectraDataCache[spectrum] = spectrumData = new WaterWavesSpectrumData(water, spectrum);
				spectrumData.ValidateSpectrumData();

				lock (spectraDataList)
				{
					spectraDataList.Add(spectrumData);
				}
			}

			return spectrumData;
		}

		public void CacheSpectrum(WaterWavesSpectrum spectrum)
		{
			GetSpectrumData(spectrum);
		}

		public Dictionary<WaterWavesSpectrum, WaterWavesSpectrumData> GetCachedSpectraDirect()
		{
			return spectraDataCache;
		}

		#region WavemapsSampling
		private void InterpolationParams(float x, float z, int scaleIndex, float tileSize, out float fx, out float invFx, out float fy, out float invFy, out int index0, out int index1, out int index2, out int index3)
		{
			int resolution = spectraLevels[scaleIndex].Resolution;
			int displayResolution = windWaves.FinalResolution;
			x += (-1.0f / resolution + 0.5f / displayResolution) * tileSize;
			z += (-1.0f / resolution + 0.5f / displayResolution) * tileSize;

			float multiplier = resolution / tileSize;
			fx = x * multiplier;
			fy = z * multiplier;
			int indexX = (int)fx;
			int indexY = (int)fy;
			fx -= indexX;
			fy -= indexY;

			if(fx < 0) fx += 1.0f;
			if(fy < 0) fy += 1.0f;

			indexX = indexX % resolution;
			indexY = indexY % resolution;

			if(indexX < 0) indexX += resolution;
			if(indexY < 0) indexY += resolution;

			indexX = resolution - indexX - 1;
			indexY = resolution - indexY - 1;

			int indexX_2 = indexX + 1;
			int indexY_2 = indexY + 1;

			if(indexX_2 == resolution) indexX_2 = 0;
			if(indexY_2 == resolution) indexY_2 = 0;

			indexY *= resolution;
			indexY_2 *= resolution;

			index0 = indexY + indexX;
			index1 = indexY + indexX_2;
			index2 = indexY_2 + indexX;
			index3 = indexY_2 + indexX_2;

			invFx = 1.0f - fx;
			invFy = 1.0f - fy;
		}

		public Vector3 GetDisplacementAt(float x, float z, float spectrumStart, float spectrumEnd, float time, ref bool completed)
		{
			Vector3 result = new Vector3();
			x = -(x + surfaceOffset.x);
			z = -(z + surfaceOffset.y);

			// sample FFT results
			if(spectrumStart == 0.0f)
			{
				for(int scaleIndex = numScales - 1; scaleIndex >= 0; --scaleIndex)
				{
					if(spectraLevels[scaleIndex].resolveByFFT)
					{
						float fx, invFx, fy, invFy, t; int index0, index1, index2, index3;
						InterpolationParams(x, z, scaleIndex, windWaves.TileSizes[scaleIndex], out fx, out invFx, out fy, out invFy, out index0, out index1, out index2, out index3);

						Vector2[] da, db;
						vector4[] fa, fb;
						spectraLevels[scaleIndex].GetResults(time, out da, out db, out fa, out fb, out t);

						Vector2 subResult = FastMath.Interpolate(
							ref da[index0], ref da[index1], ref da[index2], ref da[index3],
							ref db[index0], ref db[index1], ref db[index2], ref db[index3],
							fx, invFx, fy, invFy, t
						);

						result.x -= subResult.x;
						result.z -= subResult.y;

#if WATER_SIMD
						result.y += FastMath.Interpolate(
							fa[index0].W, fa[index1].W, fa[index2].W, fa[index3].W,
							fb[index0].W, fb[index1].W, fb[index2].W, fb[index3].W,
							fx, invFx, fy, invFy, t
						);
#else
						result.y += FastMath.Interpolate(
							fa[index0].w, fa[index1].w, fa[index2].w, fa[index3].w,
							fb[index0].w, fb[index1].w, fb[index2].w, fb[index3].w,
							fx, invFx, fy, invFy, t
						);
#endif
					}
				}
			}

			// sample waves directly
			SampleWavesDirectly(spectrumStart, spectrumEnd, ref completed, (cpuWaves, startIndex, endIndex, weight) =>
			{
				Vector3 subResult = new Vector3();

				for(int i = startIndex; i < endIndex; ++i)
					subResult += cpuWaves[i].GetDisplacementAt(x, z, time);

				result += subResult * weight;
			});

			float scale = -water.HorizontalDisplacementScale;
			result.x = result.x * scale;
			result.z = result.z * scale;

			return result;
		}

		public Vector2 GetHorizontalDisplacementAt(float x, float z, float spectrumStart, float spectrumEnd, float time, ref bool completed)
		{
			Vector2 result = new Vector2();
			x = -(x + surfaceOffset.x);
			z = -(z + surfaceOffset.y);

			// sample FFT results
			if(spectrumStart == 0.0f)
			{
				for(int scaleIndex = numScales - 1; scaleIndex >= 0; --scaleIndex)
				{
					if(spectraLevels[scaleIndex].resolveByFFT)
					{
						float fx, invFx, fy, invFy, t; int index0, index1, index2, index3;
						InterpolationParams(x, z, scaleIndex, windWaves.TileSizes[scaleIndex], out fx, out invFx, out fy, out invFy, out index0, out index1, out index2, out index3);

						Vector2[] da, db;
						vector4[] fa, fb;
						spectraLevels[scaleIndex].GetResults(time, out da, out db, out fa, out fb, out t);
						
						result -= FastMath.Interpolate(
							ref da[index0], ref da[index1], ref da[index2], ref da[index3],
							ref db[index0], ref db[index1], ref db[index2], ref db[index3],
							fx, invFx, fy, invFy, t
						);
					}
				}
			}

			// sample waves directly
			SampleWavesDirectly(spectrumStart, spectrumEnd, ref completed, (cpuWaves, startIndex, endIndex, weight) =>
			{
				Vector2 subResult = new Vector2();

				for(int i = startIndex; i < endIndex; ++i)
					subResult += cpuWaves[i].GetRawHorizontalDisplacementAt(x, z, time);

				result += subResult * weight;
			});

			float scale = -water.HorizontalDisplacementScale;
			result.x = result.x * scale;
			result.y = result.y * scale;

			return result;
		}

		public Vector4 GetForceAndHeightAt(float x, float z, float spectrumStart, float spectrumEnd, float time, ref bool completed)
		{
			vector4 result = new vector4();
			x = -(x + surfaceOffset.x);
			z = -(z + surfaceOffset.y);

			// sample FFT results
			if(spectrumStart == 0.0f)
			{
				for(int scaleIndex = numScales - 1; scaleIndex >= 0; --scaleIndex)
				{
					if(spectraLevels[scaleIndex].resolveByFFT)
					{
						float fx, invFx, fy, invFy, t; int index0, index1, index2, index3;
						InterpolationParams(x, z, scaleIndex, windWaves.TileSizes[scaleIndex], out fx, out invFx, out fy, out invFy, out index0, out index1, out index2, out index3);

						Vector2[] da, db;
						vector4[] fa, fb;
						spectraLevels[scaleIndex].GetResults(time, out da, out db, out fa, out fb, out t);
						
						result += FastMath.Interpolate(
							fa[index0], fa[index1], fa[index2], fa[index3],
							fb[index0], fb[index1], fb[index2], fb[index3],
							fx, invFx, fy, invFy, t
						);
					}
				}
			}

			// sample waves directly
			SampleWavesDirectly(spectrumStart, spectrumEnd, ref completed, (cpuWaves, startIndex, endIndex, weight) =>
			{
				Vector4 subResult = new Vector4();

				for(int i = startIndex; i < endIndex; ++i)
					cpuWaves[i].GetForceAndHeightAt(x, z, time, ref subResult);

#if WATER_SIMD
				result.X += subResult.x * weight;
				result.Y += subResult.y * weight;
				result.Z += subResult.z * weight;
				result.W += subResult.w * weight;
#else
				result += subResult * weight;
#endif
			});

			float scale = water.HorizontalDisplacementScale;

#if WATER_SIMD
			result.X = result.X * scale;
			result.Z = result.Z * scale;
			result.Y *= 0.25f;

			return new Vector4(result.X, result.Y, result.Z, result.W);
#else
			result.x = result.x * scale;
			result.z = result.z * scale;
			result.y *= 0.25f;

			return result;
#endif
		}

		public float GetHeightAt(float x, float z, float spectrumStart, float spectrumEnd, float time, ref bool completed)
		{
			float result = 0.0f;
			x = -(x + surfaceOffset.x);
			z = -(z + surfaceOffset.y);

			// sample FFT results
			if(spectrumStart == 0.0f)
			{
				for(int scaleIndex = numScales - 1; scaleIndex >= 0; --scaleIndex)
				{
					if(spectraLevels[scaleIndex].resolveByFFT)
					{
						float fx, invFx, fy, invFy, t; int index0, index1, index2, index3;
						InterpolationParams(x, z, scaleIndex, windWaves.TileSizes[scaleIndex], out fx, out invFx, out fy, out invFy, out index0, out index1, out index2, out index3);

						Vector2[] da, db;
						vector4[] fa, fb;
						spectraLevels[scaleIndex].GetResults(time, out da, out db, out fa, out fb, out t);

#if WATER_SIMD
						result += FastMath.Interpolate(
							fa[index0].W, fa[index1].W, fa[index2].W, fa[index3].W,
							fb[index0].W, fb[index1].W, fb[index2].W, fb[index3].W,
							fx, invFx, fy, invFy, t
						);
#else
						result += FastMath.Interpolate(
							fa[index0].w, fa[index1].w, fa[index2].w, fa[index3].w,
							fb[index0].w, fb[index1].w, fb[index2].w, fb[index3].w,
							fx, invFx, fy, invFy, t
						);
#endif
					}
				}
			}

			// sample waves directly
			SampleWavesDirectly(spectrumStart, spectrumEnd, ref completed, (cpuWaves, startIndex, endIndex, weight) =>
			{
				float subResult = 0.0f;

				for(int i = startIndex; i < endIndex; ++i)
					subResult += cpuWaves[i].GetHeightAt(x, z, time);

				result += subResult * weight;
			});

			return result;
		}
		
		private void SampleWavesDirectly(float spectrumStart, float spectrumEnd, ref bool completed, System.Action<WaterWave[], int, int, float> func)
		{
			if(spectrumEnd == 0.0f)
				return;

			float threshold = 0.001f + spectrumStart * spectrumStart;

			for(int scaleIndex = numScales - 1; scaleIndex >= 0; --scaleIndex)
			{
				if(spectraLevels[scaleIndex].resolveByFFT)
					continue;

				int numSpectra = spectraDataList.Count;

				for(int spectrumIndex = 0; spectrumIndex < numSpectra; ++spectrumIndex)
				{
					WaterWavesSpectrumData spectrum;

					lock (spectraDataList)
					{
						if(spectraDataList.Count <= spectrumIndex)
							return;

						spectrum = spectraDataList[spectrumIndex];
					}

					if(spectrum.Weight < threshold) continue;

					spectrum.UpdateSpectralValues(windDirection, water.Directionality);
					
					var cpuWaves = spectrum.GetCpuWavesDirect();

					lock(cpuWaves)
					{
						var cpuWavesLevel = cpuWaves[scaleIndex];

						if(cpuWavesLevel.Length == 0)
							continue;

                        int startIndex = (int)(spectrumStart * cpuWavesLevel.Length);
						int endIndex = (int)(spectrumEnd * cpuWavesLevel.Length);

						if(startIndex < endIndex)
							func(cpuWavesLevel, startIndex, endIndex, spectrum.Weight);
					}

					completed = false;
				}
			}
		}
#endregion

#region WavesSelecting
		public GerstnerWave[] FindMostMeaningfulWaves(int count, bool mask)
		{
			var list = new List<FoundWave>();

			foreach(var spectrum in spectraDataList)
			{
				if(spectrum.Weight < 0.001f)
					continue;

				spectrum.UpdateSpectralValues(windDirection, water.Directionality);

				var waveMaps = spectrum.GetCpuWavesDirect();

				for(int scaleIndex = 0; scaleIndex < numScales; ++scaleIndex)
				{
					var waves = waveMaps[scaleIndex];
					int numWaves = Mathf.Min(waves.Length, count);

					for(int i = 0; i < numWaves; ++i)
						list.Add(new FoundWave(spectrum, waves[i]));
				}
			}

			list.Sort((a, b) => b.importance.CompareTo(a.importance));

			// compute texture offsets from the FFT shader to match Gerstner waves to FFT
			Vector2[] offsets = new Vector2[4];

			for(int i = 0; i < 4; ++i)
			{
				float tileSize = windWaves.TileSizes[i];

				offsets[i].x = tileSize + (0.5f / windWaves.FinalResolution) * tileSize;
				offsets[i].y = -tileSize + (0.5f / windWaves.FinalResolution) * tileSize;
			}

			var gerstners = new GerstnerWave[count];

			for(int i = 0; i < count; ++i)
				gerstners[i] = list[i].ToGerstner(offsets);

			return gerstners;
		}

		public Gerstner4[] FindGerstners(int count, bool mask)
		{
			var list = new List<FoundWave>();

			foreach(var spectrum in spectraDataCache.Values)
			{
				if(spectrum.Weight < 0.001f)
					continue;

				spectrum.UpdateSpectralValues(windDirection, water.Directionality);
				
				var waveMaps = spectrum.GetCpuWavesDirect();

				for(int scaleIndex = 0; scaleIndex < numScales; ++scaleIndex)
				{
					var waves = waveMaps[scaleIndex];
					int numWaves = Mathf.Min(waves.Length, count);

					for(int i = 0; i < numWaves; ++i)
						list.Add(new FoundWave(spectrum, waves[i]));
				}
			}

			list.Sort((a, b) => b.importance.CompareTo(a.importance));

			int index = 0;
			int numFours = (count >> 2);
			var result = new Gerstner4[numFours];

			// compute texture offsets from the FFT shader to match Gerstner waves to FFT
			Vector2[] offsets = new Vector2[4];

			for(int i = 0; i < 4; ++i)
			{
				float tileSize = windWaves.TileSizes[i];

				offsets[i].x = tileSize + (0.5f / windWaves.FinalResolution) * tileSize;
				offsets[i].y = -tileSize + (0.5f / windWaves.FinalResolution) * tileSize;
			}

			for(int i = 0; i < numFours; ++i)
			{
				var wave0 = index < list.Count ? list[index++].ToGerstner(offsets) : new GerstnerWave();
				var wave1 = index < list.Count ? list[index++].ToGerstner(offsets) : new GerstnerWave();
				var wave2 = index < list.Count ? list[index++].ToGerstner(offsets) : new GerstnerWave();
				var wave3 = index < list.Count ? list[index++].ToGerstner(offsets) : new GerstnerWave();

				result[i] = new Gerstner4(wave0, wave1, wave2, wave3);
			}

			//if(mask)
			//	foundWave.spectrum.texture.SetPixel(wave.u, wave.v, new Color(0.0f, 0.0f, 0.0f, 0.0f));

			/*if(mask)
			{
				foreach(var spectrum in spectra)
					spectrum.texture.Apply(false, false);

				ComputeTotalSpectrum();
				directionalSpectrumDirty = true;
			}*/

			return result;
		}
#endregion

		virtual internal void OnProfilesChanged()
		{
			var profiles = water.Profiles;

			foreach(var spectrumData in spectraDataCache.Values)
				spectrumData.Weight = 0.0f;

			foreach(var weightedProfile in profiles)
			{
				if(weightedProfile.weight <= 0.0001f)
					continue;

				var spectrum = weightedProfile.profile.Spectrum;

				WaterWavesSpectrumData spectrumData;

				if(!spectraDataCache.TryGetValue(spectrum, out spectrumData))
					spectrumData = GetSpectrumData(spectrum);

				spectrumData.Weight = weightedProfile.weight;
			}

			totalAmplitude = 0.0f;

			foreach(var spectrumData in spectraDataCache.Values)
				totalAmplitude += spectrumData.TotalAmplitude * spectrumData.Weight;

			maxVerticalDisplacement = totalAmplitude * 0.12f;
			maxHorizontalDisplacement = Mathf.Sqrt(FastMath.Pow2(maxVerticalDisplacement) + FastMath.Pow2(maxVerticalDisplacement * water.HorizontalDisplacementScale));

			InvalidateDirectionalSpectrum();
		}

		private void CreateSpectraLevels()
		{
			this.spectraLevels = new SpectrumLevel[numScales];

			for(int scaleIndex = 0; scaleIndex < numScales; ++scaleIndex)
				spectraLevels[scaleIndex] = new SpectrumLevel(windWaves, scaleIndex);
		}

		virtual protected void InvalidateDirectionalSpectrum()
		{
			foreach(var spectrum in spectraDataList)
				spectrum.SetCpuWavesDirty();

			for(int scaleIndex = 0; scaleIndex < numScales; ++scaleIndex)
				spectraLevels[scaleIndex].directionalSpectrumDirty = true;
		}

		virtual internal void OnMapsFormatChanged(bool resolution)
		{
			if(spectraDataCache != null)
			{
				foreach(var spectrumData in spectraDataCache.Values)
					spectrumData.Dispose(!resolution);
			}

			InvalidateDirectionalSpectrum();
		}

		virtual internal void OnDestroy()
		{
			OnMapsFormatChanged(true);
			spectraDataCache = null;

			lock(spectraDataList)
			{
				spectraDataList.Clear();
			}
		}

		public class SpectrumLevel
		{
			// work-time data
			public Vector2[] directionalSpectrum;

			// results
			public Vector2[][] displacements;
			public vector4[][] forceAndHeight;
			public float[] resultsTiming;
			public int recentResultIndex;

			// cache
			public float cachedTime = float.NegativeInfinity;
			public float cachedTimeProp;
			public Vector2[] cachedDisplacementsA, cachedDisplacementsB;
			public vector4[] cachedForceAndHeightA, cachedForceAndHeightB;
			
			// state and context
			public bool resolveByFFT;
			public bool directionalSpectrumDirty;
			public int scaleIndex;
			private int resolution;
			public WindWaves windWaves;
			public Water water;

			public SpectrumLevel(WindWaves windWaves, int index)
			{
				this.windWaves = windWaves;
				this.water = windWaves.GetComponent<Water>();
				this.scaleIndex = index;
			}

			public bool IsResolvedByFFT
			{
				get { return resolveByFFT; }
			}

			public int Resolution
			{
				get { return resolution; }
			}

			public void SetResolveMode(bool resolveByFFT, int resolution)
			{
				if(this.resolveByFFT != resolveByFFT || (this.resolveByFFT && this.resolution != resolution))
				{
					if(resolveByFFT)
					{
						this.resolution = resolution;
						int resolutionSquared = resolution * resolution;
						directionalSpectrum = new Vector2[resolutionSquared];
						displacements = new Vector2[4][];
						forceAndHeight = new vector4[4][];
						resultsTiming = new float[4];

						for(int i = 0; i < 4; ++i)
						{
							displacements[i] = new Vector2[resolutionSquared];
							forceAndHeight[i] = new vector4[resolutionSquared];
						}

						if(this.resolveByFFT == false)
						{
							WaterAsynchronousTasks.Instance.AddFFTComputations(this);
							this.resolveByFFT = true;
						}
					}
					else
					{
						WaterAsynchronousTasks.Instance.RemoveFFTComputations(this);
						this.resolveByFFT = false;
					}
				}
			}

			public void GetResults(float time, out Vector2[] da, out Vector2[] db, out vector4[] fa, out vector4[] fb, out float p)
			{
				if(time == cachedTime)
				{
					// there is a very minor chance of threads reading/writing this in the same time, but this shouldn't have noticeable consequences and should be extremely rare
					da = cachedDisplacementsA;
					db = cachedDisplacementsB;
					fa = cachedForceAndHeightA;
					fb = cachedForceAndHeightB;
					p = cachedTimeProp;

					return;
				}

				int recentResultIndex = this.recentResultIndex;

				for(int i = recentResultIndex - 1; i >= 0; --i)
				{
					if(resultsTiming[i] <= time)
					{
						int nextIndex = i + 1;

						da = displacements[i];
						db = displacements[nextIndex];
						fa = forceAndHeight[i];
						fb = forceAndHeight[nextIndex];

						float duration = resultsTiming[nextIndex] - resultsTiming[i];

						if(duration != 0.0f)
							p = (time - resultsTiming[i]) / duration;
						else
							p = 0.0f;

						if(time > cachedTime)
						{
							cachedTime = time;
							cachedDisplacementsA = da;
							cachedDisplacementsB = db;
							cachedForceAndHeightA = fa;
							cachedForceAndHeightB = fb;
							cachedTimeProp = p;
						}

						return;
					}
				}

				for(int i = resultsTiming.Length - 1; i > recentResultIndex; --i)
				{
					if(resultsTiming[i] <= time)
					{
						int nextIndex = i != displacements.Length - 1 ? i + 1 : 0;

						da = displacements[i];
						db = displacements[nextIndex];
						fa = forceAndHeight[i];
						fb = forceAndHeight[nextIndex];

						float duration = resultsTiming[nextIndex] - resultsTiming[i];

						if(duration != 0.0f)
							p = (time - resultsTiming[i]) / duration;
						else
							p = 0.0f;

						return;
					}
				}

				da = displacements[recentResultIndex];
				db = displacements[recentResultIndex];
				fa = forceAndHeight[recentResultIndex];
				fb = forceAndHeight[recentResultIndex];
				p = 0.0f;
			}
		}

		private class FoundWave
		{
			public WaterWavesSpectrumData spectrum;
			public WaterWave wave;
			public float importance;

			public FoundWave(WaterWavesSpectrumData spectrum, WaterWave wave)
			{
				this.spectrum = spectrum;
				this.wave = wave;

				importance = wave.cpuPriority * spectrum.Weight;
			}

			public GerstnerWave ToGerstner(Vector2[] scaleOffsets)
			{
				float speed = wave.w;
				float mapOffset = (scaleOffsets[wave.scaleIndex].x * wave.nkx + scaleOffsets[wave.scaleIndex].y * wave.nky) * wave.k;       // match Gerstner to FFT map equivalent

				return new GerstnerWave(new Vector2(wave.nkx, wave.nky), wave.amplitude * spectrum.Weight, mapOffset + wave.offset, wave.k, speed);
			}
		}
	}
}
