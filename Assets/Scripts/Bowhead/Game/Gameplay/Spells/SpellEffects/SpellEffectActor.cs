// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using System;

namespace Bowhead.Actors.Spells {
	public abstract class SpellEffectActor<T> : SpellEffectActor where T : SpellEffectActor<T>, new() {
		public override Type serverType {
			get {
				return typeof(T);
			}
		}

		public override Type clientType {
			get {
				return typeof(T);
			}
		}
	}

	public abstract class SpellEffectActor : Actor {

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		Actor _instigatingActor;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		PlayerState _instigatingPlayer;
		[Replicated(Condition = EReplicateCondition.InitialOnly, Notify = "OnRep_Attached")]
		DamageableActor _attached;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		StaticAssetRef<SpellClass> _spellClass;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		StaticAssetRef<SpellEffectClass> _effectClass;

		[Replicated(UpdateRate = 1f)]
		float _time;
		[Replicated(UpdateRate = 1f)]
		float _duration;
		[Replicated(UpdateRate = 1f)]
		float _tickRate;
		[Replicated(Notify = "OnRep_StackCount")]
		byte _stackCount;

		Spell _spell;
		float _fogOfWarLocalAlpha;
		AudioSource _castSound;

		readonly ActorRPC<byte> rpc_Multicast_ProcEnd;
		
		public SpellEffectActor() {
			SetReplicates(true);
			rpc_Multicast_ProcEnd = BindRPC<byte>(Multicast_ProcEnd);
		}

		public void ServerInit(Spell spell, SpellEffectClass effectClass) {
			_spell = spell;
			_spellClass = spell.spellClass;
			_effectClass = effectClass;
			_duration = spell.duration;
			_tickRate = spell.tickRate;
			_stackCount = (byte)spell.stackDepth;

			_instigatingActor = spell.instigatingActor;
			_instigatingPlayer = (spell.instigatingPlayer != null) ? spell.instigatingPlayer.playerState : null;

			// can be attached multiple times if the effect is transfered
			// due to stackmode == replace
			if (_attached != spell.target) {
				if (_attached != null) {
					_attached.NotifyEffectRemoved(this);
				}
				_attached = spell.target;
				_attached.NotifyEffectAdded(this);
			}
        }

		public override void Tick() {
			base.Tick();

			if (hasAuthority) {
				_time = _spell.time;
				_duration = _spell.duration;
				_tickRate = _spell.tickRate;

				var oldDepth = _stackCount;
				_stackCount = (byte)_spell.stackDepth;

				if (stackCount != oldDepth) {
					NetFlush();
				}
			} else if (_attached != null) {
				_time += world.deltaTime*_tickRate;
				if ((_duration > 0f) && (_time > _duration)) {
					_time = _duration;
				}
			
				UpdateLocalFogOfWarVisibility();

				if (fogOfWarLocalVisibility) {
					if (_fogOfWarLocalAlpha < 1f) {
						bool visChange = (_fogOfWarLocalAlpha <= 0f);
						_fogOfWarLocalAlpha += world.deltaTime*DamageableActor.FOGOFWAR_FADE_TIME;
						if (_fogOfWarLocalAlpha > 1f) {
							_fogOfWarLocalAlpha = 1f;
						}
						OnFogOfWarLocalAlphaChanged(visChange);
					}
				} else if (_fogOfWarLocalAlpha > 0f) {
					_fogOfWarLocalAlpha -= world.deltaTime*DamageableActor.FOGOFWAR_FADE_TIME;
					if (_fogOfWarLocalAlpha <= 0f) {
						_fogOfWarLocalAlpha = 0f;
						OnFogOfWarLocalAlphaChanged(true);
					} else {
						OnFogOfWarLocalAlphaChanged(false);
					}
				}
			}
		}

		void InitFogOfWar() {
			UpdateLocalFogOfWarVisibility();
			if (fogOfWarLocalVisibility) {
				_fogOfWarLocalAlpha = 1f;
			} else {
				_fogOfWarLocalAlpha = 0f;
			}
		}

