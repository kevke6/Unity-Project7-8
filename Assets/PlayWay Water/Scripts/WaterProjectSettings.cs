using UnityEngine;

namespace PlayWay.Water
{
	public class WaterProjectSettings : ScriptableObjectSingleton
	{
		static public readonly float CurrentVersion = 1.14f;            // 1.1 RC4
		static public readonly string CurrentVersionString = "1.1 RC4";

		[SerializeField]
		private int waterLayer = 4;

		[Tooltip("Used for some camera effects. Needs to be unused. You don't need to mask it on your cameras.")]
		[SerializeField]
		private int waterTempLayer = 22;

		[Tooltip("More threads increase physics precision under stress, but also decrease overall performance a bit.")]
		[SerializeField]
		private int physicsThreads = 1;

		[SerializeField]
		private System.Threading.ThreadPriority physicsThreadsPriority = System.Threading.ThreadPriority.BelowNormal;

		[SerializeField]
		private bool allowCpuFFT = true;

		[Tooltip("Some hardware doesn't support floating point mip maps correctly and they are forcefully disabled. You may simulate how the water would look like on such hardware by disabling this option. Most notably fp mip maps don't work correctly on most AMD graphic cards (for now).")]
		[SerializeField]
		private bool allowFloatingPointMipMaps = true;

		[SerializeField]
		private bool debugPhysics = false;
		
		static private WaterProjectSettings instance;

		static public WaterProjectSettings Instance
		{
			get
			{
				if(instance == null)
					instance = LoadSingleton<WaterProjectSettings>();

				return instance;
			}
		}

		public int PhysicsThreads
		{
			get { return physicsThreads; }
			set { physicsThreads = value; }
		}

		public int WaterLayer
		{
			get { return waterLayer; }
		}

		public int WaterTempLayer
		{
			get { return waterTempLayer; }
		}

		public System.Threading.ThreadPriority PhysicsThreadsPriority
		{
			get { return physicsThreadsPriority; }
		}

		public bool AllowCpuFFT
		{
			get { return allowCpuFFT; }
		}

		public bool AllowFloatingPointMipMaps
		{
			get { return allowFloatingPointMipMaps; }
		}

		public bool DebugPhysics
		{
			get { return debugPhysics; }
		}
	}
}
