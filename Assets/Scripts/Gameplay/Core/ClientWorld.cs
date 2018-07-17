// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using System;
using Unity.Jobs;

namespace Bowhead.Client {
	public partial class ClientWorld : global::Client.ClientWorld {

		enum EDecalGroup {
			BloodAndExplosions,
            General
		}

		static readonly int NUM_DECAL_GROUPS = Enum.GetNames(typeof(EDecalGroup)).Length;

		DecalGroup[] _decalGroups = new DecalGroup[NUM_DECAL_GROUPS];
		Queue<IRagdollController> _ragdolls = new Queue<IRagdollController>();
		Queue<IRagdollController> _gibs = new Queue<IRagdollController>();
		PhysicalContactMatrixState _physicalContactMatrix;
		ActorSingleton<Bowhead.Actors.GameState> _gameState;

		public event Action<Bowhead.Actors.Critter> CritterActiveEvent;
        public event Action<Bowhead.Actors.Pawn, float> DamageEvent;
        public event Action<Bowhead.Actors.Pawn, StatusEffect> StatusEffectAddedEvent;

        public ClientWorld(
			IGameInstance gameInstance,
			Streaming serverStreaming,
			World_ChunkComponent chunkComponent,
			Transform sceneGroup,
			System.Reflection.Assembly[] assemblies,
			INetDriver driver
		) : base(gameInstance, serverStreaming, chunkComponent, sceneGroup, GameManager.instance.staticData.defaultActorPrefab, () => GameManager.instance.staticObjectPoolRoot, () => GameManager.instance.transientObjectPoolRoot, assemblies, driver) {
		}

		public Decal NewDecal(float lifetime, Decal.UpdateDecalDelegate update, Vector3 position, Vector3 scale, Quaternion rotation, Material material, bool visible) {
			return NewDecal<Decal>(lifetime, update, position, scale, rotation, material, visible);
		}

		public Decal NewDecal<T>(float lifetime, Decal.UpdateDecalDelegate update, Vector3 position, Vector3 scale, Quaternion rotation, Material material, bool visible) where T : Decal, new() {
			return _decalGroups[(int)EDecalGroup.General].NewDecal<T>(lifetime, update, position, scale, rotation, material, visible);
		}

		internal void InternalServer_BeginTravel(string travelLevel, HashSetList<int> travelActorNetIDs) {
			isServerTraveling = true;
			BeginTravel(travelLevel, travelActorNetIDs);
		}

		protected override void TickActors(MonoBehaviour loadingContext) {
			base.TickActors(loadingContext);
		}

		protected override void BeginTravel(string travelLevel, HashSetList<int> travelActorNetIDs) {
			base.BeginTravel(travelLevel, travelActorNetIDs);
			_gameState = null;
			_ragdolls.Clear();
			_gibs.Clear();
		}

		public override void LateUpdate() {
			base.LateUpdate();

			if (!(isTraveling || wasTraveling)) {
				for (int i = 0; i < _decalGroups.Length; ++i) {
					if (_decalGroups[i] != null) {
						_decalGroups[i].Update(deltaTime);
					}
				}
				if (_decalGroups[(int)EDecalGroup.BloodAndExplosions] != null) {
					_decalGroups[(int)EDecalGroup.BloodAndExplosions].maxDecals = 100;// GameManager.instance.bloodAndExplosionDecalLimit;
				}
			}
		}

		public int RequestRagdolls(int numRequested) {
			var ragdollLimit = GameManager.instance.ragdollLimit;
			if (ragdollLimit <= 0) {
				return numRequested;
			}

			var fadeTime = 1f;// GameManager.instance.ragdollFadeTime;
			var num = Mathf.Min(numRequested, ragdollLimit - _ragdolls.Count);
			for (; (num < numRequested) && (_ragdolls.Count > 0);) {
				var x = _ragdolls.Dequeue();
				if (!x.disposed && x.FadeOutRagdoll(GameManager.instance.randomNumber * 3f, fadeTime)) {
					++num;
				}
			}
			
			return num;
		}

		public void RagdollAdded(IRagdollController ragdoll) {
			var ragdollLimit = GameManager.instance.ragdollLimit;
			if (ragdollLimit > 0) {
				_ragdolls.Enqueue(ragdoll);
			}
		}

