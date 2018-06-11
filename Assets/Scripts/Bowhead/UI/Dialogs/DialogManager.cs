// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Bowhead.Client.UI {
	public class DialogManager {

		List<Canvas> _canvasStack = new List<Canvas>();
		List<GameObject> _dialogStack = new List<GameObject>();

		public void AddDialog(GameObject dialog, bool darken) {
			var canvas = GameObject.Instantiate(darken ? GameManager.instance.clientData.dialogDarkenCanvasPrefab : GameManager.instance.clientData.dialogNormalCanvasPrefab);

			if (_canvasStack.Count > 0) {
				canvas.sortingOrder = _canvasStack[_canvasStack.Count-1].sortingOrder + 1;
			}

			_canvasStack.Add(canvas);
			_dialogStack.Add(dialog);

			dialog.transform.SetParent(canvas.transform, false);
			EventSystem.current.SetSelectedGameObject(null);
		}

		public void DestroyDialog(GameObject dialog) {
			var index = _dialogStack.FindIndex((x) => x == dialog);
			_dialogStack.RemoveAt(index);
			Utils.DestroyGameObject(_canvasStack[index].gameObject);
			_canvasStack.RemoveAt(index);
			EventSystem.current.SetSelectedGameObject(null);
		}	
		
		public bool dialogOpen {
			get {
				return _dialogStack.Count > 0;
			}
		}
	}
}