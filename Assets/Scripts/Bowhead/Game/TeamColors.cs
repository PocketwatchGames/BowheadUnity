// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead {
	public class TeamColors : MonoBehaviour {
		int TEAM_PRIMARY_COLOR;
		int TEAM_SECONDARY_COLOR;
		List<Object> _destroyList = new List<Object>();
		
		void Awake() {
			TEAM_PRIMARY_COLOR = Shader.PropertyToID("_TeamPrimaryColor");
			TEAM_SECONDARY_COLOR = Shader.PropertyToID("_TeamSecondaryColor");
		}

		void OnDestroy() {
			for (int i = 0; i < _destroyList.Count; ++i) {
				var obj = _destroyList[i];
				if (obj != null) {
					Destroy(_destroyList[i]);
				}
			}
		}

		public void SetSharedColors(Actors.PlayerState player) {
			var renderers = GetComponents<Renderer>();
			for (int i = 0; i < renderers.Length; ++i) {
				var r = renderers[i];
				var m = r.sharedMaterials;
				for (int k = 0; k < m.Length; ++k) {
					m[k] = GameManager.instance.InstanceTeamColorMaterial(m[k], player.netID, player.primaryColor, player.secondaryColor);
				}
				r.sharedMaterials = m;
			}
		}

		public void SetSharedColors(Actors.Team team) {
			var renderers = GetComponents<Renderer>();
			for (int i = 0; i < renderers.Length; ++i) {
				var r = renderers[i];
				var m = r.sharedMaterials;
				for (int k = 0; k < m.Length; ++k) {
					m[k] = GameManager.instance.InstanceTeamColorMaterial(m[k], team.netID, team.teamColor, team.teamColor);
				}
				r.sharedMaterials = m;
			}
		}

		public void SetUniqueColors(Color primary, Color secondary) {
			var renderers = GetComponents<Renderer>();
			for (int i = 0; i < renderers.Length; ++i) {
				var r = renderers[i];
				var m = r.materials;
				for (int k = 0; k < m.Length; ++k) {
					var instance = m[k];
					if (!_destroyList.Contains(instance)) {
						_destroyList.Add(instance);
					}
					instance.SetColor(TEAM_PRIMARY_COLOR, primary);
					instance.SetColor(TEAM_SECONDARY_COLOR, secondary);
				}
			}
		}
	}
}