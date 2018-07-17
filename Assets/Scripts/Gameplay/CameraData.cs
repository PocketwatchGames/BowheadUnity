using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenuAttribute(menuName = "CameraData")]
public class CameraData : ScriptableObject {

    public float lookAtFriction = 10f;
    public float lookAtAcceleration = 20;
    public float lookAtLeadDist = 5;
    public float cameraFriction = 10f;
    public float minDistance = 20;
    public float maxDistance = 40;
	public float turnStopTime = 0.1f;
	public float turnAcceleration = 360;
	public float turnMaxSpeed = 360;
	public float turnAccelerationFirstTime = 360;
	public float turnAccelerationSlowTime = 0.75f;
	public float leashFollowVelocityRateExplore = 0.15f;
	public float leashFollowVelocityRateCombat = 0.35f;
}
