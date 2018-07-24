// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections;

namespace Bowhead {
	public class ClientMonoBehaviour : MonoBehaviourEx {

		protected enum EWhenRunOnServer {
			DestroyComponent,
			DestroyGameObject
		}

		[SerializeField]
		protected EWhenRunOnServer _whenRunOnServer;

		void Start() {
			if (GameManager.instance.clientWorld == null) {
				if (_whenRunOnServer == EWhenRunOnServer.DestroyComponent) {
					Destroy(this);
				} else {
					Utils.DestroyGameObject(gameObject);
				}
			} else {
				OnStart();
			}
		}

		protected virtual void OnStart() { }
	}
}