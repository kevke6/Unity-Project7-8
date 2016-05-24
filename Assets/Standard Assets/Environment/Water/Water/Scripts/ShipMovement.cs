using UnityEngine;
using System.Collections;

public class ShipMovement : MonoBehaviour {

	private float acceleration =0.03f;
	private float speedForward = 0.0f;
	private float maxSpeed = 15.0f;
	private float maxBackSpeed = -10.0f;

	// Use this for initialization
	void Start ()
	{
		
	}
	
	// Update is called once per frame
	void Update ()
	{
		transform.position += transform.forward * Time.deltaTime * speedForward;
		if (Input.GetKey ("w") && speedForward < maxSpeed) 
		{
			speedForward += acceleration;
		}
		if (Input.GetKey ("s") && speedForward > maxBackSpeed) 
		{
			speedForward -= acceleration;
		}
		if (Input.GetKey ("a")) 
		{
			if (speedForward < 1.0f || speedForward > -1.0f) {
				transform.Rotate (Vector3.down * Time.deltaTime * 2);
				slowDown (2);
			} else {
				transform.Rotate (Vector3.down * Time.deltaTime);
				slowDown (2);
			}
		}
		if (Input.GetKey ("d")) 
		{
			if (speedForward < 1.0f || speedForward > -1.0f) {
				transform.Rotate (Vector3.up * Time.deltaTime * 2);
				slowDown (2);
			} else {
				transform.Rotate (Vector3.up * Time.deltaTime);
				slowDown (2);
			}
		}
		slowDown (1);
	}

	public void slowDown(int n)
	{
		if(speedForward > 0 && n == 1){ speedForward -= 0.01f;}
		if(speedForward < 0 && n == 1){ speedForward += 0.01f;}
		if(speedForward > 0 && n == 2){ speedForward -= 0.02f;}
		if(speedForward < 0 && n == 2){ speedForward += 0.02f;}
	}
}
