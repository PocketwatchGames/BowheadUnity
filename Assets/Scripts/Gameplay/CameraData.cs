using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenuAttribute(menuName = "CameraData")]
public class CameraData : ScriptableObject {

	public bool allowLook;
	public float lookAtFriction = 10f;
    public float lookAtAcceleration = 20;
    public float lookAtLeadDist = 5;
	public float friction = 10.0f;
	public float leashFollowVelocityRate = 0.15f;

	public float minDistance;
	public float maxDistance;

	public float pitch = 45.0f;
	public float fov = 55;

	[Header("Turning")]
	public bool allowRotation = true;
	public float turnStopTime = 0.1f;
	public float turnAcceleration = 360;
	public float turnMaxSpeed = 360;
	public float turnAccelerationFirstTime = 360;
	public float turnAccelerationSlowTime = 0.75f;
}
