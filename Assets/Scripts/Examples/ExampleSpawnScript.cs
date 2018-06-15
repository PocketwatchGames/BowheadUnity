using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// NOTE: you would not normally spawn an actor from a monobehavior, but this is for demonstration
using Bowhead;

public class ExampleSpawnScript : MonoBehaviour {

	public bool clickMeToSpawnACube;

	void Update() {
		if (clickMeToSpawnACube) {
			clickMeToSpawnACube = false;

			if (GameManager.instance.serverWorld == null) {
				Debug.LogWarning("A local server is not running so I am spwaning game object from a client, this actor can't replicate!");
				var actor = GameManager.instance.clientWorld.Spawn<ExampleActor>(null, default(SpawnParameters));
				actor.ServerSpawn();
			} else {
				var actor = GameManager.instance.serverWorld.Spawn<ExampleActor>(null, default(SpawnParameters));
				actor.ServerSpawn();
			}
		}
	}

}
