// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;

namespace Bowhead.Actors.Spells {

	public class TagAttachedSpellEffectClass : SpellEffectClass {

		public enum EAttachmentType {
			Attached,
			Unattached,
			UnattachedDontOrient
		}

		[Serializable]
		public class Attachment {
			[EditorTags]
			public string tag;
			[MinMaxSlider(1, 32)]
			public IntMath.Vector2i stackRange;
			public EAttachmentType type;
			public GameObject_WRef prefab;
			[HideInInspector]
			public bool initialized;

			public void Precache() {
				Utils.PrecacheWithSounds(prefab);
			}

			public static void Precache(IList<Attachment> attachments) {
				if (attachments != null) {
					for (int i = 0; i < attachments.Count; ++i) {
						var a = attachments[i];
						if (a != null) {
							a.Precache();
						}
					}
				}
			}
		}

		public Attachment[] procBeginFx;
		public Attachment[] procEndFx;

		public override void Precache() {
			base.Precache();
			Attachment.Precache(procBeginFx);
			Attachment.Precache(procEndFx);
		}

#if UNITY_EDITOR

		private void InitAttachments(Attachment[] attachments) {
			for (int i = 0; i < attachments.Length; ++i) {
				var attachment = attachments[i];
				if ((attachment != null) && !attachment.initialized) {
					attachment.initialized = true;
					attachment.stackRange.x = 1;
					attachment.stackRange.y = 32;
				}
			}
		}

		protected override void OnInitVersion() {

			if (version < 1) {
				spellEffectActorClassString = typeof(TagAttachedSpellEffectActor).FullName;
			}

			if (procBeginFx != null) {
				InitAttachments(procBeginFx);
			}

			if (procEndFx != null) {
				InitAttachments(procEndFx);
			}

			base.OnInitVersion();
		}
#endif
	}
}