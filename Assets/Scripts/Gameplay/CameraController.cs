using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {
    public sealed class CameraController {

        public CameraData data;

        private float _yaw;
        private float _pitch;
        private Player _target;
        private Vector3 _position;
        private bool _isLooking;
        private bool _mouseLookActive;
        private float _shakeTime;
        private float _shakeTimeTotal;
        private float _shakeAngleMag;
        private float _shakePositionMag;

        private Vector3 _playerPosition;
        private Vector3 _cameraVelocity;
        private Vector3 _lookAtVelocity;
        private Vector3 _lookAt;

		Camera _camera;

		public CameraController(Camera camera, CameraData d) {
			_camera = camera;
            data = d;
			Init();
		}
        
        public void Update(float dt) {

            HandleInput(0, dt);
            Tick(dt);

            Vector3 pos;
            float yaw, pitch;
            GetPositionAngles(out pos, out yaw, out pitch);

            _camera.transform.SetPositionAndRotation(pos, Quaternion.Euler(new Vector3(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0)));

            Debug.DrawLine(_target.headPosition(_target.position), _lookAt);
        }

        public float GetYaw() {
            return _yaw;
        }

        public void SetMouseLookActive(bool a) {
            _mouseLookActive = a;
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
			
            float mouseTurnSpeed = 360f * Mathf.Deg2Rad;
            float gpTurnSpeed = 360f * Mathf.Deg2Rad;

            _isLooking = false;

            if (_mouseLookActive) {
                var m = Input.mousePosition;
                var mouseDelta = m - _oldMousePosition;

                Vector2 gamepad = new Vector2(Input.GetAxis("LookHorizontal"), Input.GetAxis("LookVertical"));
                _yaw += gamepad.x * gpTurnSpeed * dt;
                _pitch += gamepad.y * gpTurnSpeed * dt;

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
            if (_target != null) {

                float minDist = Mathf.Sqrt(Mathf.Max(0, _pitch) / (Mathf.PI / 2)) * (data.maxDistance - data.minDistance) + data.minDistance;

                Vector3 avgPlayerPosition = _target.headPosition(_target.renderPosition());
                Vector3 lookAtDiff = avgPlayerPosition - _lookAt;
                bool isMoving = _playerPosition != avgPlayerPosition;
                Vector3 playerMovement = avgPlayerPosition - _playerPosition;
                if (_isLooking) {

                    _playerPosition = avgPlayerPosition;
                    _lookAtVelocity -= _lookAtVelocity * data.lookAtFriction * dt;
                    _lookAtVelocity += (lookAtDiff + playerMovement.normalized * data.lookAtLeadDist) * data.lookAtAcceleration * dt;
                    _lookAt += _lookAtVelocity * dt;

                    Vector3 diff = _position - _lookAt;
                    diff.y = 0;
                    if (diff == Vector3.zero)
                        diff.x = 1;
                    diff.Normalize();
                    diff *= minDist;
                    var desiredCameraMove = (_lookAt + diff) - _position;

                    _cameraVelocity -= _cameraVelocity * data.cameraFriction * dt;
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

                        float cameraFriction = 10f;

                        _playerPosition = avgPlayerPosition;

                        if (!isMoving) {
                            _lookAtVelocity -= _lookAtVelocity * data.lookAtFriction * dt;
                        }
                        else {

                            _lookAtVelocity -= _lookAtVelocity * data.lookAtFriction * dt;
                            _lookAtVelocity += (lookAtDiff + playerMovement.normalized * data.lookAtLeadDist) * data.lookAtAcceleration * dt;
                        }
                        _lookAt += _lookAtVelocity * dt;

                        Vector3 diff = _position - _lookAt;
                        diff.y = 0;
                        if (diff == Vector3.zero)
                            diff.x = 1;
                        diff.Normalize();
                        diff *= minDist;

                        var desiredCameraMove = (_lookAt + diff) - _position;

                        _cameraVelocity -= _cameraVelocity * cameraFriction * dt;
                        _cameraVelocity += desiredCameraMove * dt;

                        _position += _cameraVelocity * dt;
                        _position = new Vector3(_position.x, Mathf.Max(_position.y, avgPlayerPosition.y), _position.z);

                        diff = _position - _lookAt;
                        diff.y = 0;
                        if (diff.magnitude < 0.1f)
                            diff.x = 1;
                        diff.Normalize();
                        diff *= minDist;

                        _yaw = Mathf.Atan2(-diff.x, -diff.z);

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

            _shakeTime = Mathf.Max(0, _shakeTime - dt);
        }


        void Init() {
            _position = Vector3.zero;

            _yaw = 0f;
            _pitch = 45f * Mathf.Deg2Rad;
        }


        public void Shake(float time, float pos, float angle) {
            _shakeTime = _shakeTimeTotal = time;
            _shakeAngleMag = angle;
            _shakePositionMag = pos;
        }

        void GetPositionAngles(out Vector3 _pos, out float _yaw, out float _pitch) {
            _pos = _position;

            RaycastHit hit;
            var dir = _position - _lookAt;
            if (Physics.Raycast(_lookAt, dir.normalized, out hit, dir.magnitude, Bowhead.Layers.ToLayerMask(Bowhead.ELayers.Terrain))) {
                _pos = hit.point;
            }


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
                //int perlinTime = (int)(world.time * 100);
                //_pos.x += t * shakePositionMag * GetPerlinValue(perlinTime, perlinTime + 5422, perlinTime + 123, 0.1f);
                //_pos.y += t * shakePositionMag * GetPerlinValue(perlinTime, perlinTime + 5, perlinTime + 165423, 0.1f);
                //_pos.z += t * shakePositionMag * GetPerlinValue(perlinTime, perlinTime + 542462, perlinTime + 1253, 0.1f);
                //_yaw += t * shakeAngleMag * GetPerlinValue(perlinTime, perlinTime + 52, perlinTime + 13, 0.1f);
                //_pitch += t * shakeAngleMag * GetPerlinValue(perlinTime, perlinTime + 542, perlinTime + 1273, 0.1f);
            }
        }

    }
}