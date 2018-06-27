using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageIndicator : MonoBehaviourEx {

    float time, totalTime;
    Color color;
    public void Init(float t, float scale, Vector3 position, Color c) {
        transform.localScale = Vector3.one * scale;
        transform.position = position;
        time = totalTime = t;
        color = c;

        AddGC(GetComponent<MeshRenderer>().material);
        GetComponent<MeshRenderer>().material.color = color;

        Destroy(gameObject, time);
    }
    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        time -= Time.deltaTime;
        GetComponent<MeshRenderer>().material.color = color * Mathf.Max(0,time) / totalTime;
    }
}
