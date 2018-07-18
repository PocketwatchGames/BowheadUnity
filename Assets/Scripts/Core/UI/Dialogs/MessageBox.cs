// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.UI;
using System;

namespace Bowhead.Client.UI {

	public sealed class MessageBox : ModalDialog {

		public enum EType {
			Info,
			Question,
			Warning,
			Error
		}

		public enum EButtons {
			OK,
			OKCancel,
			YesNo,
			YesNoCancel
		}
		
		[SerializeField]
		TMPro.TextMeshProUGUI _title;
		[SerializeField]
		TMPro.TextMeshProUGUI _message;
		[SerializeField]
		Button[] _buttons;
		[SerializeField]
		TMPro.TextMeshProUGUI[] _buttonLabels;
		[SerializeField]
		Image[] _icons;
		[SerializeField]
		SoundCue[] _sounds;

		public static MessageBox OK(EType type, string title, string message, EMaskEffect effect, Action<EResult> dialogResult) {
			return Create(type, effect, dialogResult).DoOK(title, message);
		}

		public static MessageBox OKCancel(EType type, string title, string message, EMaskEffect effect, Action<EResult> dialogResult) {
			return Create(type, effect, dialogResult).DoOKCancel(title, message);
		}

		public static MessageBox YesNo(EType type, string title, string message, EMaskEffect effect, Action<EResult> dialogResult) {
			return Create(type, effect, dialogResult).DoYesNo(title, message);
		}

		public static MessageBox YesNoCancel(EType type, string title, string message, EMaskEffect effect, Action<EResult> dialogResult) {
			return Create(type, effect, dialogResult).DoYesNoCancel(title, message);
		}

		static MessageBox Create(EType type, EMaskEffect effect, Action<EResult> dialogResult) {
			var mb = Create(GameManager.instance.clientData.messageBoxPrefab, effect, dialogResult);
						
			int typeIndex = (int)type;
			for (int i = 0; i < mb._icons.Length; ++i) {
				mb._icons[i].gameObject.SetActive(i == typeIndex);
			}

			if ((mb._sounds != null) && (typeIndex < mb._sounds.Length)) {
				GameManager.instance.PlaySound(Vector3.zero, mb._sounds[typeIndex]);
			}

			return mb;
		}



		MessageBox DoOK(string title, string message) {
			_title.text = title;
			_message.text = message;
			_buttons[1].gameObject.SetActive(false);
			_buttons[1].transform.parent.gameObject.SetActive(false);
			_buttons[2].gameObject.SetActive(false);
			_buttons[2].transform.parent.gameObject.SetActive(false);
			_buttonLabels[0].text = Utils.GetLocalizedText("UI.MessageBox.OK");

			_buttons[0].onClick.AddListener(() => {
				Close(EResult.OK);
			});

			WaitForKeys(EResult.OK, EResult.OK);
			Center();
			return this;
		}

		MessageBox DoOKCancel(string title, string message) {
			_title.text = title;
			_message.text = message;
			_buttons[2].gameObject.SetActive(false);
			_buttons[2].transform.parent.gameObject.SetActive(false);
			_buttonLabels[1].text = Utils.GetLocalizedText("UI.MessageBox.OK");
			_buttonLabels[0].text = Utils.GetLocalizedText("UI.MessageBox.Cancel");

			_buttons[0].onClick.AddListener(() => {
				Close(EResult.Cancel);
			});

			_buttons[1].onClick.AddListener(() => {
				Close(EResult.OK);
			});

			WaitForKeys(EResult.OK, EResult.Cancel);
			Center();
			return this;
		}

		MessageBox DoYesNo(string title, string message) {
			_title.text = title;
			_message.text = message;
			_buttons[2].gameObject.SetActive(false);
			_buttons[2].transform.parent.gameObject.SetActive(false);
			_buttonLabels[1].text = Utils.GetLocalizedText("UI.MessageBox.Yes");
			_buttonLabels[0].text = Utils.GetLocalizedText("UI.MessageBox.No");

			_buttons[0].onClick.AddListener(() => {
				Close(EResult.No);
			});

			_buttons[1].onClick.AddListener(() => {
				Close(EResult.Yes);
			});

			WaitForKeys(EResult.Yes, EResult.No);
			Center();
			return this;
		}

		MessageBox DoYesNoCancel(string title, string message) {
			_title.text = title;
			_message.text = message;
			_buttonLabels[2].text = Utils.GetLocalizedText("UI.MessageBox.Yes");
			_buttonLabels[1].text = Utils.GetLocalizedText("UI.MessageBox.No");
			_buttonLabels[0].text = Utils.GetLocalizedText("UI.MessageBox.Cancel");

			_buttons[0].onClick.AddListener(() => {
				Close(EResult.Cancel);
			});

			_buttons[1].onClick.AddListener(() => {
				Close(EResult.No);
			});

			_buttons[2].onClick.AddListener(() => {
				Close(EResult.Yes);
			});

			WaitForKeys(EResult.Yes, EResult.Cancel);
			Center();
			return this;
		}
	}
}