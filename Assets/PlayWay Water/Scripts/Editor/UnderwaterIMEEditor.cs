using UnityEngine;
using UnityEditor;

namespace PlayWay.Water
{
	[CustomEditor(typeof(UnderwaterIME))]
	public class UnderwaterIMEEditor : WaterEditorBase
	{
		public override void OnInspectorGUI()
		{
			SubPropertyField("blur", "iterations", "Blur Quality");
			PropertyField("underwaterAudio");

			serializedObject.ApplyModifiedProperties();
		}
	}
}
