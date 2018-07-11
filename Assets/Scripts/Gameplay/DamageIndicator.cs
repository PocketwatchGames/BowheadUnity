using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageIndicator : MonoBehaviourEx {

    Color color;
    public void Init(float t, float scale) {
        transform.localScale = Vector3.one * scale;

        AddGC(GetComponent<MeshRenderer>().material);
		color = Color.white * 0.25f;
		Destroy(gameObject, t);
	}

	public void Tick(Vector3 position, bool hit) {
		if (hit) {
			color = Color.red * 0.75f;
		}
		GetComponent<MeshRenderer>().material.color = color;
		transform.position = position;
	}
}
