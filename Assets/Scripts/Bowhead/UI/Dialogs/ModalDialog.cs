// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

namespace Bowhead.Client.UI {

	[RequireComponent(typeof(Graphic))]
	public abstract class ModalDialog : MonoBehaviour {

		public enum EResult {
			OK,
			Yes = OK,
			No,
			Cancel
		}

		public enum EMaskEffect {
			None,
			Darken
		}

		Action<EResult> _result;

		protected static T Create<T>(T template, EMaskEffect effect, Action<EResult> dialogResult) where T: ModalDialog {
#if DEDICATED_SERVER
			return null;
#else
			T dialog = Instantiate(template);
			dialog._result = dialogResult;
			dialog.defaultAcceptValid = true;

			GameManager.instance.dialogManager.AddDialog(dialog.gameObject, effect == EMaskEffect.Darken);
			return dialog;
#endif
		}

		public void Center() {
			Canvas.ForceUpdateCanvases();
			var gr = GetComponent<Graphic>();
			gr.rectTransform.CenterWidgetInParent();
		}

		public virtual bool Close(EResult result) {
#if !DEDICATED_SERVER
			GameManager.instance.dialogManager.DestroyDialog(gameObject);
			if (_result != null) {
				_result(result);
			}
#endif
			return true;
		}

		protected void WaitForKeys(EResult accept, EResult cancel) {
			StartCoroutine(CoWaitForKeys(accept, cancel));
		}

		protected bool defaultAcceptValid {
			get;
			set;
		}

		IEnumerator CoWaitForKeys(EResult accept, EResult cancel) {
			while (true) {
				if (defaultAcceptValid && (InputManager.GetKeyState(InputKey.Return).down ||
					InputManager.GetKeyState(InputKey.KeypadEnter).down)) {
					InputManager.FlushKeyState(InputKey.Return);
					InputManager.FlushKeyState(InputKey.KeypadEnter);
					if (Close(accept)) {
						break;
					}
				}
				if (InputManager.GetKeyState(InputKey.Escape).down) {
					if (Close(cancel)) {
						break;
					}
				}

				yield return null;
			}
		}
	}
}
