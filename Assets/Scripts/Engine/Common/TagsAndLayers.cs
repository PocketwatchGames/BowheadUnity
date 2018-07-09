// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using System;


public static class Layers {
	public const int Default = 0;
	public const int DefaultMask = 1 << Default;

	public const int TransparentFX = 1;
	public const int TransparentFXMask = 1 << TransparentFX;

	public const int IgnoreRaycast = 2;
	public const int IgnoreRaycastMask = 1 << IgnoreRaycast;

	public const int Water = 4;
	public const int WaterMask = 1 << Water;

	public const int UI = 5;
	public const int UIMask = 1 << UI;

	public const int Terrain = 9;
	public const int TerrainMask = 1 << Terrain;

	public const int Pickups = 10;
	public const int PickupsMask = 1 << Pickups;

	public const int NoCollision = 11;
	public const int NoCollisionMask = 1 << NoCollision;

	public const int Trigger = 12;
	public const int TriggerMask = 1 << Trigger;

	public const int HitTest = 13;
	public const int HitTestMask = 1 << HitTest;

	public const int Trees = 14;
	public const int TreesMask = 1 << Trees;

	public const int CameraTraceMask = TerrainMask;

	public const int PawnCollisionMask = TerrainMask|TreesMask;

	public static int ToLayerMask(this ELayers layers) {
		int mask = 0;

		if ((layers & ELayers.Default) != 0) {
			mask |= DefaultMask;
		}
		if ((layers & ELayers.TransparentFX) != 0) {
			mask |= TransparentFXMask;
		}
		if ((layers & ELayers.Water) != 0) {
			mask |= WaterMask;
		}
		if ((layers & ELayers.UI) != 0) {
			mask |= UIMask;
		}
		if ((layers & ELayers.Terrain) != 0) {
			mask |= TerrainMask;
		}
		if ((layers & ELayers.Pickups) != 0) {
			mask |= PickupsMask;
		}
		if ((layers & ELayers.NoCollision) != 0) {
			mask |= NoCollisionMask;
		}
		if ((layers & ELayers.Trigger) != 0) {
			mask |= TriggerMask;
		}
		if ((layers & ELayers.HitTest) != 0) {
			mask |= HitTestMask;
		}
		if ((layers & ELayers.Trees) != 0) {
			return mask |= TreesMask;
		}

		return mask;
	}
}

[Flags]
public enum ELayers {
	Default = 0x1,
	TransparentFX = 0x2,
	Water = 0x4,
	UI = 0x8,
	Terrain = 0x10,
	Pickups = 0x20,
	NoCollision = 0x40,
	Trigger = 0x80,
	HitTest = 0x100,
	Trees = 0x200,
	PawnCollision = Terrain|Water|Trees
}

public static class Tags {
	public const string Untagged = "Untagged";
	public const string MainCamera = "MainCamera";
	public const string UICamera = "UICamera";
}
