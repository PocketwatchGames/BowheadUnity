using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageHUD : MonoBehaviour {

    float totalTime;
    float time;
    Vector3 startPos;
    UnityEngine.UI.Text text;
    public void Init(float d, float t, Bowhead.Actors.Pawn target)
    {
        totalTime = time = t;
        text = GetComponent<UnityEngine.UI.Text>();
        text.fontSize = (int)Mathf.Clamp(Mathf.Sqrt(d) * 5, 5, 40);
        text.color = Color.red;
        text.text = Mathf.CeilToInt(d).ToString();
        transform.position = startPos = Camera.main.WorldToScreenPoint(target.headPosition());

        Destroy(gameObject, time);
    }

    public void Init(string s, int size, float t, Bowhead.Actors.Pawn target)
    {
        totalTime = time = t;
        text = GetComponent<UnityEngine.UI.Text>();
        text.fontSize = size;
        text.color = Color.red;
        text.text = s;
        transform.position = startPos = Camera.main.WorldToScreenPoint(target.headPosition());

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
        transform.position = startPos + new Vector3(0,posT  * 40, 0);
        text.color = Color.Lerp(Color.red, new Color(1, 0, 0, 0), t);
	}
}
