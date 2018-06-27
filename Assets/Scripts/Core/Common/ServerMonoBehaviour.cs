// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections;

namespace Bowhead {
	public class ServerMonoBehaviour : MonoBehaviourEx {

		enum EWhenRunOnClient {
			DestroyComponent,
			DestroyGameObject
		}

		[SerializeField]
		EWhenRunOnClient _whenRunOnClient;

		void Start() {
			if (GameManager.instance.serverWorld == null) {
				if (_whenRunOnClient == EWhenRunOnClient.DestroyComponent) {
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