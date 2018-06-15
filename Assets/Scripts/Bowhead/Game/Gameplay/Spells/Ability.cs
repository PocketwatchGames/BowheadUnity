// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Collections.Generic;
using Bowhead.Client.Actors;
using Bowhead.Server.Actors;

namespace Bowhead.Actors.Spells {
	
	public class Ability : Actor {
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		StaticAssetRef<AbilityClass> _abilityClass;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		int _index;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		int _level;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		PlayerController _player;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		float _baseCooldownTime;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		float _basePickupCooldownTime;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		float _spellPower;

		Team _serverTeam;
		ClientPlayerController _clientPlayer;
		int _useCount;
		bool _clientValid;
		float _cooldown;
		float _cooldownTime;
		float _cdAdvance;
		ActorSingleton<GameState> _gameState;
		
		Dictionary<DamageableActor, List<Spell>> _activeSpells;
		Dictionary<DamageableActor, List<Spell>> _passiveSpells;
		List<DamageableActor> _removeList;

		AreaOfEffectActor _placedAOE;
		bool _clientPlaced;
		bool _clientPlacing;
		bool _aoeDead;

		readonly ActorRPC rpc_Owner_InstantCooldown;
		readonly ActorRPC<bool, bool, int> rpc_Owner_ServerResponse;
		//readonly ActorRPC<List<Unit>, Vector3> rpc_Server_Execute;
		readonly ActorRPC<float> rpc_Owner_SetCooldown;
		//readonly ActorRPC<UnitGravestoneActor> rpc_Server_ActivateGravestone;
		readonly ActorRPC rpc_Owner_AOEDead;

		public Ability() {
			SetReplicates(true);
			SetOwnerOnly(true);

			rpc_Owner_InstantCooldown = BindRPC(Owner_InstantCooldown);
			rpc_Owner_ServerResponse = BindRPC<bool, bool, int>(Owner_ServerResponse);
			//rpc_Server_Execute = BindRPC<List<Unit>, Vector3>(Server_Execute);
			rpc_Owner_AOEDead = BindRPC(Owner_AOEDead);
			rpc_Owner_SetCooldown = BindRPC<float>(Owner_SetCooldown);
			//rpc_Server_ActivateGravestone = BindRPC<UnitGravestoneActor>(Server_ActivateGravestone);
		}

		public override void PostNetConstruct() {
			base.PostNetConstruct();
			_clientPlayer = (ClientPlayerController)_player;
			ClientInit();
		}

		public override void Tick() {
			base.Tick();

			var prevCooldown = _cooldown;

			_cooldown += world.deltaTime+_cdAdvance;

			if (_cooldown >= _cooldownTime) {
				_cooldown = _cooldownTime;

				if (hasAuthority && (prevCooldown < _cooldown)) {
					// flush cooldown state.
					// this fixes a race condition:
					// 1) Assume that the server cooldown clock on ability B is slightly ahead of client predicted time
					// 2) Ability B CD is < GlobalCoolDownTime, but client CD is > GlobalCoolDownTime and _cooldownTime is 15 seconds
					// 3) Client executes ability A causing a GCD on B on server, but not on client since client CD > GlobalCoolDownTime
					// 4) Ability B GCD on server means CD is zero, and _cooldownTime is GCD (1 second).
					// 5) Ability B on client appears to finally fully cool, even though on server it is still in GCD
					// 6) Player executes ability B as soon as it appears cooled, setting CD = 0 and _cooldownTime = 15
					// 7) Server replies with failure, client sets CD = _cooldownTime
					// 8) Quickening ability causes server to accelerate and flush the server CD time which is a max of 1 second from the prior GCD
					// 9) The client sets CD to 1 (from server call), meaning client now sees CD of 1 second out of _cooldownTime of 15
					// 10) Each time ability B's CD is advanced from Quickening ability it resets the timer to 1 second on the client.

					// The fix for this is to flush the cooldown time on a client when it is fully cooled on the server
					// and avoid sending CD timing advances if the ability is fully cooled.

					prevCooldown = _cooldown;
					rpc_Owner_InstantCooldown.Invoke();
				}
			}

			if (_cdAdvance > 0f) {
				Assert.IsTrue(hasAuthority);
				_cdAdvance = 0f;
				if (_cooldown > prevCooldown) { // only do deltas
					rpc_Owner_SetCooldown.Invoke(_cooldown);
				}
			}

			if (hasAuthority) {
				if (_placedAOE != null) {
					_aoeDead = _placedAOE.pendingKill || _placedAOE.dead;
					if (_aoeDead) {
						_placedAOE = null;
						rpc_Owner_AOEDead.Invoke();
					}
				}

				if (_activeSpells != null) {
					foreach (var pair in _activeSpells) {
						if (pair.Key.pendingKill || pair.Key.dead) {
							_removeList.Add(pair.Key);
						} else {
							for (int i = pair.Value.Count-1; i >= 0; --i) {
								var spell = pair.Value[i];
								if (spell.disposed) {
									pair.Value.RemoveAt(i);
								}
							}
							if (pair.Value.Count < 1) {
								if (abilityClass.passivity == EPassiveAbilityMode.PassivesCleansedAndRecastAfterActiveSpells) {
									ServerActivatePassives(pair.Key);
								}
								_removeList.Add(pair.Key);
							}
						}
					}

					for (int i = 0; i < _removeList.Count; ++i) {
						_activeSpells.Remove(_removeList[i]);
					}
					_removeList.Clear();
				}
			}
		}

