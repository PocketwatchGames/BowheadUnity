// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System;

public sealed class PointerClickHandler : MonoBehaviour, IPointerClickHandler, IPointerUpHandler, IPointerDownHandler {

	[Serializable]
	public class PointerClick : UnityEvent {}

	public PointerClick onLeftClick;
	public PointerClick onRightClick;
	public PointerClick onMiddleClick;

	void Awake() {
		if (onLeftClick == null) {
			onLeftClick = new PointerClick();
		}
		if (onRightClick == null) {
			onRightClick = new PointerClick();
		}
		if (onMiddleClick == null) {
			onMiddleClick = new PointerClick();
		}
	}

	public void OnPointerClick(PointerEventData eventData) {
		if (eventData.button == PointerEventData.InputButton.Left) {
			onLeftClick.Invoke();
		} else if (eventData.button == PointerEventData.InputButton.Right) {
			onRightClick.Invoke();
		} else if (eventData.button == PointerEventData.InputButton.Middle) {
			onMiddleClick.Invoke();
		}
	}

	public void OnPointerDown(PointerEventData eventData) {}
	public void OnPointerUp(PointerEventData eventData) {}
}
