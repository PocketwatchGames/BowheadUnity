﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Client.UI {
    public class ButtonHint : MonoBehaviour {

        private Bowhead.Actors.Entity _target;

        // Use this for initialization
        void Start() {

        }

        // Update is called once per frame
        void Update() {

        }
        private void LateUpdate() {
            if (_target != null) {
                transform.localPosition = Camera.main.WorldToViewportPoint(_target.go.transform.position);
            }
        }

        public void SetTarget(Bowhead.Actors.Entity target) {
            if (target == _target) {
                return;
            }
            if (target == null) {
                gameObject.SetActive(false);
            }
            else if (_target == null) {
                gameObject.SetActive(true);
            }

            _target = target;

        }
    }
}