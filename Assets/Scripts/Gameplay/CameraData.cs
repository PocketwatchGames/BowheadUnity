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

}
