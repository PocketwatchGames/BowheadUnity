// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

	public enum ESpawnPointType {
		Monster,
		Merchant,
		Horse,
		Chest,
		MapReveal,
	};

	public interface ISpawnPointSupport {
		Actor Spawn(World world, Vector3 pos, float yaw, Team team);
	};

    [CreateAssetMenu(menuName = "EntityData/SpawnPoint")]
	public sealed class SpawnPointData : StaticVersionedAsset {

		[System.Serializable]
		public class SpawnCount {
			public EntityData _entityData;
			public int _spawnCountMin=1;
			public int _spawnCountMax =1;
		}
		[SerializeField]
		ESpawnPointType _type;
		[SerializeField]
		List<SpawnCount> _entities = new List<SpawnCount>();
		public GameObject_WRef mapMarker;
		public Client.UI.EMapMarkerStyle mapMarkerStyle;

		static List<SpawnPointData> _list = new List<SpawnPointData>();

		public static SpawnPointData[] GetAllSpawnTypes(ESpawnPointType type) {
			var objs = GameManager.instance.staticData.indexedObjects;

			foreach (var obj in objs) {
				var spawnPoint = obj as SpawnPointData;
				if (spawnPoint != null) {
					if ((spawnPoint._type == type)) {
						_list.Add(spawnPoint);
					}
				}
			}

			var arr = _list.ToArray();
			_list.Clear();
			return arr;
		}

		public List<T> Spawn<T>(Server.GameMode gameMode, Vector3 pos, float yaw) where T: Actor {
			Team team;
			var spawns = new List<T>();
			switch (_type) {
				default:
					team = null;
				break;
				case ESpawnPointType.Horse:
				case ESpawnPointType.Merchant:
					team = gameMode.players[0].team;
					break;
				case ESpawnPointType.Monster:
					team = gameMode.monsterTeam;
					break;
			}

			int entCount = 0;
			foreach (var s in _entities) {
				int count = s._spawnCountMin + (int)((((Server.BowheadGame)gameMode).GetWhiteNoise((int)pos.x,(int)pos.y,(int)pos.z)) * (s._spawnCountMax - s._spawnCountMin));
				for (int i = 0; i < count; i++) {
					var spawnFn = (ISpawnPointSupport)s._entityData;
					if (spawnFn != null) {
						spawns.Add((T)spawnFn.Spawn(gameMode.world, pos + new Vector3((entCount ++)* 0.1f,0,0), yaw, team));
					}
				}
			}

			if (mapMarker != null) {
				var marker = GameManager.instance.clientWorld.gameState.hud.CreateMapMarker(mapMarker.Load(), mapMarkerStyle);
				marker.worldPosition = new Vector2(pos.x,pos.z);
			}

			return spawns;
		}
	}
}
