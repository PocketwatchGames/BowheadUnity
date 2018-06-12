﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port { 
public class Camera : MonoBehaviour {

        World world;
        public float yaw;
        float pitch;
        Player target;
        Vector3 position;
        Vector3 _playerPosition;
        Vector3 _cameraVelocity;
        Vector3 _lookAtVelocity;
        Vector3 _lookAt;
        bool isLooking;
        float shakeTime;
        float shakeTimeTotal;
        float shakeAngleMag;
        float shakePositionMag;




        // Use this for initialization
        void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}











    void setTarget(Player player) {
        target = player;
    }
    void handleInput(int playerNum, float dt) {


    //float mouseTurnSpeed = 360f * Mathf.Deg2Rad;
    //float gpTurnSpeed = 360f * Mathf.Deg2Rad;

    isLooking = false;
	//if (cgi.getPlayerRawInput(playerNum, &m, &kb, &gp)) {
	//	cg->camera.yaw += -m->x.fdelta* mouseTurnSpeed;
 //   cg->camera.pitch += m->y.fdelta* mouseTurnSpeed;
 //   isLooking |= m->x.delta != 0 || m->y.delta != 0;

	//	if (gp != nullptr) {
	//		Vec2f_t gamepad = cgi.getDeadZonedGamepad(gp->axis[2].fvalue, gp->axis[3].fvalue);
 //   cg->camera.yaw += -gamepad.x() * gpTurnSpeed * dt;
	//		cg->camera.pitch += gamepad.y() * gpTurnSpeed * dt;

	//		isLooking |= gamepad != Vec2f_t::zero;
	//	}
	//}
	float maxAngle = Mathf.PI/2 * 0.95f;
float minAngle = -Mathf.PI/2 * 0.95f;
	if (pitch > maxAngle)
		pitch = maxAngle;
	if (pitch<minAngle)

        pitch = minAngle;

}

void update(float dt) {
    if (target != null) {

        float minDist = Mathf.Sqrt(Mathf.Max(0, pitch) / (Mathf.PI/2)) * 20 + 20;

        Vector3 avgPlayerPosition = target.renderPosition();
                Vector3 lookAtDiff = avgPlayerPosition - _lookAt;
        bool isMoving = _playerPosition != avgPlayerPosition;
                Vector3 playerMovement = avgPlayerPosition - _playerPosition;
        if (isLooking) {
            float lookAtFriction = 10f;
            float lookAtAcceleration = 20;
            float lookAtLeadDist = 5;
            float cameraFriction = 10f;

            _playerPosition = avgPlayerPosition;
            _lookAtVelocity -= _lookAtVelocity * lookAtFriction * dt;
            _lookAtVelocity += (lookAtDiff + playerMovement * lookAtLeadDist) * lookAtAcceleration * dt;
            _lookAt += _lookAtVelocity * dt;

                    Vector3 diff = position - _lookAt;
            diff.z = 0;
            if (diff == Vector3.zero)
                diff.x = 1;
            diff.Normalize();
            diff *= minDist;
            var desiredCameraMove = (_lookAt + diff) - position;

            _cameraVelocity -= _cameraVelocity * cameraFriction * dt;
            _cameraVelocity += desiredCameraMove * dt;

            position += _cameraVelocity * dt;
            position = new Vector3(position.x, position.y, Mathf.Max(position.z, avgPlayerPosition.z));

        }
        else {


            if (lookAtDiff.magnitude > 100) {
                _lookAt = avgPlayerPosition;
                _lookAtVelocity = Vector3.zero;
                _cameraVelocity = Vector3.zero;
                position = _lookAt;
            }
            else {

                float cameraFriction = 10f;

                _playerPosition = avgPlayerPosition;

                if (!isMoving) {
                    float lookAtFriction = 10f;
                    _lookAtVelocity -= _lookAtVelocity * lookAtFriction * dt;
                }
                else {
                    float leadPlayerMotionSpeed = 0.25f;
                    position += playerMovement * leadPlayerMotionSpeed;


                    float lookAtFriction = 10f;
                    float lookAtAcceleration = 20;
                    float lookAtLeadDist = 5;

                    _lookAtVelocity -= _lookAtVelocity * lookAtFriction * dt;
                    _lookAtVelocity += (lookAtDiff + playerMovement * lookAtLeadDist) * lookAtAcceleration * dt;
                }
                _lookAt += _lookAtVelocity * dt;

                Vector3 diff = position - _lookAt;
                diff.z = 0;
                if (diff == Vector3.zero)
                    diff.x = 1;
                diff.Normalize();
                diff *= minDist;

                var desiredCameraMove = (_lookAt + diff) - position;

                _cameraVelocity -= _cameraVelocity * cameraFriction * dt;
                _cameraVelocity += desiredCameraMove * dt;

                position += _cameraVelocity * dt;
                position = new Vector3(position.x, position.y, Mathf.Max(position.z, avgPlayerPosition.z));

                diff = position - _lookAt;
                diff.z = 0;
                if (diff.magnitude < 0.1f)
                    diff.x = 1;
                diff.Normalize();
                diff *= minDist;

                yaw = Mathf.Atan2(-diff.y, -diff.x);

            }
        }

        float horizDist = Mathf.Cos(pitch);
                Vector3 cameraOffset = new Vector3(-Mathf.Cos(yaw) * horizDist, -Mathf.Sin(yaw) * horizDist, Mathf.Sin(pitch));
        cameraOffset *= minDist;
        position = _lookAt + cameraOffset;


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

        cameraOffset = new Vector3(-Mathf.Cos(yaw) * Mathf.Cos(pitch), -Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch));
        cameraOffset *= cameraDist;
        position = _lookAt + cameraOffset;
    }

    shakeTime = Mathf.Max(0, shakeTime - dt);
}


void initialize() {
    position = Vector3.zero;

    yaw = 0f;
    pitch = 45f * Mathf.Deg2Rad;
}


public void shake(float time, float pos, float angle) {
    shakeTime = shakeTimeTotal = time;
    shakeAngleMag = angle;
    shakePositionMag = pos;
}

void getPositionAngles(out Vector3 _pos, out float _yaw, out float _pitch) {
    _pos = position;
    _yaw = yaw;
    _pitch = pitch;
    if (shakeTime > 0) {
        float rampUpTime = Mathf.Min(0.05f, shakeTimeTotal / 2);
        float t;
        if (shakeTime < rampUpTime) {
            t = shakeTime / rampUpTime;
        }
        else {
            t = 1f - (shakeTime - rampUpTime) / (shakeTimeTotal - rampUpTime);
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