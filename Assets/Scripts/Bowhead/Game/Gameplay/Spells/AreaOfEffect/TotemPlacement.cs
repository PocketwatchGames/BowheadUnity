// Copyright (c) 2018 Pocketwatch Games LLC.


using UnityEngine;
using System;

namespace Bowhead.Actors.Spells {
	[Serializable]
	public sealed class TotemPlacement_WRef : WeakAssetRef<TotemPlacement> { }

	public class TotemPlacement : MonoBehaviour {
		static readonly Color INVALID_COLOR = Color.red;

		Transform _damageable;
		Transform _visual;
		Transform _invalidVisual;
		Transform _selectionHiTest;
		
		public void ServerInit() {
			_damageable = transform.Find("Damageable");
		}

		public void ClientInit(Team team, PlayerState player) {
			_visual = transform.Find("Visual");
			if (_visual != null) {
				_visual = _visual.Find("Rotated");
			}

			_invalidVisual = transform.Find("InvalidVisual");

			if (_invalidVisual != null) {
				_invalidVisual.gameObject.SetActive(false);
			}

			_selectionHiTest = transform.Find("SelectionHitTest");

			var teamColors = this.GetComponentsInAllChildren<TeamColors>();

			if (teamColors != null) {
				for (int i = 0; i < teamColors.Length; ++i) {
					teamColors[i].SetUniqueColors((player != null) ? player.primaryColor : team.teamColor, (player != null) ? player.secondaryColor : team.teamColor);
				}
			}
		}

		public void SetPosition(Vector3 pos, Vector3 normal, float rotation, bool valid) {
			var q = Utils.LookBasis(normal);
			var r = Quaternion.AngleAxis(rotation, Vector3.up);
			var rq = r*q;

			transform.position = pos;
			transform.rotation = r;

			if (_damageable != null) {
				_damageable.rotation = rq;
			}

			if (_visual != null) {
				_visual.rotation = rq;
			}

			if (_selectionHiTest != null) {
				_selectionHiTest.rotation = rq;
			}

			if (_invalidVisual != null) {
				_invalidVisual.gameObject.SetActive(valid);
			}
		}

		public void Damaged() {}
	}
}