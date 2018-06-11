// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;

public class MinMaxSlider : PropertyAttribute {


	public MinMaxSlider(float min, float max) {
		this.min = min;
		this.max = max;
	}

	public float min { get; set; }
	public float max { get; set; }

}
