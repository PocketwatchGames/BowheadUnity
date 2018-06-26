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
	public abstract class PlayerController : Actor, MatchStateEventReceiver {
		
		[Replicated(Condition = EReplicateCondition.InitialOnly, Notify = "OnRep_playerState")]
		PlayerState _playerState;
		
		[Replicated(Condition = EReplicateCondition.InitialOnly, Notify = "OnRep_playerPawn")]
		Player _playerPawn;

		public PlayerController() {
			SetReplicates(true);
			SetOwnerOnly(true);
		}

		protected virtual void OnRep_playerState() { }
		protected virtual void OnRep_playerPawn() { }

		public virtual void GetTravelActorNetIds(HashSetList<int> travelActorNetIDs) { }
				
		public PlayerState playerState {
			get {
				return _playerState;
			}
			protected set {
				_playerState = value;
			}
		}
		
		public Player playerPawn {
			get {
				return _playerPawn;
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
		protected void Owner_SetPlayerPawn(Player playerPawn) {
			_playerPawn = playerPawn;
			OnRep_playerPawn();
		}

		[RPC(ERPCDomain.Server)]
		protected virtual void Server_ClientHasLoaded() { }

		[RPC(ERPCDomain.Server)]
		protected virtual void Server_ExecuteCFunc(string command) {}

		[RPC(ERPCDomain.Owner)]
		protected virtual void Owner_ConsolePrint(byte logType, string message) {
			if (world is Client.ClientWorld) {
				ConsolePrint((LogType)logType, message);
			}
		}

		[RPC(ERPCDomain.Owner)]
		protected virtual void Owner_Say(PlayerState player, string text) { }

		[RPC(ERPCDomain.Owner)]
		protected virtual void Owner_SayTeam(PlayerState player, string text) { }

		[RPC(ERPCDomain.Server)]
		protected virtual void Server_Say(string text) { }

		[RPC(ERPCDomain.Server)]
		protected virtual void Server_SayTeam(string text) { }

		[RPC(ERPCDomain.Owner)]
		protected virtual void Owner_HUDDisplaySubtitle(string key, float stayTime) { }

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