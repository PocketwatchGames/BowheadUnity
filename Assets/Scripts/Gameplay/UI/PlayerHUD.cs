using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player = Bowhead.Actors.Player;

public class PlayerHUD : MonoBehaviour {

    [SerializeField]
    UnityEngine.UI.Slider _health;
    [SerializeField]
    UnityEngine.UI.Slider _stamina;
    [SerializeField]
    UnityEngine.UI.Image _staminaFill;
    [SerializeField]
    UnityEngine.UI.Slider _water;
	[SerializeField]
	UnityEngine.UI.Text _weaponMultiplier;

    private Player _target;
	private float _changeTimerWater, _changeTimerHealth;
	float totalTime;

    void LateUpdate()
    {
        if (_target == null || _target.go == null)
        {
            Destroy(gameObject);
            return;
        }

		var mountOrTarget = _target.mount ?? _target;

        float h = _target.health / _target.maxHealth;
        float s = mountOrTarget.stamina / mountOrTarget.maxStamina;
        float w = _target.water / _target.maxWater;
		float wm = 0;
		for (int i=0;i<Player.MaxInventorySize;i++) {
			var weapon = _target.GetInventorySlot(i) as Bowhead.Weapon;
			if (weapon != null && weapon.chargeTime > 0) {
				wm = Mathf.Max(wm, weapon.GetChargeMultiplier(_target, weapon.chargeTime));
			}
		}
		if (wm <= 1) {
			_weaponMultiplier.gameObject.SetActive(false);
		}
		else {
			_weaponMultiplier.fontSize = 20 + (int)(2 * wm);
			_weaponMultiplier.gameObject.SetActive(true);
			_weaponMultiplier.text = "x" + Mathf.FloorToInt(wm);
		}

		if (_health.value != h) {
			_changeTimerHealth = 3;
		}
		if (_water.value != w) {
			_changeTimerWater = 3;
		}

        if (h < 1 || w < 1 || s < 1 || _changeTimerHealth > 0 || _changeTimerWater > 0 || wm > 1)
        {
            transform.position = Camera.main.WorldToScreenPoint(_target.headPosition());
            _health.value = h;
            _water.value = w;
            _stamina.value = s;
            _staminaFill.color = mountOrTarget.recovering ? Color.yellow : Color.green;
        }
        _stamina.gameObject.SetActive(s < 1);
        _health.gameObject.SetActive(_changeTimerHealth > 0 || (h < 1 && _target.stance == Player.Stance.Combat));
        _water.gameObject.SetActive(_changeTimerWater > 0 || (w < 1 && _target.stance == Player.Stance.Combat));

		_changeTimerHealth = Mathf.Max(0, _changeTimerHealth - Time.deltaTime);
		_changeTimerWater = Mathf.Max(0, _changeTimerWater - Time.deltaTime);

		if (_target.worldStreaming.loadedChunkCount < _target.worldStreaming.totalChunkCount) {
			totalTime += Time.deltaTime;
		}
	}

	bool showDebug;

	void OnGUI() {

		if (_target != null) {
			if (Event.current.type == EventType.KeyDown) {
				if (Event.current.keyCode == KeyCode.F3) {
					showDebug = !showDebug;
				}
			}

			if (showDebug) {
				var streaming = _target.world.worldStreaming;

				GUI.contentColor = Color.gray;
				GUI.Box(new Rect(0, 0, 800, 280), string.Empty);
				GUI.contentColor = Color.yellow;
				GUILayout.BeginHorizontal();
				GUILayout.Space(10);
				GUILayout.BeginVertical();
				GUILayout.Label("WorldStreaming");
				GUILayout.Space(10);
				GUILayout.Label("Frame:");
				GUILayout.Label("Jobs: Submitted: " + streaming.countersThisFrame.submittedJobs + " | Pending: " + streaming.countersThisFrame.pendingJobs + " | Completed: " + streaming.countersThisFrame.completedJobs);
				GUILayout.Label("Chunks: Built:" + streaming.countersThisFrame.chunksGenerated + " | Copied: " + streaming.countersThisFrame.chunksCopiedToScene + "/" + (streaming.countersThisFrame.chunkSceneCopyTime/Utils.TimestampFrequencyPerMicro) + "us");

				var div = Utils.TimestampFrequencyPerMicro;
				var div2 = div * (streaming.countersThisFrame.chunksGenerated > 0 ? streaming.countersThisFrame.chunksGenerated : 1);

				{
					var timing = streaming.countersThisFrame.chunkTiming;
					GUILayout.Label("Chunk (Total us): Latency: " + timing.latency/div + " | GenVoxels: " + timing.voxelTime/div + " | GenVerts: " + timing.verts1/div + " | SmoothVerts: " + timing.verts2/div);
					GUILayout.Label("Chunk (Avg us)  : Latency: " + timing.latency/div2 + " | GenVoxels: " + timing.voxelTime/div2 + " | GenVerts: " + timing.verts1/div2 + " | SmoothVerts: " + timing.verts2/div2);
				}

				GUILayout.Space(10);
				div2 = div * (streaming.countersTotal.chunksGenerated > 0 ? streaming.countersTotal.chunksGenerated : 1);

				{
					var timing = streaming.countersTotal.chunkTiming;
					GUILayout.Label("Total:");
					GUILayout.Label("Time (seconds): " + streaming.countersTotal.totalTime/Utils.TimestampFrequencyPerMilli/1000f + " Generated: " + streaming.countersTotal.chunksGenerated + " | CompletedJobs: " + streaming.countersTotal.completedJobs);
					GUILayout.Label("Chunk (Total us): Latency: " + timing.latency/div + " | GenVoxels: " + timing.voxelTime/div + " | GenVerts: " + timing.verts1/div + " | SmoothVerts: " + timing.verts2/div + " | Copy: " + streaming.countersTotal.copyTime / div);
					GUILayout.Label("Chunk (Avg us)  : Latency: " + timing.latency/div2 + " | GenVoxels: " + timing.voxelTime/div2 + " | GenVerts: " + timing.verts1/div2 + " | SmoothVerts: " + timing.verts2/div2 + " | Copy: " + streaming.countersTotal.copyTime / div2);
				}

				GUILayout.EndVertical();
				GUILayout.EndHorizontal();
			}
		}
	}


	public void SetTarget(Player p)
    {
        _target = p;
    }
}