		public void ServerInit(ServerPlayerController player, Team team, AbilityClass abilityClass, int level, float spellPower, int index, float cooldownMultiplier) {
			SetOwningConnection(player.ownerConnection);
			_gameState = new ActorSingleton<GameState>(world);
			_player = player;
			_serverTeam = team;
			_abilityClass = abilityClass;
			_index = index;
			_level = level;
			_spellPower = spellPower;
			_baseCooldownTime = Mathf.Max(AbilityClass.GLOBAL_COOLDOWN, abilityClass.cooldown) * cooldownMultiplier;
			_basePickupCooldownTime = Mathf.Max(AbilityClass.GLOBAL_COOLDOWN, abilityClass.cooldownWhenAOEPickedUp) * cooldownMultiplier;
			_cooldownTime = _baseCooldownTime;
			_cooldown = _cooldownTime;
			//ServerActivatePassives();
		}

		public void ServerInstantCooldown() {
			_cooldown = _cooldownTime;
			rpc_Owner_InstantCooldown.Invoke();
		}

		public void ClientInit() {
			_gameState = new ActorSingleton<GameState>(world);
			_cooldownTime = _baseCooldownTime;
			_cooldown = _cooldownTime;
			//ClientSelectionChanged();
			//ClientPlayerController.localPlayer.ClientAbilitySpawned(this, _index);
		}

		//public bool ClientActivateGravestone(UnitGravestoneActor gravestone) {
		//	if (abilityClass.isResurrect && ClientPlayerController.localPlayer.CanActivateGravestone(gravestone)) {
		//		_clientPlacing = false;
		//		ClientPlayerController.localPlayer.EndResurrectionSelection();
		//		rpc_Server_ActivateGravestone.Invoke(gravestone);
		//		_cooldownTime = _baseCooldownTime;
		//		ClientStartCooldown();
		//		return true;
		//	}
		//	return false;
		//}

		//public void ClientCancelResurrection() {
		//	_clientPlacing = false;
		//	ClientPlayerController.localPlayer.EndResurrectionSelection();
		//}

		public void GlobalCooldown() {
			if (!abilityClass.hasActiveSpells || outOfCharges) {
				return;
			}

			if (_cooldown < _cooldownTime) {
				// still cooling down? should GC restart?
				var d = _cooldownTime - _cooldown;
				if (d >= AbilityClass.GLOBAL_COOLDOWN) {
					return;
				}
			}

			_cooldownTime = AbilityClass.GLOBAL_COOLDOWN + ClientPlayerController.localPlayerPingSeconds * 0.5f;
			_cooldown = 0f;
		}

