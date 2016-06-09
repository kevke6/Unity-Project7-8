using UnityEngine;
using System.Collections;

public class ShipMovement : MonoBehaviour
{

	public float thrust = 0.0f;
	public float rotate = 0.0f;
	private int count;
	public Vector3 eulerAngleVelocity;
	public Rigidbody rb;
	void Start()
	{
		rb = GetComponent<Rigidbody>();
	}
	void FixedUpdate()
	{
		ShipMovementForce();
		ShipRotation();
	}

	void ShipRotation()
	{
		if (Input.GetKey("a"))
		{
			if (thrust < 0)
			{
				rotate = 7.0f;
			}
			else
			{
				rotate = -7.0f;
			}
			eulerAngleVelocity = new Vector3(0, rotate, 0);
			Quaternion deltaRotation = Quaternion.Euler(eulerAngleVelocity * Time.deltaTime);
			rb.MoveRotation(rb.rotation * deltaRotation);
		}
		if (Input.GetKey("d"))
		{
			if (thrust < 0)
			{
				rotate = -7.0f;
			}
			else
			{
				rotate = 7.0f;
			}
			eulerAngleVelocity = new Vector3(0, rotate, 0);
			Quaternion deltaRotation = Quaternion.Euler(eulerAngleVelocity * Time.deltaTime);
			rb.MoveRotation(rb.rotation * deltaRotation);
		}
	}

	void ShipMovementForce()
	{
		if (Input.GetKey("w"))
		{
			if (thrust <= 0.2f)
			{
				thrust += 0.005f;
			}
		}
		if (Input.GetKey("s"))
		{
			if (thrust >= -0.07f)
			{
				thrust -= 0.001f;
			}
		}
		else
		{
			if (thrust > 0.0f && Input.GetKey("w") != true)
			{
				thrust -= 0.0001f;
				count++;
			}
			if (thrust < 0.0f && Input.GetKey("s") != true)
			{
				thrust += 0.0001f;
				count++;
			}
			if (count > 750)
			{
				thrust = 0.0f;
				count = 0;
			}
		}
		rb.MovePosition(transform.position + transform.forward * thrust);
	}
}