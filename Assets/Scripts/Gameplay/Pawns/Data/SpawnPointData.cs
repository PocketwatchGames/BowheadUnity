// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

	public enum ESpawnPointType {
		Monster,
		Chest,
		MapReveal
	};

	public interface ISpawnPointSupport {
		Actor Spawn(World world, Vector3 pos, float yaw, Team team);
	};

    [CreateAssetMenu(menuName = "EntityData/SpawnPoint")]
	public sealed class SpawnPointData : StaticVersionedAsset {
		[SerializeField]
		ESpawnPointType _type;
		[SerializeField]
		EntityData _entityData;

		static List<SpawnPointData> _list = new List<SpawnPointData>();

		public static SpawnPointData[] GetAllSpawnTypes<T>(ESpawnPointType type) {
			var objs = GameManager.instance.staticData.indexedObjects;

			foreach (var obj in objs) {
				var spawnPoint = obj as SpawnPointData;
				if (spawnPoint != null) {
					if ((spawnPoint._type == type) && (spawnPoint._entityData is T)) {
						_list.Add(spawnPoint);
					}
				}
			}

			var arr = _list.ToArray();
			_list.Clear();
			return arr;
		}

		public T Spawn<T>(Server.GameMode gameMode, Vector3 pos, float yaw) where T: Actor {
			Team team;
			switch (_type) {
				default:
					team = null;
				break;
				case ESpawnPointType.Monster:
					team = gameMode.monsterTeam;
				break;
			}

			var spawnFn = (ISpawnPointSupport)_entityData;
			if (spawnFn != null) {
				return (T)spawnFn.Spawn(gameMode.world, pos, yaw, team);
			}

			return null;
		}
	}
}
