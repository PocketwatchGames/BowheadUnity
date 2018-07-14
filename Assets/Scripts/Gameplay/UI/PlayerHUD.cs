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

    private Player _target;


    void LateUpdate()
    {
        if (_target == null || _target.go == null)
        {
            Destroy(gameObject);
            return;
        }
        float h = _target.health / _target.maxHealth;
        float s = _target.stamina / _target.maxStamina;
        float w = _target.water / _target.maxWater;

        if (h < 1 || w < 1 || s < 1)
        {
            transform.position = Camera.main.WorldToScreenPoint(_target.headPosition());
            _health.value = h;
            _water.value = w;
            _stamina.value = s;
            _staminaFill.color = _target.recovering ? Color.yellow : Color.green;
        }
        _stamina.gameObject.SetActive(s < 1);
        _health.gameObject.SetActive(h < 1 && _target.stance == Player.Stance.Combat);
        _water.gameObject.SetActive(w < 1 && _target.stance == Player.Stance.Combat);
    }




    public void SetTarget(Player p)
    {
        _target = p;
    }
}
