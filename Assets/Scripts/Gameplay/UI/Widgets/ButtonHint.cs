using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Client.UI {
    public class ButtonHint : MonoBehaviour {

        private Bowhead.Actors.Entity _target;
		private Vector3 _targetPosition;
		private bool _active;

        public UnityEngine.UI.Text buttonText;
        public UnityEngine.UI.Text hintText;

        // Use this for initialization
        void Start() {

        }

        // Update is called once per frame
        void Update() {

        }
        private void LateUpdate() {
            if (_target != null) {
                transform.position = Camera.main.WorldToScreenPoint(_target.go.transform.position);
            }
        }

		public void SetTarget(Bowhead.Actors.Entity target) {
			if (target == _target) {
				return;
			}

			_target = target;
			_active = _target != null;
			gameObject.SetActive(_active);
		}
		public void SetTarget(Vector3 pos) {
			if (_active && pos == _targetPosition && _target == null) {
				return;
			}

			_targetPosition = pos;
			_active = true;
			gameObject.SetActive(_active);
		}

		public void SetButton(string b) {
            buttonText.text = b;
        }
        public void SetHint(string h) {
            hintText.text = h;
        }
    }
}
