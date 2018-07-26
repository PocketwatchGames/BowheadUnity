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

        private Player _target;
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

            Debug.DrawLine(_target.headPosition(), _lookAt);
        }

        public float GetYaw() {
            return _yaw;
        }

        public void SetTarget(Player player) {
            if (_target != null) {
                _target.OnLand -= OnLand;
            }

            _target = player;
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
					if (_angularVelocity.magnitude < 0.1f) {
						_angularVelocity = Vector2.zero;
					}
					else {
						_angularVelocity = -_angularVelocity * dt * data.turnStopTime;
					}
				} else {
					_turnAccelerationTimer += dt;
					float acceleration;
					if (_turnAccelerationTimer < data.turnAccelerationSlowTime) {
						acceleration = data.turnAccelerationFirstTime * Mathf.Deg2Rad;
					}
					else {
						acceleration = data.turnAcceleration * Mathf.Deg2Rad;
					}

					_angularVelocity += (gamepad* data.turnMaxSpeed * Mathf.Deg2Rad - _angularVelocity) * dt * acceleration;
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

			if (_target != null) {
				SetMouseLookActive(_target.stance == Player.Stance.Explore);


				float minDist = Mathf.Sqrt(Mathf.Max(0, _pitch) / (Mathf.PI / 2)) * (data.maxDistance - data.minDistance) + data.minDistance;

                Vector3 avgPlayerPosition = _target.headPosition(_target.renderPosition());
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

						if (_target.activity == Pawn.Activity.Climbing) {
							var desiredYaw = Mathf.Atan2(-_target.climbingNormal.x, -_target.climbingNormal.z);
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
							float adjustYawAcceleration = 5;

							float turnToYaw = _adjustYaw - Mathf.Sign(Utils.SignedMinAngleDelta(_adjustYaw * Mathf.Rad2Deg, _yaw * Mathf.Rad2Deg)) * 40 * Mathf.Deg2Rad;
							float desiredVelocity = Utils.SignedMinAngleDelta(turnToYaw * Mathf.Rad2Deg, _yaw * Mathf.Rad2Deg) * Mathf.Deg2Rad * 2;
							_adjustYawVelocity += (desiredVelocity - _adjustYawVelocity) * dt * adjustYawAcceleration;
							_yaw = Utils.NormalizeAngle((_yaw + _adjustYawVelocity * dt) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
						}

						// Mario style leash camera
						if (data.allowRotation) {
							_position += _target.velocity * data.leashFollowVelocityRate * dt;

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


                //Vec2f_t maxPlayerPos = Vec2f_t(-10000, -10000);
                //Vec2f_t minPlayerPos = Vec2f_t(10000, 10000);
                //foreach(var p in players)
                //{
                //	var screenPos = camera.viewport.Project(p.waistPosition, camera.Projection, camera.View, Matrix.Identity);
                //	maxPlayerPos.X = Math.Max(maxPlayerPos.X, screenPos.X);
                //	maxPlayerPos.Y = Math.Max(maxPlayerPos.Y, screenPos.Y);
                //	minPlayerPos.X = Math.Min(minPlayerPos.X, screenPos.X);
                //	minPlayerPos.Y = Math.Min(minPlayerPos.Y, screenPos.Y);

                //}

                //float maxDist = (float)Math.Sqrt(Math.Pow((maxPlayerPos.X - minPlayerPos.X) / camera.viewport.Width, 2) + Math.Pow((maxPlayerPos.Y - minPlayerPos.Y) / camera.viewport.Height, 2)) * 60;
                float maxDist = 0f;
                float cameraDist = Mathf.Clamp(maxDist, minDist, 100f);

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
            if (_shakeTime > 0) {
                float rampUpTime = Mathf.Min(0.05f, _shakeTimeTotal / 2);
                float t;
                if (_shakeTime < rampUpTime) {
                    t = _shakeTime / rampUpTime;
                }
                else {
                    t = 1f - (_shakeTime - rampUpTime) / (_shakeTimeTotal - rampUpTime);
                }
                int perlinTime = (int)(_target.world.time * 100);
                _pos.x += t * _shakePositionMag * _target.gameMode.GetPerlinValue(perlinTime, perlinTime + 5422, perlinTime + 123, 0.1f);
                _pos.y += t * _shakePositionMag * _target.gameMode.GetPerlinValue(perlinTime, perlinTime + 5, perlinTime + 165423, 0.1f);
                _pos.z += t * _shakePositionMag * _target.gameMode.GetPerlinValue(perlinTime, perlinTime + 542462, perlinTime + 1253, 0.1f);
                _yaw += t * _shakeAngleMag * _target.gameMode.GetPerlinValue(perlinTime, perlinTime + 52, perlinTime + 13, 0.1f);
                _pitch += t * _shakeAngleMag * _target.gameMode.GetPerlinValue(perlinTime, perlinTime + 542, perlinTime + 1273, 0.1f);
            }
        }

    }
}