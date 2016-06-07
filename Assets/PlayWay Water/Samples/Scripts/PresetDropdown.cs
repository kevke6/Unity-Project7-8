using UnityEngine;
using UnityEngine.UI;

namespace PlayWay.Water.Samples
{
	public class PresetDropdown : MonoBehaviour
	{
		[SerializeField]
		private Water water;

		[SerializeField]
		private WaterProfile[] profiles;

		[SerializeField]
		private Dropdown dropdown;

		[SerializeField]
		private Slider progressSlider;

		private WaterProfile sourceProfile;
		private WaterProfile targetProfile;
		private float changeTime = float.NaN;

		void Start()
		{
			dropdown.onValueChanged.AddListener(OnValueChanged);

			if(water.Profiles == null)
			{
				enabled = false;
				return;
			}

			targetProfile = water.Profiles[0].profile;
		}

		public void SkipPresetTransition()
		{
			changeTime = -100.0f;
		}

		void Update()
		{
			if(!float.IsNaN(changeTime))
			{
				float p = Mathf.Clamp01((Time.time - changeTime) / 30.0f);

				water.SetProfiles(
					new Water.WeightedProfile(sourceProfile, 1.0f - p),
					new Water.WeightedProfile(targetProfile, p)
				);

				progressSlider.value = p;

				if(p == 1.0f)
				{
					p = float.NaN;
					changeTime = float.NaN;
					progressSlider.transform.parent.gameObject.SetActive(false);
					dropdown.interactable = true;
                }
			}
		}

		private void OnValueChanged(int index)
		{
			sourceProfile = targetProfile;
			targetProfile = profiles[index];
			changeTime = Time.time;

			progressSlider.transform.parent.gameObject.SetActive(true);
			dropdown.interactable = false;
        }
	}
}
