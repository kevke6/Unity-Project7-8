using UnityEngine;
using System.Collections;

public class GameOverByContact : MonoBehaviour {

	private GameController gameController;
	private string objectHit;

	void Start () {
		GameObject gameControllerObject = GameObject.FindWithTag ("GameController");
		if (gameControllerObject != null) {
			gameController = gameControllerObject.GetComponent<GameController> ();
		}
		if (gameController == null) {
			Debug.Log ("Cannot find 'GameController' script");
		}
	}


	void OnTriggerEnter (Collider col) {
		if (col.tag == "Harbor") {
			Debug.Log("You hit the harbor");
			gameController.GameOver ();
		}
	}
}
