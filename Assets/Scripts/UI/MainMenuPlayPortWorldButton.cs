// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bowhead.Client.UI {
	[RequireComponent(typeof(Button))]
	public class MainMenuPlayPortWorldButton : MonoBehaviour {

		void Start() {
			GetComponent<Button>().onClick.AddListener(OnClick);
		}

		void OnClick() {
			GameManager.PlayPortWorld();
		}
	}
}