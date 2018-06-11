// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;

namespace Bowhead.Actors {

	[Serializable]
	public sealed class ItemPickupClientPrefab_WRef : WeakAssetRef<ItemPickupClientPrefab> { }

	[RequireComponent(typeof(ActorReference))]
	[RequireComponent(typeof(CapsuleCollider))]
	[RequireComponent(typeof(Rigidbody))]
	[RequireComponent(typeof(Client.UI.ItemTooltip))]
	public class ItemPickupClientPrefab : MonoBehaviour {
		const float SHOW_NAMEPLATE_TIME = 8f;
		
		//[SerializeField]
		//HighlightingSystem.Highlighter _highlighter;
		[SerializeField]
		GameObject _spawnFXPrefab;
		[SerializeField]
		GameObject _pickupFXPrefab;
		[SerializeField]
		GameObject _contactFXPrefab;
		[SerializeField]
		Client.UI.ItemTooltip _tooltip;
		//[SerializeField]
		//TMPro.TextMeshProUGUI _nameplate;

		bool _highlighted;
		float _showNameplateTime;

		public void ClientInit(PlayerState owner, MetaGame.DropItemClass itemClass, int ilvl) {
			_tooltip.Init(owner, itemClass, ilvl);
			//if (_nameplate != null) {
			//	_nameplate.text = itemClass.localizedName;
			//}
		}

		public void SpawnFX() {
			if (_spawnFXPrefab != null) {
				Instantiate(_spawnFXPrefab, transform.position, transform.rotation, null);
			}
		}

		public void PickupFX() {
			if (_pickupFXPrefab != null) {
				Instantiate(_pickupFXPrefab, transform.position, transform.rotation, null);
			}
		}

		public void ContactFX() {
			//if (_nameplate != null) {
			//	_nameplate.gameObject.SetActive(true);
			//	_showNameplateTime = SHOW_NAMEPLATE_TIME;
			//}
			if (_contactFXPrefab != null) {
				Instantiate(_contactFXPrefab, transform, false);
			}
		}

		void Update() {
			//if (_nameplate != null) {
			//	if (_showNameplateTime > 0) {
			//		_showNameplateTime -= Time.deltaTime;
			//		if (_showNameplateTime <= 0f) {
			//			if (!_highlighted) {
			//				_nameplate.gameObject.SetActive(false);
			//			}
			//		}
			//	}
			//}
		}
		
		
		public bool highlighted {
			get {
				return _highlighted;
			}
			set {
				if (_highlighted != value) {
					_highlighted = value;

					//if (_highlighter != null) {
					//	if (_highlighted) {
					//		_highlighter.ConstantOn();
					//	} else {
					//		_highlighter.ConstantOffImmediate();
					//	}
					//}

					if (_highlighted) {
						_tooltip.Show();
						//if (_nameplate != null) {
						//	_nameplate.gameObject.SetActive(true);
						//}
					} else {
						_tooltip.Hide();
						//if (_nameplate != null) {
						//	if (_showNameplateTime <= 0f) {
						//		_nameplate.gameObject.SetActive(false);
						//	}
						//}
					}
				}
			}		
		}

#if UNITY_EDITOR
		void Reset() {
			gameObject.layer = Layers.Pickups;
			_tooltip = GetComponent<Client.UI.ItemTooltip>();
			GetComponent<Collider>().isTrigger = true;
			GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.Interpolate;
		}
#endif
	}
}