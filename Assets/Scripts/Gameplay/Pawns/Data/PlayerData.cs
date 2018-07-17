﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

    [CreateAssetMenu(menuName = "EntityData/Player")]
    public class PlayerData : PawnData<PlayerData> {

		[Header("Player"), ClassDropdown(typeof(Player)), SerializeField]
		string _playerClass;
		public GameObject_WRef prefab;
        public float temperatureSleepMinimum;
        public float temperatureSleepMaximum;
        public float fallDamageSpeed;
		public float fallSpeedLoudness;
		public float dropTime;
		public float tradePartnerCancelDistance;
		public int[] weightClassItemCount;

		public Type playerClass { get; private set; }

		public override void OnAfterDeserialize() {
			base.OnAfterDeserialize();

			if (string.IsNullOrEmpty(_playerClass)) {
				playerClass = null;
			} else {
				playerClass = Type.GetType(_playerClass);
			}
		}

		public T Spawn<T>(World world, Vector3 pos, float yaw, Actor instigator, Actor owner, Team team) where T : Player {
			var player = (T)world.Spawn(playerClass, null, default(SpawnParameters));
			player.Spawn(this, pos, yaw, instigator, owner, team);
			return player;
		}
	};

}