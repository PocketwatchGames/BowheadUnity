// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace Bowhead.Client.UI {
	public class LineEditDialog : ModalDialog {

		[SerializeField]
		TMPro.TextMeshProUGUI _title;
		[SerializeField]
		TMPro.TextMeshProUGUI _message;
		[SerializeField]
		TMPro.TMP_InputField _input;
		[SerializeField]
		Button[] _buttons;
		[SerializeField]
		TMPro.TextMeshProUGUI[] _buttonLabels;

		ValidateInputDelegate _validate;

		public delegate bool ValidateInputDelegate(string text);

		public static void Display(string title, string message, string inputText, EMaskEffect effect, Action<EResult> dialogResult, ValidateInputDelegate validate) {
			Display(GameManager.instance.clientData.defaultLineEditorDialogPrefab, title, message, inputText, effect, dialogResult, validate);
		}

		public static void Display(LineEditDialog template, string title, string message, string inputText, EMaskEffect effect, Action<EResult> dialogResult, ValidateInputDelegate validate) {
			Display(template, title, message, inputText, Utils.GetLocalizedText("UI.MessageBox.OK"), Utils.GetLocalizedText("UI.MessageBox.Cancel"), effect, dialogResult, validate);
		}

		public static void Display(LineEditDialog template, string title, string message, string inputText, string okButtonText, string cancelButtonText, EMaskEffect effect, Action<EResult> dialogResult, ValidateInputDelegate validate) {
			var dialog = Create(template, effect, dialogResult);

			dialog._validate = validate;
			dialog._title.text = title;
			dialog._message.text = message;
			dialog._input.text = inputText;
			dialog._buttonLabels[0].text = okButtonText;
			dialog._buttonLabels[1].text = cancelButtonText;

			dialog._buttons[0].onClick.AddListener(() => {
				dialog.Close(EResult.OK);
			});

			dialog._buttons[1].onClick.AddListener(() => {
				dialog.Close(EResult.Cancel);
			});

			value = inputText;

			dialog._input.onValueChanged.AddListener(dialog.Validate);
			EventSystem.current.SetSelectedGameObject(dialog._input.gameObject);

			dialog.Validate(inputText);
			dialog.Center();
			dialog.WaitForKeys(EResult.OK, EResult.Cancel);
		}

		void Validate(string text) {
			defaultAcceptValid = ValidateInput(text);
			_buttons[0].interactable = defaultAcceptValid;
		}

		bool ValidateInput(string text) {
			return (_validate != null) ? _validate(text) : true;
		}

		public override bool Close(EResult result) {
			value = _input.text;
			return base.Close(result);
		}

		public static string value {
			get;
			private set;
		}
	}
}
