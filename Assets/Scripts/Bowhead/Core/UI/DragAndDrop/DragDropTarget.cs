// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Bowhead.Client.UI {
	public abstract class DragDropTarget : MonoBehaviour, IDropHandler {
		DragDropWidget _dropped;

		protected virtual void Awake() { }
		protected abstract bool Hover(DragDropWidget item);
		protected abstract void Drop(DragDropWidget item);

		public void OnHover(DragDropWidget item) {
			var canDrop = Hover(item);
			_dropped = canDrop ? item : null;
		}
		
		public void OnDrop(PointerEventData eventData) {
			if (_dropped != null) {
				Drop(_dropped);
				_dropped = null;
			}
		}
	}
}