		protected virtual void OnProcBegin() {
			if (fogOfWarLocalVisibility) {
				_castSound = GameManager.instance.Play(target, effectClass.sounds.cast);
				if ((_castSound != null) && !_castSound.loop) {
					_castSound = null;
				}
			}
		}

		protected virtual void OnProcBeginImmediate() {}

		protected virtual void OnProcEnd(EExpiryReason reason) {
			if (fogOfWarLocalVisibility) {
				if (_castSound != null) {
					Utils.DestroyGameObject(_castSound.gameObject);
					_castSound = null;
				}

				switch (reason) {
					case EExpiryReason.Cleansed:
						GameManager.instance.Play(target, effectClass.sounds.cleansed);
					break;
					case EExpiryReason.Expired:
						GameManager.instance.Play(target, effectClass.sounds.expired);
					break;
				}
			}
		}

		protected virtual void OnFogOfWarLocalVisibilityChanged() { }
		protected virtual void OnFogOfWarLocalAlphaChanged(bool visChange) { }

		public virtual void UpdateLocalFogOfWarVisibility() {
			_attached.UpdateLocalFogOfWarVisibility();
			var vis = _attached.fogOfWarLocalVisibility;
			if (vis != fogOfWarLocalVisibility) {
				fogOfWarLocalVisibility = vis;
				OnFogOfWarLocalVisibilityChanged();
			}
			if (!vis && (_castSound != null)) {
				Utils.DestroyGameObject(_castSound.gameObject);
				_castSound = null;
			}
		}

		public virtual void OnRep_Attached() {
			_attached.NotifyEffectAdded(this);
			CreateOrAttachActorGameObject();
			InitFogOfWar();
			if (_time < 0.5f) {
				OnProcBegin();
			} else {
				OnProcBeginImmediate();
			}
		}

		public override bool IsNetRelevantFor(ActorReplicationChannel channel) {
			return _attached.disposed ? false : _attached.IsNetRelevantFor(channel);
		}

		protected override GameObject CreateActorGameObject() {
			return null;
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			if (disposing && (_attached != null)) {
				if (!_attached.pendingKill) {
					_attached.NotifyEffectRemoved(this);
				}
			}
		}

		public void ServerEndProc(EExpiryReason reason) {
			rpc_Multicast_ProcEnd.Invoke((byte)reason);
			if (effectClass.timeToLiveAfterSpellExpiry > 0f) {
				NetTearOff();
			}
			Destroy();
		}

		[RPC(ERPCDomain.Multicast, Reliable = true)]
        void Multicast_ProcEnd(byte reason) {
			OnProcEnd((EExpiryReason)reason);
		}

		protected override void OnNetTornOff() {
			base.OnNetTornOff();
			if (!hasAuthority) {
				SetLifetime(effectClass.timeToLiveAfterSpellExpiry);
			}
		}

		protected virtual void OnRep_StackCount() {}

		public Actor instigatingActor {
			get {
				return _instigatingActor;
			}
		}

		public PlayerState instigatingPlayer {
			get {
				return _instigatingPlayer;
			}
		}

		public DamageableActor target {
			get {
				return _attached;
			}
		}

		public SpellClass spellClass {
			get {
				return _spellClass;
			}
		}

		public SpellEffectClass effectClass {
			get {
				return _effectClass;
			}
		}

		public int stackCount {
			get {
				return _stackCount;
			}
		}

		public float unscaledDuration {
			get {
				return _duration;
			}
		}

		public float duration {
			get {
				if (tickRate > 0f) {
					return _duration / tickRate;
				}
				return _duration;
			}
		}

		public float tickRate {
			get {
				return _tickRate;
			}
		}

		public float time {
			get {
				return _time;
			}
		}

		public float timeLeft {
			get {
				if (_duration > 0f) {
					return Mathf.Max(_duration - _time, 0f);
				}
				return 0f;
			}
		}

		public float timeFraction {
			get {
				if (_duration > 0f) {
					return 1f - (Mathf.Max(_duration - _time, 0f) / _duration);
				}
				return 0f;
			}
		}

		public bool fogOfWarLocalVisibility {
			get;
			protected set;
		}

	}
}