		public bool ClientExecute() {
			//if (clientValid) {
			//	if (abilityClass.isResurrect) {
			//		if (_clientPlacing) {
			//			_clientPlacing = false;
			//			ClientPlayerController.localPlayer.EndResurrectionSelection();
			//		} else {
			//			_clientPlacing = true;
			//			ClientPlayerController.localPlayer.BeginResurrectionSelection(this);
			//		}
			//	} else if (abilityClass.aoeClass) {
			//		if (_clientPlaced) {
			//			_clientPlaced = false;
			//			_cooldownTime = _basePickupCooldownTime;
			//			// tell server to pickup
			//			rpc_Server_Execute.Invoke(null, Vector3.zero);
			//			ClientStartCooldown();
			//			return false;
			//		} else if (_clientPlacing) {
			//			ClientPlayerController.localPlayer.CancelTotemPlacement();
			//		} else {
			//			_clientPlacing = true;
			//			ClientPlayerController.localPlayer.BeginTotemPlacement(this);
			//		}
			//	} else {
			//		ClientPlayerController.localPlayer.CancelTotemPlacement();
			//		if (ClientPlayerController.localPlayer.selectedTarget != null) {
			//			rpc_Server_Execute.Invoke(new List<Unit>(new Unit[] { ClientPlayerController.localPlayer.selectedTarget }), Vector3.zero);
			//		} else {
			//			rpc_Server_Execute.Invoke(new List<Unit>(ClientPlayerController.localPlayer.selectedUnits), Vector3.zero);
			//		}
			//		_cooldownTime = _baseCooldownTime;
			//		ClientStartCooldown();
			//	}

			//	return true;
			//} else if (_clientValid && valid) {
			//	// we don't have enough essence.
			//	ClientPlayerController.localPlayer.gameState.hud.FlashSoulBar();
			//}
			return false;
		}

		public void AdvanceCooldown(float dt) {
			if (!abilityClass.hasActiveSpells || outOfCharges) {
				return;
			}

			_cdAdvance += Mathf.Max(dt, 0f);
		}

		void ClientStartCooldown() {
			_cooldownTime += ClientPlayerController.localPlayerPingSeconds * 0.5f;
			_cooldown = 0f;
			++_useCount;
			_player.GlobalCooldown(this);
		}

//		[RPC(ERPCDomain.Server)]
//		void Server_ActivateGravestone(UnitGravestoneActor gravestone) {
//			if ((gravestone == null) || (gravestone.pendingKill || gravestone.activated) || (gravestone.team != _serverTeam) || !valid || !((ServerPlayerController)_player).ConsumeSoulStones(gravestone.unitClass.soulStoneCost)) {
//				rpc_Owner_ServerResponse.Invoke(false, false, _useCount);
//				return;
//			}

//			gravestone.ServerActivate(this);

//			((ServerPlayerController)_player).didUseSpell = _index;
//			rpc_Owner_ServerResponse.Invoke(true, false, ++_useCount);

//#if !(BACKEND_SERVER || LOGIN_SERVER)
//			if (((ServerPlayerController)_player).godMode) {
//				_cooldown = _cooldownTime;
//				rpc_Owner_SetCooldown.Invoke(_cooldown);
//			} else {
//#endif
//				_cooldown = 0f;
//				_player.GlobalCooldown(this);
//#if !(BACKEND_SERVER || LOGIN_SERVER)
//			}
//#endif
//		}

