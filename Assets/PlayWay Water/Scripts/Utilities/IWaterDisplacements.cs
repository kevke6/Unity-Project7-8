
using UnityEngine;

public interface IWaterDisplacements
{
	Vector3 GetDisplacementAt(float x, float z, float spectrumStart, float spectrumEnd, float time, ref bool completed);
    Vector2 GetHorizontalDisplacementAt(float x, float z, float spectrumStart, float spectrumEnd, float time, ref bool completed);
	float GetHeightAt(float x, float z, float spectrumStart, float spectrumEnd, float time, ref bool completed);
	Vector4 GetForceAndHeightAt(float x, float z, float spectrumStart, float spectrumEnd, float time, ref bool completed);

	float MaxVerticalDisplacement { get; }
	float MaxHorizontalDisplacement { get; }
}
