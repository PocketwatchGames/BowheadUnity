﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Client.UI {
    public class Compass : MonoBehaviour {

        public GameObject arrowDirection;
        public GameObject arrowWind;

        Camera _camera;
        Bowhead.Actors.Player _player;

        public void Init(Camera camera, Bowhead.Actors.Player player) {
            _camera = camera;
            _player = player;
        }

        // Update is called once per frame
        void Update() {
            arrowDirection.transform.localRotation = Quaternion.AngleAxis(_camera.transform.rotation.eulerAngles.y, Vector3.forward);

            var wind = _player.gameMode.GetWind(_player.position);
			if (wind == Vector3.zero) {
				arrowWind.gameObject.SetActive(false);
			} else {
				arrowWind.gameObject.SetActive(true);
				float windAngle = Mathf.Atan2(-wind.x, wind.z) * Mathf.Rad2Deg;
				arrowWind.transform.localRotation = Quaternion.AngleAxis(windAngle + _camera.transform.rotation.eulerAngles.y, Vector3.forward);

				float windSpeed = wind.magnitude/ _player.gameMode.data.windSpeedStormy;
				arrowWind.transform.localScale = new Vector3(windSpeed, windSpeed, windSpeed);
			}


		}
    }
}
