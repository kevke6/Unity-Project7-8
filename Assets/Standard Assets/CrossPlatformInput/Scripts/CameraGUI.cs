using UnityEngine;
using System.Collections;

public class CameraGUI : MonoBehaviour {

	public float rotY;
	public float rotX;
	private float sensitivity = 2.0f;
	// Use this for initialization
	void Start () {
		rotY = transform.localEulerAngles.y;
		rotX = transform.localEulerAngles.x;
	}
	
	// Update is called once per frame
	void Update () {
		
		rotX += Input.GetAxis ("Mouse X") * sensitivity;
		rotX = Mathf.Clamp (rotX,-180,180);
		rotY += Input.GetAxis ("Mouse Y") * sensitivity;
		rotY = Mathf.Clamp (rotY,-70,70);

		transform.localEulerAngles = new Vector3 (-rotY, rotX, transform.localEulerAngles.z);
	}
}
