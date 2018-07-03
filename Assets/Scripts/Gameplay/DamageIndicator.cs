using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageIndicator : MonoBehaviourEx {

    float time, totalTime;
    Color color;
    public void Init(float t, float scale) {
        transform.localScale = Vector3.one * scale;
        time = totalTime = t;

        AddGC(GetComponent<MeshRenderer>().material);
		color = Color.white * 0.25f;
		gameObject.SetActive(true);

	}

	public void Tick(Vector3 position, bool hit) {
		if (hit) {
			color = Color.red * 0.75f;
		}
		GetComponent<MeshRenderer>().material.color = color;
		transform.position = position;
	}

    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        time -= Time.deltaTime;
		if (time <= 0) {
			gameObject.SetActive(false);
		}
	}
}
