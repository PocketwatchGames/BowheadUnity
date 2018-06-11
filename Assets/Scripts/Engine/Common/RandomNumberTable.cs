using UnityEngine;
using System.Collections.Generic;

public class RandomNumberTable : ScriptableObject {
	const int TABLE_SIZE = 1024*4;

	[SerializeField]
	float[] values;

	uint nextValue;

	void OnEnable() {
#if UNITY_EDITOR
		if (values == null) {
			values = new float[TABLE_SIZE];
			values[0] = 0;
			values[TABLE_SIZE-1] = 1;
			double step = 1.0f / TABLE_SIZE;
			double n = step;

			for (int i = 1; i < TABLE_SIZE-1; ++i) {
				values[i] = (float)n;
				n += step;
			}

			for (int k = 0; k < TABLE_SIZE; ++k) {
				var rand = new System.Random((int)((System.DateTime.Now.Ticks+k*1000) & 0xffffffffL));
				for (int i = 0; i < values.Length; ++i) {
					var x = rand.Next(1, values.Length);
					var o = (i+x) % TABLE_SIZE;
					var t = values[i];
					values[i] = values[o];
					values[o] = t;
				}
			}

			UnityEditor.EditorUtility.SetDirty(this);
		}
#endif
	}

	public float randomValue {
		get {
			uint i = nextValue++;
			return RandomFromIndex(i+seed);
		}
	}
	
	public float RandomFromIndex(uint index) {
		return values[index&(TABLE_SIZE-1)];
	}

	public uint seed {
		get;
		set;
	}

#if UNITY_EDITOR
	[UnityEditor.MenuItem("Assets/Create/Engine/Random Number Table")]
	static void CreateTableAsset() {
		Utils.CreateAsset<RandomNumberTable>();
	}
#endif
}
