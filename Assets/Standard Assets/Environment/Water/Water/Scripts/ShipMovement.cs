using UnityEngine;
using System.Collections;

public class ShipMovement : MonoBehaviour {

	private int accel =20;
	private int turn = 50;
	private float speedForward =1;
	private float speedTurn =1;
	// Use this for initialization
	void Start ()
	{
		
	}
	
	// Update is called once per frame
	void Update ()
	{
		if (Input.GetKey ("w")) 
		{
			//SetTransformZ((transform.position.z) + speedForward / accel);
			transform.TransformDirection(Vector3.forward);
		}
		if (Input.GetKey ("s")) 
		{
			SetTransformZ((transform.position.z) - speedForward / accel);
		}
		if (Input.GetKey ("a")) 
		{
			transform.Rotate (Vector3.down * Time.deltaTime);
		}
		if (Input.GetKey ("d")) 
		{
			transform.Rotate (Vector3.up * Time.deltaTime*2);
		}
	}

	void SetTransformZ(float n)
	{
		transform.position = new Vector3(transform.position.x, transform.position.y, n);
	}
}
