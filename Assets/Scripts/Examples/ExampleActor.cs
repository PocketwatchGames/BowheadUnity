using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Bowhead;

public class ExampleActor : Actor {

	// NOTE: The actor system supports polymorphism at the networking level,
	// in some places, like in the PlayerController Actor, there are two
	// types, a ServerPlayerController, and a ClientPlayerController. Sometimes
	// this is useful, other times it's not, it's up to you.

	public override Type serverType => typeof(ExampleActor);
	public override Type clientType => typeof(ExampleActor);

	[Replicated(Notify ="OnRep_position")]
	Vector3 _position;
	[Replicated(Notify = "OnRep_rotation")]
	Quaternion _rotation;

	public ExampleActor() {
		SetReplicates(true);
	}

	public void ServerSpawn() {
		AttachExternalGameObject(GameObject.Instantiate(Resources.Load<GameObject>("ExamplePlayer")));
		_position = go.transform.position;
		_rotation = go.transform.rotation;
	}

	public override void PostNetConstruct() {
		base.PostNetConstruct();
				
		if (GameManager.instance.serverWorld == null) {
			// pure client
			AttachExternalGameObject(GameObject.Instantiate(Resources.Load<GameObject>("ExamplePlayer")));
			go.GetComponent<Rigidbody>().isKinematic = true;
			go.GetComponent<Collider>().enabled = false;
		} else {
			// Since this is a dynamically spawned actor (i.e. there isn't an ActorSpawnTag in the scene which spawns this)
			// it means that in a mixed client/server situation (when the game is running a local server and client)
			// we need to find the gameobject that was attached to the local server and use it

			var svActor = (Actor)GameManager.instance.serverWorld.GetObjectByNetID(netID);
			if ((svActor != null) && !svActor.pendingKill) {
				AttachExternalGameObject(svActor.go);
			}
		}
	}

	public override void Tick() {
		base.Tick();

		if (hasAuthority) {
			_position = go.transform.position;
			_rotation = go.transform.rotation;
		} else if (GameManager.instance.serverWorld == null) {
			// pure client
			go.transform.position = _position;
			go.transform.rotation = _rotation;
		}
	}

	void OnRep_position() {
		Debug.Log("OnRep_position");
	}

	void OnRep_rotation() {
		Debug.Log("OnRep_rotation");
	}
}
