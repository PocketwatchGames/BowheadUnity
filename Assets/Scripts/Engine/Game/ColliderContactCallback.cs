using UnityEngine;
using System.Collections.Generic;

public interface ColliderContactReceiver {
	void OnContactBegin(Collision collision);
	void OnContactEnd(Collision collision);
}

public interface ColliderTriggerReceiver {
	void OnTriggerEnter(Collider other);
	void OnTriggerExit(Collider other);
}

[RequireComponent(typeof(ActorReference))]
public class ColliderContactCallback : MonoBehaviour {

	ActorReference _actorRef;

	void Awake() {
		_actorRef = GetComponent<ActorReference>();
	}

	void OnCollisionEnter(Collision collision) {
		if (_actorRef != null) {
			var svActor = _actorRef.serverActor as ColliderContactReceiver;
			if (svActor != null) {
				svActor.OnContactBegin(collision);
			}

			var clActor = _actorRef.clientActor as ColliderContactReceiver;
			if (clActor != null) {
				clActor.OnContactBegin(collision);
			}
		}
	}

	void OnCollisionExit(Collision collision) {
		if (_actorRef != null) {
			var svActor = _actorRef.serverActor as ColliderContactReceiver;
			if (svActor != null) {
				svActor.OnContactEnd(collision);
			}

			var clActor = _actorRef.clientActor as ColliderContactReceiver;
			if (clActor != null) {
				clActor.OnContactEnd(collision);
			}
		}
	}

	void OnTriggerEnter(Collider collider) {
		if (_actorRef != null) {
			var svActor = _actorRef.serverActor as ColliderTriggerReceiver;
			if (svActor != null) {
				svActor.OnTriggerEnter(collider);
			}

			var clActor = _actorRef.clientActor as ColliderTriggerReceiver;
			if (clActor != null) {
				clActor.OnTriggerEnter(collider);
			}
		}
	}

	void OnTriggerExit(Collider collider) {
		if (_actorRef != null) {
			var svActor = _actorRef.serverActor as ColliderTriggerReceiver;
			if (svActor != null) {
				svActor.OnTriggerExit(collider);
			}

			var clActor = _actorRef.clientActor as ColliderTriggerReceiver;
			if (clActor != null) {
				clActor.OnTriggerExit(collider);
			}
		}
	}
}