		//[RPC(ERPCDomain.Server)]
		//void Server_Execute(List<Unit> selection, Vector3 pos) {

//			// some units may not get replicated as null if they are dead.
//			selection.RemoveAll(x => x == null);

//			bool notEnoughSoultones = false;
//			var svWorld = ((Server.ServerWorld)world);

//			if ((svWorld.gameMode.baseSoulStonePointScale > 0f) && (abilityClass.activeSoulStoneCost > 0)) {
//				var cost = (int)Math.Floor((double)abilityClass.activeSoulStoneCost * svWorld.gameMode.GetTeamSoulStonePointScale(_player.team));
//				notEnoughSoultones = cost > _player.playerState.soulStonePoints;
//			}

//			if (notEnoughSoultones || !(valid && Validate(selection))) {
//				rpc_Owner_ServerResponse.Invoke(false, (_placedAOE != null) ? _placedAOE.placed : false, _useCount);
//				return;
//			}

//			if (abilityClass.aoeClass != null) {
//				bool failed = true;

//				if ((_placedAOE != null) && _placedAOE.placed) {
//					_cooldownTime = _basePickupCooldownTime;
//					_placedAOE.ServerPickup();
//					failed = false;
//				} else if (ValidatePosition(pos)) {
//					if (_placedAOE == null) {
//						_placedAOE = abilityClass.aoeClass.Spawn<AreaOfEffectActor>(level, spellPower, (Server.ServerWorld)world, (ServerPlayerController)_player, null, null, _serverTeam);
//					}
//					_placedAOE.ServerPlace(pos, 0);
//					_cooldownTime = _baseCooldownTime;
//					if (!abilityClass.canPickupAOE) {
//						_placedAOE = null;
//					}
//					failed = false;
//				}

//				if (failed) {
//					rpc_Owner_ServerResponse.Invoke(false, (_placedAOE != null) ? _placedAOE.placed : false, _useCount);
//					return;
//				}
//			} else {
//				if ((abilityClass.activeSpells != null) && (abilityClass.activeSpells.Length > 0)) {
//					if (abilityClass.passivity != EPassiveAbilityMode.PassivesAlwaysOn) {
//						if (_activeSpells == null) {
//							_activeSpells = new Dictionary<DamageableActor, List<Spell>>();
//							_removeList = new List<DamageableActor>();
//						}

//						if (_passiveSpells != null) {
//							foreach (var pair in _passiveSpells) {
//								for (int i = 0; i < pair.Value.Count; ++i) {
//									pair.Value[i].Dispose();
//								}
//							}
//							_passiveSpells.Clear();
//						}
//					}
//					ServerCastSpells(((abilityClass.selectionTypes != null) && (abilityClass.selectionTypes.Length > 0)) ? ((IList<Unit>)selection) : _player.unitsControlledByPlayer);
//				}

//				_cooldownTime = _baseCooldownTime;
//			}

//			if (abilityClass.activeSoulStoneCost > 0) {
//				((ServerPlayerController)_player).ConsumeSoulStones(abilityClass.activeSoulStoneCost);
//			}

//			((ServerPlayerController)_player).didUseSpell = _index;
//			rpc_Owner_ServerResponse.Invoke(true, (_placedAOE != null) ? _placedAOE.placed : false, ++_useCount);

//#if !(BACKEND_SERVER || LOGIN_SERVER)
//			if (((ServerPlayerController)_player).godMode) {
//				_cooldown = _cooldownTime;
//				rpc_Owner_SetCooldown.Invoke(_cooldown);
//			} else {
//#endif
//				_cooldown = 0f;
//				_player.GlobalCooldown(this);
//#if !(BACKEND_SERVER || LOGIN_SERVER)
//			}
//#endif
		//}

//		void ServerCastSpells(IList<Unit> units) {
//			for (int i = 0; i < units.Count; ++i) {
//				var u = units[i];
//				if (!(u.pendingKill || u.dead)) {
//					SpellCastRule rule;
//					if (SpellCastRule.GetBestRule(abilityClass.activeSpells, _player.team, u, out rule)) {
//						List<Spell> spells = (_activeSpells != null) ? new List<Spell>() : null;
//						rule.Execute(level, spellPower, GameManager.instance.randomNumber, _player.team, null, (ServerPlayerController)_player, u, spells);
//						if (spells != null) {
//							_activeSpells[u] = spells;
//						}
//					}
//				}
//			}
//		}

//		public void NotifyUnitResurrected(Unit unit) {
//			ResurrectionRule rule;
//			if (ResurrectionRule.GetBestRule(abilityClass.resurrectionRules, _player.team, unit, out rule)) {
//				List<Spell> spells = (_activeSpells != null) ? new List<Spell>() : null;
//				rule.Execute(spellPower, _player.team, null, (ServerPlayerController)_player, unit, spells);
//				if (spells != null) {
//					_activeSpells[unit] = spells;
//				}
//			}

//			if (ClientPlayerController.localPlayer != null) {
//				ClientPlayerController.localPlayer.didResurrect = true;
//			}
//		}

//		public void UnitTradedToPlayer(Unit unit) {
//			ServerActivatePassives(unit);
//		}

//		public void UnitTradedFromPlayer(Unit unit) {
//			if (_passiveSpells != null) {
//				List<Spell> spells;
//				if (_passiveSpells.TryGetValue(unit, out spells)) {
//					for (int i = 0; i < spells.Count; ++i) {
//						spells[i].Dispose();
//					}
//					_passiveSpells.Remove(unit);
//				}
//			}
//		}

		[RPC(ERPCDomain.Owner)]
		void Owner_InstantCooldown() {
			_cooldown = _cooldownTime;
		}

		[RPC(ERPCDomain.Owner)]
		void Owner_AOEDead() {
			_aoeDead = true;
		}

