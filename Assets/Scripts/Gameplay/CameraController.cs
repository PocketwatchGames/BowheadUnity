using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {
    public sealed class CameraController {

		public CameraData exploreData;
		public CameraData combatData;

		private CameraData data;

		private float _yaw;
        private float _pitch;
		private Vector2 _angularVelocity;

		private Vector2 _angleCorrectionVelocity;
		private float _fovVelocity, _fovAcceleration;

        private List<Player> _targets = new List<Player>();
        private Vector3 _position;
        private bool _isLooking;
        private float _shakeTime;
        private float _shakeTimeTotal;
        private float _shakeAngleMag;
        private float _shakePositionMag;

        private Vector3 _playerPosition;
		private Vector3 _targetOffsetPosition;
		private Vector3 _targetOffsetVelocity;

		private Vector3 _cameraVelocity;
        private Vector3 _lookAtVelocity;
        private Vector3 _lookAt;


		private float _adjustYaw;
		private float _adjustYawVelocity;
		private bool _isAdjustingYaw;
		private float _turnAccelerationTimer;
		private float _turnAccelerationResetTimer;

		Camera _camera;

		public CameraController(Camera camera, CameraData cData, CameraData eData) {
			_camera = camera;
			combatData = cData;
			exploreData = eData;
            data = combatData;
			Init();
		}
        
        public void Update(float dt) {

            HandleInput(0, dt);
            Tick(dt);

            Vector3 pos;
            float yaw, pitch;
            GetPositionAngles(out pos, out yaw, out pitch);


			_camera.transform.SetPositionAndRotation(pos, Quaternion.Euler(new Vector3(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0)));

//            Debug.DrawLine(_target.headPosition(), _lookAt);
        }

        public float GetYaw() {
            return _yaw;
        }

		public void ClearTargets() {
			foreach (var t in _targets) {
				t.OnLand -= OnLand;
			}
		}
		public void AddTarget(Player player) {
			if (_targets.Count == 0) {
				_position = player.position;
			}
            _targets.Add(player);
            player.OnLand += OnLand;
        }

        private void OnLand(float damage) {
			if (damage > 0) {
				Shake(0.2f, damage * 0.2f, damage * 0.05f);
			} else {
				Shake(0.15f, 0.05f, 0.01f);
			}
		}


        Vector3 _oldMousePosition;
		void HandleInput(int playerNum, float dt) {


			_isLooking = false;

			if (data.allowLook) {
				var m = Input.mousePosition;
				var mouseDelta = m - _oldMousePosition;

				Vector2 gamepad = new Vector2(Input.GetAxis("LookHorizontal"), Input.GetAxis("LookVertical"));

				if (gamepad == Vector2.zero) {
					_turnAccelerationResetTimer = Mathf.Max(0, _turnAccelerationResetTimer - dt);
					if (_turnAccelerationResetTimer <= 0) {
						_turnAccelerationTimer = 0;
					}
					if (_angularVelocity.magnitude < 0.1f) {
						_angularVelocity = Vector2.zero;
					} else {
						_angularVelocity = -_angularVelocity * dt * data.turnStopTime;
					}
				} else {
					_turnAccelerationResetTimer = data.turnAccelerationSlowResetTime;
					_turnAccelerationTimer += dt;
					float acceleration;
					if (_turnAccelerationTimer < data.turnAccelerationSlowTime) {
						acceleration = data.turnAccelerationFirstTime * Mathf.Deg2Rad;
					} else {
						acceleration = data.turnAcceleration * Mathf.Deg2Rad;
					}

					_angularVelocity += (gamepad * data.turnMaxSpeed * Mathf.Deg2Rad - _angularVelocity) * dt * acceleration;
				}
				if (_angularVelocity.magnitude > data.turnMaxSpeed * Mathf.Deg2Rad) {
					_angularVelocity = _angularVelocity.normalized * data.turnMaxSpeed * Mathf.Deg2Rad;
				}

				_yaw += _angularVelocity.x * dt;
				_pitch += _angularVelocity.y * dt;

				_isLooking |= gamepad != Vector2.zero;

				float maxAngle = Mathf.PI / 2 * 0.95f;
				float minAngle = -Mathf.PI / 2 * 0.95f;
				if (_pitch > maxAngle)
					_pitch = maxAngle;
				if (_pitch < minAngle)

					_pitch = minAngle;


				_oldMousePosition = m;
			} else {
				float turn = 0;
				if (Input.GetButton("ShoulderLeft")) {
					turn = -1;
				} else if (Input.GetButton("ShoulderRight")) {
					turn = 1;
				}
				_yaw += turn * data.turnMaxSpeed * Mathf.Deg2Rad * dt;
			}
		}

        void Tick(float dt) {

			if (!data.allowLook) {
				var curAngles = new Vector2(_yaw, _pitch);
				var desiredAngles = new Vector2(_yaw, Mathf.Deg2Rad * data.pitch);
				float angleCorrectionAcceleration = 2;
				float angleCorrectionTime = 2;
				var desiredVelocity = (desiredAngles - curAngles) / angleCorrectionTime;
				_angleCorrectionVelocity += (desiredVelocity - _angleCorrectionVelocity) * dt * angleCorrectionAcceleration;
				curAngles += _angleCorrectionVelocity * dt;
				_yaw = curAngles.x;
				_pitch = curAngles.y;
			}

			Vector3 avgPlayerPosition = Vector3.zero;
			Vector3 avgPlayerVelocity = Vector3.zero;
			foreach (var t in _targets) {
				avgPlayerPosition += t.headPosition(t.renderPosition());
				avgPlayerVelocity += t.velocity;
			}
			if (_targets.Count > 0) {
				avgPlayerPosition /= _targets.Count;
				avgPlayerVelocity /= _targets.Count;


				float minDist = data.standardLeashDistance;
				if (_targets.Count == 1) {
					minDist = Mathf.Sqrt(Mathf.Max(0, _pitch) / (Mathf.PI / 2)) * (data.maxDistance - data.minDistance) + data.minDistance;
				}

                bool isMoving = _playerPosition != avgPlayerPosition;

				if (isMoving) {
					Vector3 playerMovement = avgPlayerPosition - _playerPosition;
					var desiredTargetOffset = playerMovement.normalized * data.lookAtLeadDist;
					_targetOffsetVelocity = _targetOffsetVelocity + ((desiredTargetOffset - _targetOffsetPosition) - _targetOffsetVelocity) * dt * 10;
					_targetOffsetPosition += _targetOffsetVelocity * dt;
				}
				else {
					_targetOffsetVelocity = Vector3.zero;
				}

				Vector3 lookAtDiff = (avgPlayerPosition + _targetOffsetPosition) - _lookAt;


				if (_isLooking) {

					_isAdjustingYaw = false;
                    _playerPosition = avgPlayerPosition;
                    _lookAtVelocity -= _lookAtVelocity * data.lookAtFriction * dt;
                    _lookAtVelocity += lookAtDiff * data.lookAtAcceleration * dt;
                    _lookAt += _lookAtVelocity * dt;

                    Vector3 diff = _position - _lookAt;
                    diff.y = 0;
                    if (diff == Vector3.zero)
                        diff.x = 1;
                    diff.Normalize();
                    diff *= minDist;
                    var desiredCameraMove = (_lookAt + diff) - _position;

                    _cameraVelocity -= _cameraVelocity * data.friction * dt;
                    _cameraVelocity += desiredCameraMove * dt;

                    _position += _cameraVelocity * dt;
                    _position = new Vector3(_position.x, Mathf.Max(_position.y, avgPlayerPosition.y), _position.z);

                }
                else {


					if (lookAtDiff.magnitude > 100) {
						_lookAt = avgPlayerPosition;
						_lookAtVelocity = Vector3.zero;
						_cameraVelocity = Vector3.zero;
						_position = _lookAt;
					}
					else {


						_playerPosition = avgPlayerPosition;

						if (!isMoving) {
							_lookAtVelocity -= _lookAtVelocity * data.lookAtFriction * dt;
						}
						else {

							_lookAtVelocity -= _lookAtVelocity * data.lookAtFriction * dt;
							_lookAtVelocity += lookAtDiff * data.lookAtAcceleration * dt;
						}
						_lookAt += _lookAtVelocity * dt;

						Vector3 diff = _position - _lookAt;
						diff.y = 0;
						if (diff == Vector3.zero)
							diff.x = 1;
						diff.Normalize();
						diff *= minDist;

						var desiredCameraMove = (_lookAt + diff) - _position;

						_cameraVelocity -= _cameraVelocity * data.friction * dt;
						_cameraVelocity += desiredCameraMove * dt;

						_position += _cameraVelocity * dt;
						_position = new Vector3(_position.x, Mathf.Max(_position.y, avgPlayerPosition.y), _position.z);

						var firstTarget = _targets[0];
						if (_targets.Count == 1 && firstTarget.activity == Pawn.Activity.Climbing) {
							var desiredYaw = Mathf.Atan2(-firstTarget.climbingNormal.x, -firstTarget.climbingNormal.z);
							if (Mathf.Abs(Utils.SignedMinAngleDelta(desiredYaw * Mathf.Rad2Deg, _yaw * Mathf.Rad2Deg)) >= 45) {
								if (_isAdjustingYaw || _adjustYaw != desiredYaw) {
									_isAdjustingYaw = true;
									_adjustYaw = desiredYaw;
								}
							}
						}
						else {
							_isAdjustingYaw = false;
							_adjustYaw = -1000;
						}

						if (_isAdjustingYaw) {

							float turnToYaw = _adjustYaw - Mathf.Sign(Utils.SignedMinAngleDelta(_adjustYaw * Mathf.Rad2Deg, _yaw * Mathf.Rad2Deg)) * data.climbingYawMaxAngle * Mathf.Deg2Rad;
							float desiredVelocity = Utils.SignedMinAngleDelta(turnToYaw * Mathf.Rad2Deg, _yaw * Mathf.Rad2Deg) * Mathf.Deg2Rad * data.climbingYawTurnSpeed;
							_adjustYawVelocity += (desiredVelocity - _adjustYawVelocity) * dt * data.climbingYawAdjustmentAcceleration;
							_yaw = Utils.NormalizeAngle((_yaw + _adjustYawVelocity * dt) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
						}

						// Mario style leash camera
						if (data.allowRotation) {
							_position += avgPlayerVelocity * data.leashFollowVelocityRate * dt;

							diff = _position - _lookAt;
							diff.y = 0;
							if (diff.magnitude < 0.1f)
								diff.x = 1;
							diff.Normalize();
							diff *= minDist;

							var desiredYaw = Mathf.Atan2(-diff.x, -diff.z);
							_yaw = Utils.AngleLerpShortestPath(_yaw * Mathf.Rad2Deg, desiredYaw * Mathf.Rad2Deg, dt / 0.05f) * Mathf.Deg2Rad;
						}
						//}

						// end leash camera
					}
				}

				float horizDist = Mathf.Cos(_pitch);
                Vector3 cameraOffset = new Vector3(-Mathf.Sin(_yaw) * horizDist, Mathf.Sin(_pitch), -Mathf.Cos(_yaw) * horizDist);
                cameraOffset *= minDist;
                _position = _lookAt + cameraOffset;

				Vector3 pos;
				float yaw, pitch;
				GetPositionAngles(out pos, out yaw, out pitch);
				_camera.transform.SetPositionAndRotation(pos, Quaternion.Euler(new Vector3(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0)));



				Vector2 maxPlayerPos = new Vector2(-10000, -10000);
				Vector2 minPlayerPos = new Vector2(10000, 10000);
				foreach (var p in _targets) {
					var screenPos = _camera.WorldToViewportPoint(p.waistPosition());
					maxPlayerPos.x = Mathf.Max(maxPlayerPos.x, screenPos.x);
					maxPlayerPos.y = Mathf.Max(maxPlayerPos.y, screenPos.y);
					minPlayerPos.x = Mathf.Min(minPlayerPos.x, screenPos.x);
					minPlayerPos.y = Mathf.Min(minPlayerPos.y, screenPos.y);

				}

				float maxDist = Mathf.Min(1.0f, Mathf.Sqrt(Mathf.Pow((maxPlayerPos.x - minPlayerPos.x), 2) + Mathf.Pow((maxPlayerPos.y - minPlayerPos.y), 2))) * data.maxLeashDistance;
                float cameraDist = Mathf.Max(maxDist, minDist);

                cameraOffset = new Vector3(-Mathf.Sin(_yaw) * Mathf.Cos(_pitch), Mathf.Sin(_pitch), -Mathf.Cos(_yaw) * Mathf.Cos(_pitch));
                cameraOffset *= cameraDist;
                _position = _lookAt + cameraOffset;

            }

			_fovVelocity += ((data.fov - _camera.fieldOfView) - _fovVelocity) * _fovAcceleration * dt;
			_camera.fieldOfView += _fovVelocity * dt;


			_shakeTime = Mathf.Max(0, _shakeTime - dt);
        }

		private void SetMouseLookActive(bool a) {
			var newMode = a ? exploreData : combatData;
			if (data == newMode) {
				return;
			}
			_turnAccelerationTimer = 0;
			data = newMode;
		}

		void Init() {
            _position = Vector3.zero;

            _yaw = 0f;
            _pitch = 45f * Mathf.Deg2Rad;

			_fovVelocity = 0;
			_fovAcceleration = 10;
		}


        public void Shake(float time, float pos, float angle) {
            _shakeTime = _shakeTimeTotal = time;
            _shakeAngleMag = angle;
            _shakePositionMag = pos;
        }

        void GetPositionAngles(out Vector3 _pos, out float _yaw, out float _pitch) {
            _pos = _position;


            //RaycastHit hit;
            //var dir = _position - _lookAt;
            //if (Physics.Raycast(_lookAt, dir.normalized, out hit, dir.magnitude, Layers.CameraTraceMask)) {
            //    _pos = hit.point;
            //}
			
            _yaw = this._yaw;
            _pitch = this._pitch;


			if (_targets.Count == 0) {
				return;
			}
			if (_shakeTime > 0) {
                float rampUpTime = Mathf.Min(0.05f, _shakeTimeTotal / 2);
                float t;
                if (_shakeTime < rampUpTime) {
                    t = _shakeTime / rampUpTime;
                }
                else {
                    t = 1f - (_shakeTime - rampUpTime) / (_shakeTimeTotal - rampUpTime);
                }
				var firstTarget = _targets[0];
				int perlinTime = (int)(firstTarget.world.time * 100);
                _pos.x += t * _shakePositionMag * firstTarget.gameMode.GetPerlinValue(perlinTime, perlinTime + 5422, perlinTime + 123, 0.1f);
                _pos.y += t * _shakePositionMag * firstTarget.gameMode.GetPerlinValue(perlinTime, perlinTime + 5, perlinTime + 165423, 0.1f);
                _pos.z += t * _shakePositionMag * firstTarget.gameMode.GetPerlinValue(perlinTime, perlinTime + 542462, perlinTime + 1253, 0.1f);
                _yaw += t * _shakeAngleMag * firstTarget.gameMode.GetPerlinValue(perlinTime, perlinTime + 52, perlinTime + 13, 0.1f);
                _pitch += t * _shakeAngleMag * firstTarget.gameMode.GetPerlinValue(perlinTime, perlinTime + 542, perlinTime + 1273, 0.1f);
            }
        }

    }
}