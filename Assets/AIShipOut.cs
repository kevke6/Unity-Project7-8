using UnityEngine;
using System.Collections;

public class AIShipOut : MonoBehaviour {

	public Rigidbody rb;
	private float thrust;
	private float rotate;
	private int positionCounter = 0;
	private Vector3 eulerAngleVelocity;
	public Vector3[] moveToPositions = new Vector3[4];
	public Vector3[] rotateToAngle = new Vector3[4];
	public Vector3 positions;
	// Use this for initialization
	void Start () {
		moveToPositions [0] = new Vector3 (-200f, 1.7f, -204f);
		moveToPositions [1] = new Vector3 (0f, 1.7f, -204f);
		moveToPositions [2] = new Vector3 (0f, 1.7f, -50f);
		moveToPositions [3] = new Vector3 (240f, 1.7f, -50f);
		rotateToAngle[0] = new Vector3(0,180,0);
		rotateToAngle[1] = new Vector3(0,0,0);
		rotateToAngle[2] = new Vector3(0,90,0);
		rotateToAngle[3] = new Vector3(0,90,0);
	}

	// Update is called once per frame
	void Update () {
		positions = new Vector3 (transform.position.x, transform.position.y, transform.position.z);
		if (positionCounter <= 3) {
			try {
				transform.position = Vector3.MoveTowards (transform.position, moveToPositions[positionCounter], 0.6f);
			} catch {
			}
			if (positions.x == transform.position.x && positions.z == transform.position.z) {
				transform.eulerAngles = Vector3.Lerp (transform.rotation.eulerAngles, rotateToAngle[positionCounter], 1.0f);
				positionCounter++;
			}
		}
		if (positionCounter == 4) {
			Destroy (gameObject);
		}
	}
}