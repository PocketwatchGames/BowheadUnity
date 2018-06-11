// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;

namespace Bowhead.Actors.Spells {

	[Serializable]
	public struct AreaOfEffectPrefabs {
		public TotemPlacement_WRef serverPrefab;
		public TotemPlacement_WRef clientPrefab;
		public TotemPlacement_WRef mixedPrefab;
		public TotemPlacement_WRef placementPrefab;

		public void ClientPrecache() {
			WeakAssetRef.Precache(clientPrefab);
			WeakAssetRef.Precache(mixedPrefab);
			WeakAssetRef.Precache(placementPrefab);
		}
	}
	
	public class AreaOfEffectClass : StaticVersionedAssetWithSerializationCallback {
		const int VERSION = 1;

		public enum EAttachmentType {
			Attached,
			Unattached,
			UnattachedDontOrient
		}

		[Serializable]
		public struct Attachment {
			[EditorTags]
			public string tag;
			public EAttachmentType type;
			public GameObject_WRef prefab;

			public void Precache() {
				Utils.PrecacheWithSounds(prefab);
			}

			public static void Precache(IList<Attachment> attachments) {
				if (attachments != null) {
					for (int i = 0; i < attachments.Count; ++i) {
						attachments[i].Precache();
					}
				}
			}
		}

		public ActorMetaClass metaClass;
		public ActorProperty health;
		public DamageClass.Resistance[] resistances;
		public GameObject_WRef minimapIconPrefab;
		public PhysicalMaterialClass physicalMaterial;
		public SpellCastRule[] spells;
		public AreaOfEffectPrefabs prefabs;
		public AreaOfEffectSounds sounds;
		public bool removeEffectWhenOutOfArea;
		public bool orientToGround;
		public bool trackParentRotation;
		public float lifetime;
		public float timeToLiveAfterDestroy;
		public float reapplyRate;
		public float fov;
		public bool unifiedUpdate;
		public float fogOfWarSightRadius;
		public float fogOfWarObjectRadius;
		public float fogOfWarMaxVisRadius;
		public EFogOfWarTest fogOfWarTest;
		public int powerScale;
		public Attachment[] aoePlacedFx;
		public Attachment[] aoePickedUpFx;
		public Attachment[] aoeDestroyedFx;
		public Attachment[] aoeExpiredFx;
		public Attachment[] aoeCastFx;

		[HideInInspector]
		[SerializeField]
		protected string areaOfEffectActorClassString;

		public ActorProperty[] properties {
			get;
			private set;
		}

		bool _precached;

		public T Spawn<T>(int level, float spellPower, Server.ServerWorld world, Server.Actors.ServerPlayerController player, Actor instigator, DamageableActor parent, Team team) where T : AreaOfEffectActor {
			T aoe = (T)world.Spawn(instanceType, null, SpawnParameters.defaultParameters);
			aoe.ServerConstruct(level, spellPower, player, instigator, parent, team, this);
			return aoe;
		}

		private Type instanceType {
			get;
			set;
		}

		public override void OnAfterDeserialize() {
			base.OnAfterDeserialize();

			if (health != null) {
				properties = new[] { health };
			} else {
				properties = null;
			}

			if (string.IsNullOrEmpty(areaOfEffectActorClassString)) {
				instanceType = null;
			} else {
				instanceType = Type.GetType(areaOfEffectActorClassString);
			}
		}

		public override void ClientPrecache() {
			if (!_precached) {
				_precached = true;
				base.ClientPrecache();
				WeakAssetRef.Precache(minimapIconPrefab);
				SpellCastRule.ClientPrecache(spells);
				prefabs.ClientPrecache();
				if (sounds != null) {
					sounds.Precache();
				}
				Attachment.Precache(aoePlacedFx);
				Attachment.Precache(aoePickedUpFx);
				Attachment.Precache(aoeDestroyedFx);
				Attachment.Precache(aoeExpiredFx);
				Attachment.Precache(aoeCastFx);
			}
		}

#if UNITY_EDITOR
	protected sealed override void InitVersion() {
			base.InitVersion();
			OnInitVersion();
			version = VERSION;
		}

		protected virtual void OnInitVersion() {
			if (version < 1) {
				powerScale = 1;
				reapplyRate = 1;
				orientToGround = true;
				removeEffectWhenOutOfArea = true;
			}
		}
#endif
	}
}