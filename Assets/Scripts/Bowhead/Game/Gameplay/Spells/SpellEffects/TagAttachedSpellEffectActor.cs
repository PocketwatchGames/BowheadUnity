// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;

namespace Bowhead.Actors.Spells {
	public class TagAttachedSpellEffectActor : SpellEffectActor<TagAttachedSpellEffectActor> {

		struct Attachment {
			public TagAttachedSpellEffectClass.Attachment src;
			public GameObject go;
			public List<SpellEvents> events;
			public bool didStart;
		}

		List<Attachment> _attachments = new List<Attachment>();
		bool _didBegin;

		protected override void OnProcBegin() {
			base.OnProcBegin();

			if ((target != null) && (target.go != null)) {
				SpawnAttachments(effectClass.procBeginFx);
			}

			for (int i = 0; i < _attachments.Count; ++i) {
				var att = _attachments[i];
				att.didStart = true;
				for (int k = att.events.Count-1; k >= 0; --k) {
					var e = att.events[k];
					if (e != null) {
						e.EffectStart();
					}
					if (e == null) {
						att.events.RemoveAt(k);
					}
				}
			}

			_didBegin = true;
		}

		protected override void OnProcBeginImmediate() {
			base.OnProcBeginImmediate();

			if ((target != null) && (target.go != null)) {
				SpawnAttachments(effectClass.procBeginFx);
			}

			for (int i = 0; i < _attachments.Count; ++i) {
				var att = _attachments[i];
				att.didStart = true;
				for (int k = att.events.Count-1; k >= 0; --k) {
					var e = att.events[k];
					if (e != null) {
						e.EffectStartImmediate();
					}
					if (e == null) {
						att.events.RemoveAt(k);
					}
				}
			}

			_didBegin = true;
		}

		protected override void OnProcEnd(EExpiryReason reason) {
			base.OnProcEnd(reason);

			var effectClass = this.effectClass;

			if ((target != null) && (target.go != null) && (effectClass.timeToLiveAfterSpellExpiry > 0f)) {
				SpawnAttachments(effectClass.procEndFx);				
			}

			for (int i = 0; i < _attachments.Count; ++i) {
				var att = _attachments[i];
				for (int k = att.events.Count-1; k >= 0; --k) {
					var e = att.events[k];
					if (e != null) {
						e.EffectStop();
					}
					if (e == null) {
						att.events.RemoveAt(k);
					}
				}
			}
		}

		protected override void OnRep_StackCount() {
			base.OnRep_StackCount();

			if (_didBegin) {
				CleanAttachments();

				for (int i = 0; i < _attachments.Count; ++i) {
					var att = _attachments[i];

					for (int k = att.events.Count-1; k >= 0; --k) {
						var e = att.events[k];
						if ((e != null) && !att.didStart) {
							e.EffectStartImmediate();
						}
						if (e == null) {
							att.events.RemoveAt(k);
						}
					}

					att.didStart = true;
				}
			}
		}

		void CleanAttachments() {
			for (int i = 0; i < _attachments.Count;) {
				var att = _attachments[i];

				if ((stackCount < att.src.stackRange.x) || (stackCount > att.src.stackRange.y)) {
					Utils.DestroyGameObject(att.go);
					_attachments.RemoveAt(i);
					continue;
				}

				++i;
			}
		}

		void SpawnAttachments(TagAttachedSpellEffectClass.Attachment[] attachments) {
			if (attachments != null) {
				for (int i = 0; i < attachments.Length; ++i) {
					var attachment = attachments[i];
					if ((attachment.prefab != null) && ((stackCount >= attachment.stackRange.x) && (stackCount <= attachment.stackRange.y))) {

						// does this attachment already exist?
						bool alreadySpawned = false;

						for (int k = 0; k < _attachments.Count; ++k) {
							var cur = _attachments[k];
							if (cur.src == attachment) {
								alreadySpawned = true;
								break;
							}
						}

						if (alreadySpawned) {
							continue;
						}

						var gos = target.go.FindTagsInHierarchy(attachment.tag);
						for (int k = 0; k < gos.Length; ++k) {
							var parent = gos[k].transform;
							var go = (GameObject)GameObject.Instantiate(attachment.prefab.Load(), Vector3.zero, Quaternion.identity);
							if (go != null) {
								if (attachment.type != TagAttachedSpellEffectClass.EAttachmentType.Attached) {
									go.transform.position = parent.transform.position;
									if (attachment.type != TagAttachedSpellEffectClass.EAttachmentType.UnattachedDontOrient) {
										go.transform.rotation = parent.transform.rotation;
									}
								} else {
									go.transform.SetParent(parent, false);
								}
								go.SetActive(fogOfWarLocalVisibility);
								var att = new Attachment();
								att.src = attachment;
								att.go = go;
								att.events = new List<SpellEvents>(go.GetComponentsInChildren<SpellEvents>());
                                _attachments.Add(att);
							}
						}
					}
				}
			}
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			_attachments = null;
        }

		protected override void OnFogOfWarLocalVisibilityChanged() {
			base.OnFogOfWarLocalVisibilityChanged();
			for (int i = 0; i < _attachments.Count; ++i) {
				var go = _attachments[i].go;
				go.SetActive(fogOfWarLocalVisibility);
			}
		}

		public new TagAttachedSpellEffectClass effectClass {
			get {
				return (TagAttachedSpellEffectClass)base.effectClass;
			}
		}
	}
}
