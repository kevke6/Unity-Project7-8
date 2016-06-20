using UnityEngine;
using System.Collections;

public class GameOverByContact : MonoBehaviour {

	private GameController gameController;
	private string objectHit;
	public float spotX;
	public float spotZ;
	public Vector2 answer;
	public float thrust;
		
	void Start () {
		GameObject gameControllerObject = GameObject.FindWithTag ("GameController");
		if (gameControllerObject != null) {
			gameController = gameControllerObject.GetComponent<GameController> ();
		}
		if (gameController == null) {
			Debug.Log ("Cannot find 'GameController' script");
		}
	}

	void FixedUpdate()
	{
		GameWonChecker ();
		answer = new Vector2(transform.position.x-spotX, transform.position.z - spotZ);
		spotX = GameObject.Find("ParkingSpot").transform.position.x ;
		spotZ = GameObject.Find("ParkingSpot").transform.position.z;
		GameObject ship = GameObject.Find ("SeaAngler");
		ShipMovement shipMovement = ship.GetComponent<ShipMovement> ();
		thrust = shipMovement.thrust;
	}

	void OnCollisionEnter (Collision col) {
		if (col.gameObject.name == "Harbor" || col.gameObject.name == "SeaAnglerAIIn" || col.gameObject.name == "SeaAnglerAIOut") {
			Debug.Log("You hit the harbor");
			gameController.GameOver ();
		}
	}

	void GameWonChecker(){
		if ((transform.position.x - 10.0f) <= spotX && (transform.position.x + 10.0f) >= spotX && (transform.position.z - 20.0f) <= spotZ && (transform.position.z + 20.0f) >= spotZ && thrust == 0.00000f) {
			gameController.GameOver ();
		}
	}
}
