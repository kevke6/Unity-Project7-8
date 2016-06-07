using UnityEngine;

namespace PlayWay.WaterSamples
{
	public class AdditiveVolumesSample : MonoBehaviour
	{
		[SerializeField]
		private Transform water;

		[SerializeField]
		private float speed = 10.0f;
		
		void Update()
		{
			if(Time.frameCount >= 2)
				water.gameObject.SetActive(true);

			//Vector3 delta = new Vector3(Input.GetAxis("Horizontal") * Time.deltaTime * speed, 0.0f, Input.GetAxis("Vertical") * Time.deltaTime * speed);
            //water.position += delta;
		}
	}
}
