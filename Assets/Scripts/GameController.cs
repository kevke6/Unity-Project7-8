using UnityEngine;
using System.Collections;

public class GameController : MonoBehaviour {

	private bool gameOver;
	private bool restart;

	// Use this for initialization
	void Start () {
		gameOver = false;
		restart = false;
		Time.timeScale = 1.0f;
	}
	
	// Update is called once per frame
	void Update () {
		if (Input.GetKeyDown (KeyCode.R)) {
			Application.LoadLevel (Application.loadedLevel);
		}
		if (Input.GetKeyDown (KeyCode.Escape)) {
			Time.timeScale = 0.0f;
			LoadOnClick loadPause = new LoadOnClick ();
			loadPause.LoadScene (0);
		}
	}

	public void GameOver() {
		gameOver = true;
		if (gameOver) {
			restart = true;
			Time.timeScale = 0.0f;
			LoadOnClick loadOnClick = new LoadOnClick ();
			loadOnClick.LoadScene (3);
		}
	}
}
