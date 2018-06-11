// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

public class ActorReference : MonoBehaviour {

	public bool isChild;

	Actor _clientActor;
	Actor _serverActor;
	ActorReference _parent;

	protected virtual void Awake () {
		if (isChild) {
			FindUpwards();
		}
	}

	protected virtual void Start() {}

	public void FindUpwards() {
		for (var p = transform.parent; p != null; p = p.parent) {
			_parent = p.GetComponent<ActorReference>();
			if (_parent != null) {
				break;
			}
		}
	}
		
	public virtual void SetServerActor(Actor actor) {
		if (_parent != null) {
			_parent.SetServerActor(actor);
		} else {
			_serverActor = actor;
		}
	}

	public virtual void SetClientActor(Actor actor) {
		if (_parent != null) {
			_parent.SetClientActor(actor);
		} else {
			_clientActor = actor;
		}
	}

	public Actor serverActor {
		get {
			return (_parent != null) ? _parent.serverActor : _serverActor;
		}
	}

	public Actor clientActor {
		get {
			return (_parent != null) ? _parent.clientActor : _clientActor;
		}
	}
}