		[RPC(ERPCDomain.Owner)]
		void Owner_ServerResponse(bool success, bool placed, int useCount) {
			_clientPlaced = placed;
			if (success) {
				if (abilityClass.aoeClass != null) {
					GameManager.instance.Play(Vector3.zero, ((abilityClass.aoeClass.sounds != null) ? abilityClass.aoeClass.sounds.placed : null) ?? GameManager.instance.clientData.sounds.game.abilitySounds.placedTotem);
				}
			} else {
				_cooldown = _cooldownTime;
				if (abilityClass.aoeClass != null) {
					GameManager.instance.Play(Vector3.zero, GameManager.instance.clientData.sounds.game.abilitySounds.invalidTotemPlacement);
				}
			}
			_useCount = useCount;
		}

		[RPC(ERPCDomain.Owner)]
		void Owner_SetCooldown(float cd) {
			_cooldown = Mathf.Min(cd + (ClientPlayerController.localPlayerPingSeconds * 0.5f), _cooldownTime);
		}

		//public bool Validate(IList<Unit> selection) {
		//	if (!abilityClass.isResurrect && (abilityClass.aoeClass == null) && ((abilityClass.activeSpells == null) || (abilityClass.activeSpells.Length < 1))) {
		//		return false;
		//	}

		//	//if (!abilityClass.ValidateSelection(_player, selection)) {
		//	//	return false;
		//	//}

		//	//return abilityClass.ValidateAllUnits(_player.unitsControlledByPlayer);
		//	return false;
		//}

		/*
		void ServerActivatePassives() {
			var units = _player.unitsControlledByPlayer;
			for (int i = 0; i < units.Count; ++i) {
				var u = units[i];
				if (!(u.pendingKill || u.dead)) {
					ServerActivatePassives(u);
				}
			}
		}*/

		public void ServerActivatePassives(DamageableActor target) {

			if ((abilityClass.passiveSpells != null) && (abilityClass.passiveSpells.Length > 0)) {

				if (_passiveSpells == null) {
					_passiveSpells = new Dictionary<DamageableActor, List<Spell>>();
				}

				SpellCastRule selected;
				if (SpellCastRule.GetBestRule(abilityClass.passiveSpells, _player.team, target, out selected)) {
					List<Spell> spells = (_passiveSpells != null) ? new List<Spell>() : null;
					selected.Execute(level, spellPower, GameManager.instance.randomNumber, _player.team, null, (ServerPlayerController)_player, target, spells);
					if (spells != null) {
						_passiveSpells[target] = spells;
					}
				}
			}
		}

		public float cooldown {
			get {
				if (_cooldownTime <= 0f) {
					return 0f;
				}

				return outOfCharges ? 1f : (_cooldown / _cooldownTime);
			}
		}

		public int chargesLeft {
			get {
				return abilityClass.maxUseCount - _useCount;
			}
		}

		public bool clientValid {
			get {

				//if (_clientValid && valid) {
				//	if (abilityClass.isResurrect) { // rez is valid if we have at least 1 full bubble.
				//		return _player.playerState.soulStonePoints >= ((ClientPlayerController)_player).gameState.GetTeamSoulStonePointScale(_player.team);
				//	} else if (abilityClass.activeSoulStoneCost > 0) {
				//		var cost = (int)Math.Floor(((ClientPlayerController)_player).gameState.GetTeamSoulStonePointScale(_player.team) * (double)abilityClass.activeSoulStoneCost);
				//		return cost <= _player.playerState.soulStonePoints;
				//	} else {
				//		return true;
				//	}
				//}

				return false;
			}
		}

		bool outOfCharges {
			get {
				return (abilityClass.maxUseCount > 0) && (_useCount >= abilityClass.maxUseCount);
			}
		}

		bool valid {
			get {
				//return abilityClass.hasActiveSpells && !_aoeDead && (!_player.allUnitsAreDead || abilityClass.isResurrect) && gameState.playerCanIssueCommands && (_cooldown >= _cooldownTime) && !outOfCharges;
				return false;
            }
		}

		public GameState gameState {
			get {
				return _gameState.obj;
			}
		}

		public AbilityClass abilityClass {
			get {
				return _abilityClass;
			}
		}

		public int level {
			get {
				return _level;
			}
		}

		public float spellPower {
			get {
				return _spellPower;
			}
		}

		public override Type serverType {
			get {
				return typeof(Ability);
			}
		}

		public override Type clientType {
			get {
				return typeof(Ability);
			}
		}
	}

}
