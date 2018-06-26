// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEditor;
using Bowhead;
using Bowhead.Actors;
using Bowhead.Client;

namespace Bowhead.Editor {
	public static class MenuItems {
		[MenuItem("Assets/Create/Bowhead/Client Data")]
		static void CreateClientData() {
			Utils.CreateAsset<ClientData>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Physical Contact Matrix", priority = 250)]
		static void CreatePhysicalContactMatrix() {
			Utils.CreateAsset<PhysicalContactMatrix>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Physical Material Class", priority = 251)]
		static void CreatePhysicalMaterialClass() {
			Utils.CreateAsset<PhysicalMaterialClass>();
		}
	}
}