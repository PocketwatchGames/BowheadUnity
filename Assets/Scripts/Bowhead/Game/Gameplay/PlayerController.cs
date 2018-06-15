// Copyright (c) 2018 Pocketwatch Games LLC.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Assertions;

namespace Bowhead.Actors {
#if UNITY_EDITOR
	[HideInInspector] // Hide from FlowCanvas inspector
#endif
	public abstract class PlayerController : Actor, ActorWithTeam, MatchStateEventReceiver {
		public const float DOUBLE_CLICK_SELECT_CHAIN_RADIUS = 10;
		public const float ATTACK_CHAIN_RADIUS = 3;
		public const int MAX_SOULSTONES = 6;
		
		[Replicated(Condition = EReplicateCondition.InitialOnly, Notify = "OnRep_PlayerState")]
		PlayerState _playerState;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		Vector3 _startingPosition;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		float _startingRotation;
		
		public PlayerController() {
			SetReplicates(true);
			SetOwnerOnly(true);
		}

		protected virtual void OnRep_PlayerState() { }

		public virtual void GetTravelActorNetIds(HashSetList<int> travelActorNetIDs) { }
				
		public PlayerState playerState {
			get {
				return _playerState;
			}
			protected set {
				_playerState = value;
			}
		}

		public Team team {
			get {
				return (_playerState != null) ? _playerState.team : null;
			}
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
		}

		[RPC(ERPCDomain.Owner)]
		protected void Owner_SetPlayerState(PlayerState playerState) {
			_playerState = playerState;
		}

		[RPC(ERPCDomain.Owner)]
		protected virtual void Owner_SetStartingPositionAndRotation(Vector3 pos, float rot) {
			_startingPosition = pos;
			_startingRotation = rot;
		}

		[RPC(ERPCDomain.Server)]
		protected virtual void Server_ClientHasLoaded() { }

		[RPC(ERPCDomain.Server)]
		protected virtual void Server_ReadyToPlay(bool ready) { }

		[RPC(ERPCDomain.Server)]
		protected virtual void Server_ExecuteCFunc(string command) {}

		[RPC(ERPCDomain.Owner)]
		protected virtual void Owner_ConsolePrint(byte logType, string message) {
			if (world is Client.ClientWorld) {
				ConsolePrint((LogType)logType, message);
			}
		}

		[RPC(ERPCDomain.Owner)]
		protected virtual void Owner_Explosion(ExplosiveForce explosion) { }

		[RPC(ERPCDomain.Owner)]
		protected virtual void Owner_Say(PlayerState player, string text) { }

		[RPC(ERPCDomain.Owner)]
		protected virtual void Owner_SayTeam(PlayerState player, string text) { }

		[RPC(ERPCDomain.Server)]
		protected virtual void Server_Say(string text) { }

		[RPC(ERPCDomain.Server)]
		protected virtual void Server_SayTeam(string text) { }

		[RPC(ERPCDomain.Server)]
		protected virtual void Server_SocketItem(int id, byte rune, byte gem) { }

		[RPC(ERPCDomain.Server)]
		protected virtual void Server_FlushSocketedItems() { }

		[RPC(ERPCDomain.Owner)]
		protected virtual void Owner_HUDDisplaySubtitle(string key, float stayTime) { }

		[RPC(ERPCDomain.Owner)]
		protected virtual void Owner_VO_WaveComplete() { }

		[RPC(ERPCDomain.Owner)]
		protected virtual void Owner_VO_AssassinsSpawned() { }

		[RPC(ERPCDomain.Server)]
		protected virtual void Server_PickupItem(ItemPickupActor target) { }

		[RPC(ERPCDomain.Owner)]
		protected virtual void Owner_ServerGrantedItem(int id, int count) { }
		
		public virtual void ConsolePrint(LogType logType, string message) {
			switch (logType) {
				case LogType.Assert:
					Debug.LogError(message);
				break;
				case LogType.Error:
					Debug.LogError(message);
				break;
				case LogType.Exception:
					Debug.LogError(message);
				break;
				case LogType.Warning:
					Debug.LogWarning(message);
				break;
				default:
					Debug.Log(message);
				break;
			}
		}
		
		public override void BeginTravel() {
			base.BeginTravel();
			_playerState = null;
		}

		public abstract void GlobalCooldown(Spells.Ability instigator);

		protected Vector3 startingPosition {
			get {
				return _startingPosition;
			}
		}

		protected float startingRotation {
			get {
				return _startingRotation;
			}
		}

		public virtual void OnMatchWaitingForPlayers() { }
		public virtual void OnMatchCountdown() { }
		public virtual void OnStartUnitTrading() { }

		public virtual void OnMatchStart() {
		}
		
		public virtual void OnMatchOvertime() { }
		public virtual void OnMatchComplete() { }
		public virtual void OnMatchFreeze() { }
		public virtual void OnMatchExit() { }
		
		public override Type clientType {
			get {
				return typeof(Client.Actors.ClientPlayerController);
			}
		}

		public override Type serverType {
			get {
				return typeof(Server.Actors.ServerPlayerController);
			}
		}
	}
}