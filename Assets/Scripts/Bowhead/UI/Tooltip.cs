// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

namespace Bowhead.Client.UI {

	public abstract class Tooltip : MonoBehaviourEx {
		static readonly Vector2 CLIP_SHIFT = new Vector2(-30, -30);
		
		Graphic _tooltip;
		bool _hover;
		float _delay;

		protected virtual void OnDisable() {
			_hover = false;
			if (_tooltip != null) {
				Utils.DestroyGameObject(_tooltip.gameObject);
				_tooltip = null;
			}
		}

		protected void Show(float delay) {
			if (DragDropWidget.active != null) {
				return;
			}

			if (!_hover) {
				_delay = delay;
				_hover = true;
				DisplayTooltip();
				if (_tooltip == null) {
					_hover = false;
				}
			}
		}

		protected void Hide() {
			if (_hover) {
				_hover = false;
				if (_tooltip != null) {
					Utils.DestroyGameObject(_tooltip.gameObject);
					_tooltip = null;
				}
			}
		}

		void PlaceAtCursor() {
			var r = _tooltip.rectTransform.GetRectInCanvas();

			var z = _tooltip.canvas.GetCanvasPixelRect();
			var m = _tooltip.canvas.ScreenToCanvas(GameManager.instance.mousePosition);

			r.x = m.x;
			r.y = m.y;

			if (r.xMax > z.xMax) {
				r.x = m.x - r.width + CLIP_SHIFT.x;
			}

			if (r.yMax > z.yMax) {
				r.y = m.y - r.height + CLIP_SHIFT.y;
			}

			_tooltip.rectTransform.SetRectInCanvas(r);
		}

		void DisplayTooltip() {
			_tooltip = CreateTooltip();
			if (_tooltip != null) {
				_tooltip.transform.SetParent(GameManager.instance.tooltipCanvas, false);

				StartCoroutine(CoPlaceAtCursor());
			}
		}
				
		IEnumerator CoPlaceAtCursor() {
			var nextFrame = Time.frameCount+1;
			while (nextFrame > Time.frameCount) {
				yield return null;
			}
						
			if (_delay > Time.unscaledDeltaTime) {
				yield return new WaitForSeconds(_delay - Time.unscaledDeltaTime);
			}

			while (_tooltip != null) {
				if (DragDropWidget.active != null) {
					Hide();
					break;
				}
				PlaceAtCursor();
				yield return null;
			}
		}

		protected abstract Graphic CreateTooltip();
	}
}
