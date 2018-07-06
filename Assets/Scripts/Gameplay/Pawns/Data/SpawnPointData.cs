// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

	public enum ESpawnPointTeam {
		Monster,
		NPC
	};

    [CreateAssetMenu(menuName = "EntityData/SpawnPoint")]
	public sealed class SpawnPointData : StaticVersionedAsset {
		[SerializeField]
		ESpawnPointTeam _team;
		[SerializeField]
		EntityData _entityData;

		static List<SpawnPointData> _list = new List<SpawnPointData>();

		public static SpawnPointData[] GetAllSpawnTypes(ESpawnPointTeam teamType) {
			var objs = GameManager.instance.staticData.indexedObjects;

			foreach (var obj in objs) {
				var spawnPoint = obj as SpawnPointData;
				if (spawnPoint != null) {
					if (spawnPoint._team == teamType) {
						_list.Add(spawnPoint);
					}
				}
			}

			var arr = _list.ToArray();
			_list.Clear();
			return arr;
		}
	}
}
