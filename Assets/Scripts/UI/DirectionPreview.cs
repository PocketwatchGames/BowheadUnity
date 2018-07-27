using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead.Actors;

public class DirectionPreview : MonoBehaviour {

    private Player _target;

    // Update is called once per frame
    void LateUpdate () {
        if (_target != null) {
            transform.position = _target.footPosition();
            transform.rotation = Quaternion.Euler(90, _target.yaw*Mathf.Rad2Deg, 0);
        }

    }

    public void SetTarget(Player p)
    {
        _target = p;
    }
}
