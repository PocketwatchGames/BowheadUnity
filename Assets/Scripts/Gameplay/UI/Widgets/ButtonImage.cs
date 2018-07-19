using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ButtonImage : MonoBehaviour {

	[SerializeField]
	UnityEngine.UI.Text _buttonText;

	public void SetButton(string b) {
		_buttonText.text = b;
	}

}