		public int RequestGibs(int numRequested) {
			var gibLimit = GameManager.instance.gibLimit;
			if (gibLimit <= 0) {
				return numRequested;
			}

			var fadeTime = 1f;// GameManager.instance.ragdollFadeTime;
			var num = Mathf.Min(numRequested, gibLimit - _gibs.Count);
			for (; (num < numRequested) && (_gibs.Count > 0);) {
				var x = _gibs.Dequeue();
				if (!x.disposed && x.FadeOutRagdoll(GameManager.instance.randomNumber * 3f, fadeTime)) {
					++num;
				}
			}

			return num;
		}

		public void GibAdded(IRagdollController ragdoll) {
			var gibLimit = GameManager.instance.ragdollLimit;
			if (gibLimit > 0) {
				_gibs.Enqueue(ragdoll);
			}
		}

		void AllocateDecalPools() {
			_decalGroups[(int)EDecalGroup.General] = new DecalGroup(EDecalRenderMode.Unlit, GameManager.instance.clientData.decalUnitCube, EDecalGroup.General.ToString(), 0);
			_decalGroups[(int)EDecalGroup.BloodAndExplosions] = new DecalGroup(EDecalRenderMode.Unlit, GameManager.instance.clientData.decalUnitCube, EDecalGroup.BloodAndExplosions.ToString(), 0);
		}

		void FreeDecalPools() {
			for (int i = 0; i < _decalGroups.Length; ++i) {
				if (_decalGroups[i] != null) {
					_decalGroups[i].Dispose();
					_decalGroups[i] = null;
				}
			}
		}

		public override void NotifySceneLoaded() {
			FreeDecalPools();
			AllocateDecalPools();

			if (GameManager.instance.staticData.physicalContactMatrix != null) {
				_physicalContactMatrix = new PhysicalContactMatrixState(GameManager.instance.staticData.physicalContactMatrix);
			}

			base.NotifySceneLoaded();

#if !DEDICATED_SERVER
			GameManager.instance.graphicsSettings.ApplyCameraSettings();
#endif
		}

		public void AddDecalRendererToCamera(Camera camera) {
			for (int i = 0; i < _decalGroups.Length; ++i) {
				_decalGroups[i].AddDecalRendererToCamera(camera);
			}
		}

		public void RemoveDecalRendererFromCamera(Camera camera) {
			for (int i = 0; i < _decalGroups.Length; ++i) {
				_decalGroups[i].RemoveDecalRendererFromCamera(camera);
			}
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			FreeDecalPools();
		}

		protected override void OnLevelStart() {
			base.OnLevelStart();
			_gameState = new ActorSingleton<Bowhead.Actors.GameState>(this);
			Debug.Log("Client -- level start.");
			GameManager.instance.LogMemStat();
		}

		protected override void OnDisconnectedFromServer(EDisconnectReason reason) {
			if (GameManager.instance.serverWorld == null) {
				GameManager.instance.SetPendingLevel("MainMenu", null);
			}
		}

		protected override void SendClientConnect() {
#if !DEDICATED_SERVER
			serverConnection.SendReliable(
				NetMsgs.ClientConnect.New(
					GameManager.instance.onlineLocalPlayer.id.uuid,
					GameManager.instance.challenge,
					BuildInfo.ID
				)
			);
#endif
		}

		public void UpdateBloodAndExplosionDecalLimit() {
			_decalGroups[(int)EDecalGroup.BloodAndExplosions].maxDecals = 100;// GameManager.instance.bloodAndExplosionDecalLimit;
		}

		public Bowhead.Actors.GameState gameState {
			get {
				return _gameState != null ? _gameState : null;
			}
		}

