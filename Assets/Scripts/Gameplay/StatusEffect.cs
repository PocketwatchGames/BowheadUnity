﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead.Actors;

namespace Bowhead {

	public class StatusEffect {

		#region getdata

		new public StatusEffectData data {
			get;
			private set;
		}
		#endregion

		public float time;
		public float totalTime;

        public static StatusEffect Create(StatusEffectData d, float t)
        {
            var e = new StatusEffect();
            e.Init(d, t);
            return e;
        }

		private void Init(StatusEffectData data, float t) {            
            this.data = data;
            time = totalTime = t;
		}

        public void Apply(Pawn owner)
        {
            if (data.maxStaminaBonus > 0) {
                owner.maxStamina += data.maxStaminaBonus;
                owner.stamina += data.maxStaminaBonus;
            }
            if (data.maxHealthBonus > 0) {
                owner.maxHealth += data.maxHealthBonus;
                owner.health += data.maxHealthBonus;
            }
        }
        public void Tick(float dt, Pawn owner) {
            if (time > 0)
            {
                if (data.healthPerSecond > 0) {
                    owner.health = Mathf.Min(owner.maxHealth, owner.health + data.healthPerSecond * dt);
                } else if (data.healthPerSecond < 0) {
                    owner.Damage(data.healthPerSecond * dt);
                }
                if (data.staminaPerSecond > 0) {
                    owner.stamina = Mathf.Min(owner.maxStamina, owner.stamina + data.staminaPerSecond * dt);
                } else if (data.staminaPerSecond < 0) {
                    owner.stamina = Mathf.Max(0, Mathf.Max(owner.stamina, owner.stamina + data.staminaPerSecond * dt));
                }
                time -= dt;
                if (time <= 0) {
                    if (data.maxStaminaBonus > 0) {
                        owner.maxStamina -= data.maxStaminaBonus;
                        owner.stamina = Mathf.Max(owner.stamina, owner.maxStamina);
                    }
                    if (data.maxHealthBonus > 0) {
                        owner.maxHealth -= data.maxHealthBonus;
                        owner.health = Mathf.Max(owner.health, owner.maxHealth);
                    }
                }
            }
        }
	}

}
