using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageHUD : MonoBehaviour {

    float totalTime;
    float time;
    Vector3 startPos;
    UnityEngine.UI.Text text;
	Color startColor;
    public void Init(float d, float t, Bowhead.Actors.Pawn target)
    {
        totalTime = time = t;
        text = GetComponent<UnityEngine.UI.Text>();
        text.fontSize = (int)Mathf.Clamp(Mathf.Sqrt(d/5) * 20, 20, 40);
        text.color = Color.red;
        text.text = Mathf.CeilToInt(d).ToString();
		startPos = target.headPosition();
		transform.position = Camera.main.WorldToScreenPoint(startPos);
		startColor = Color.red;

		Destroy(gameObject, time);
    }

    public void Init(string s, int size, float t, Bowhead.Actors.Pawn target)
    {
        totalTime = time = t;
        text = GetComponent<UnityEngine.UI.Text>();
        text.fontSize = size;
        text.color = Color.red;
        text.text = s;
		startPos = target.headPosition();
		transform.position = Camera.main.WorldToScreenPoint(startPos);

		Destroy(gameObject, time);
    }


    // Update is called once per frame
    void Update () {
        time -= Time.deltaTime;
        if (time <= 0) {
            return;
        }
        float posT = 1 - Mathf.Pow(time / totalTime, 3);
        float t = (totalTime - time) / totalTime;
        transform.position = Camera.main.WorldToScreenPoint(startPos) + new Vector3(0,posT  * 100, 0);
        text.color = new Color(startColor.r,startColor.g,startColor.b,1.0f-t);
	}
}
