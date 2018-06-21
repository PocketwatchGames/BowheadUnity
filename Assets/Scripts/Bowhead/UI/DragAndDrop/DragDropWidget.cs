// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Bowhead.Client.UI {
	public abstract class DragDropWidget : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {

		static Vector2 _itemStart;
		static Vector2 _mouseStart;
		static Transform _dragItem;
		static DragDropTarget _target;
		static DragDropWidget _active;
		static bool _cancel;

		Canvas _canvas;

		protected virtual void Awake() {}

		protected virtual void OnDragBegin(PointerEventData eventData) { }
		protected virtual void OnDragEnd(PointerEventData eventData) { }
		protected virtual void OnDragUpdate(PointerEventData eventData) { }

		public void OnBeginDrag(PointerEventData eventData) {
			if (_canvas == null) {
				_canvas = GetComponentInParent<Canvas>();
			}

			_cancel = false;
			_active = this;
			_dragItem = CreateDragItem();

			_dragItem.SetParent(GameManager.instance.tooltipCanvas, false);

			_itemStart = transform.position;
			_mouseStart = eventData.position;

			_dragItem.position = _itemStart;

			OnDragBegin(eventData);
		}

		public void OnDrag(PointerEventData eventData) {
			if (_dragItem != null) {
				if (_cancel || InputManager.GetKeyState(InputKey.Escape).pressed) {
					if (_target != null) {
						_target.OnHover(null);
						_target = null;
					}
					OnEndDrag(eventData);
					return;
				}

				var mpos = eventData.position;
				_dragItem.position = _itemStart + (mpos - _mouseStart);

				OnDragUpdate(eventData);
				UpdateHover(eventData);
			}
		}

		public void OnEndDrag(PointerEventData eventData) {
			if (_dragItem != null) {
				DestroyDragItem(_dragItem);
				_dragItem = null;
				_active = null;
				if (_target != null) {
					_target.OnHover(null);
					_target = null;
				}
				OnDragEnd(eventData);
			}
		}

		void UpdateHover(PointerEventData eventData) {
			var hovered = eventData.hovered;
			
			for (int i = hovered.Count-1; i >= 0; --i) {
				var go = hovered[i];
				var dd = go.GetComponent<DragDropTarget>();

				if (dd != null) {
					if (dd != _target) {
						if (_target != null) {
							_target.OnHover(null);
						}
						_target = dd;
						dd.OnHover(this);
					}

					return;
				}
			}

			if (_target != null) {
				_target.OnHover(null);
				_target = null;
			}
		}

		protected abstract Transform CreateDragItem();
		protected abstract void DestroyDragItem(Transform t);

		public static void CancelDrag() {
			_cancel = true;
		}

		public Canvas canvas {
			get {
				return _canvas;
			}
		}

		public DragDropTarget target {
			get {
				return _target;
			}
		}

		static public DragDropWidget active {
			get {
				return _active;
			}
		}
	}
}