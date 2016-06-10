using UnityEngine;
using System.Collections;

public class ParkingSpotRandomGenerator : MonoBehaviour {

	public float[] Positionx = new float[15] {96,118,147,168,197,218,248,-53,-82,-103,-132,-153,-182,-203,-232};
	public float[] Positionz = new float[2] {-153,-223};

	// Use this for initialization
	void Start () {
		transform.position = new Vector3 (Positionx[Random.Range(0,15)],10f,Positionz[Random.Range(0,2)]);
	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
