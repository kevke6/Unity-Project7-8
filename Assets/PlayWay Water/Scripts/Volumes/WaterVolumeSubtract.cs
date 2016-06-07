using UnityEngine;

namespace PlayWay.Water
{
	/// <summary>
	/// Removes water from the attached colliders volumes. No water will be rendered inside them, objects inside won't be affected by physics and cameras won't use underwater image effect.
	/// </summary>
	public class WaterVolumeSubtract : WaterVolumeBase
	{
		override protected void Register(Water water)
		{
			if(water != null)
				water.Volume.AddSubtractor(this);
		}

		override protected void Unregister(Water water)
		{
			if(water != null)
				water.Volume.RemoveSubtractor(this);
		}

		protected override void CreateRenderers()
		{
			base.CreateRenderers();

			var block = new MaterialPropertyBlock();
			block.AddFloat("_Cull", 1);

			var renderers = GetComponents<MeshRenderer>();

			foreach(var renderer in renderers)
				renderer.SetPropertyBlock(block);
		}
	}
}