		//public void RenderBloodSplat(Vector3 worldPos, Vector2 size, float orientation) {
		//	RaycastHit hitInfo;
		//	if (Physics.Raycast(worldPos + new Vector3(0, 1024, 0), Vector3.down, out hitInfo, Mathf.Infinity, Layers.TerrainMask|Layers.BlockMask)) {
		//		if ((hitInfo.collider.gameObject.layer == Layers.Block) || hitInfo.collider.gameObject.CompareTag("Decal")) {
		//			var m = GameManager.instance.clientData.bloodDecalMaterials[GameManager.instance.RandomRange(0, GameManager.instance.clientData.bloodDecalMaterials.Length)];
		//			if (_decalGroups[(int)EDecalGroup.BloodAndExplosions] != null) {
		//				_decalGroups[(int)EDecalGroup.BloodAndExplosions].NewDecal(0f, null, worldPos, new Vector3(size.x, 1, size.y), Quaternion.AngleAxis(orientation, Vector3.up), m, true);
		//			}
		//		} /*else if (_terrainBlood != null) {
		//			_terrainBlood.RenderBlood(worldPos, size, orientation);
		//		}*/
		//	}
		//}

		//public void RenderExplosionSplat(Vector3 worldPos, Vector2 size, float orientation, bool drawOnWater) {
		//	if (!drawOnWater) {
		//		RaycastHit hitInfo;
		//		if (Physics.Raycast(new Ray(worldPos + new Vector3(0, 0.5f, 0), Vector3.down), out hitInfo, Mathf.Infinity, Layers.TerrainMask|Layers.BlockMask|Layers.WaterMask, QueryTriggerInteraction.Collide)) {
		//			if (hitInfo.transform.gameObject.layer == Layers.Water) {
		//				return;
		//			}
		//		}
  //          }

		//	var m = GameManager.instance.clientData.explosionDecalMaterials[GameManager.instance.RandomRange(0, GameManager.instance.clientData.explosionDecalMaterials.Length)];
		//	if (_decalGroups[(int)EDecalGroup.BloodAndExplosions] != null) {
		//		_decalGroups[(int)EDecalGroup.BloodAndExplosions].NewDecal(0f, null, worldPos, new Vector3(size.x, 1, size.y), Quaternion.AngleAxis(orientation, Vector3.up), m, true);
		//	}
		//}

		//public void RenderBloodSplats(Vector3 center, Vector2 radius, Vector2 size, IntMath.Vector2i count) {
		//	if ((size.y > 0f) && (count.y > 0)) {
		//		var c = Mathf.FloorToInt(Mathf.Lerp(count.x, count.y, GameManager.instance.randomNumber));
		//		if (c > 0) {
		//			var r = Mathf.Lerp(radius.x, radius.y, GameManager.instance.randomNumber);

		//			for (int i = 0; i < c; ++i) {
		//				var s = Mathf.Lerp(size.x, size.y, GameManager.instance.randomNumber);
		//				if (s > 0f) {
		//					var pos = center + new Vector3(r*(GameManager.instance.randomNumber-0.5f)*2, 0, r*(GameManager.instance.randomNumber-0.5f)*2);
		//					pos = Utils.PutPositionOnGround(pos);

		//					RenderBloodSplat(pos, new Vector2(s, s), GameManager.instance.randomNumber*360);
		//				}
		//			}
		//		}
		//	}
		//}

		public void SpawnContactFx(PhysicalMaterialClass a, PhysicalMaterialClass b, Vector3 position, Vector3 normal) {
			if (_physicalContactMatrix != null) {
				_physicalContactMatrix.SpawnContactFx(time, a, b, position, normal);
			}
		}

		protected override JobHandle CreateGenVoxelsJob(WorldChunkPos_t pos, PinnedChunkData_t chunk) {
			return gameState.worldStreaming.ScheduleChunkGenerationJob(pos, chunk, true);
		}

		protected override Streaming.IMMappedChunkData MMapChunkData(Streaming.IChunk chunk) {
			return gameState.worldStreaming.MMapChunkData(chunk);
		}

		protected override void WriteChunkData(Streaming.IChunkIO chunk) {
			gameState.worldStreaming.WriteChunkData(chunk);
		}

        public void OnCritterActive(Bowhead.Actors.Critter c)
        {
            CritterActiveEvent?.Invoke(c);
        }

        public void OnDamage(Bowhead.Actors.Pawn p, float d)
        {
            DamageEvent?.Invoke(p, d);
        }

        public void OnStatusEffectAdded(Bowhead.Actors.Pawn target, StatusEffect effect)
        {
            StatusEffectAddedEvent?.Invoke(target, effect);
        }

    }
}
