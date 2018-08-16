using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead.Actors;

namespace Bowhead.Client.UI {
	public class InventoryPanel : MonoBehaviour {

		public InventorySlot inventorySlotPrefab;
		private int _playerIndex = 1;
		public InventorySlot[] slots;

		private int selectSlot;
		private Pawn _pawn;
		private int _startSlot;
		private int _slotCount;

		// Use this for initialization
		void Start() {

		}

		public void Init(Pawn p, int start, int count) {
			_pawn = p;
			_startSlot = start;
			_slotCount = count;

			slots = new InventorySlot[_slotCount];
			for (int i=0;i< _slotCount; i++) {
				var s = GameObject.Instantiate<InventorySlot>(inventorySlotPrefab, transform);
				slots[i] = s;
			}

			OnInventoryChange();

		}


		public void OnInventoryChange() {
			for (int i = 0; i < slots.Length; i++) {
				slots[i].SetItem(_pawn.GetInventorySlot(i+_startSlot));
			}
		}

	}
}
