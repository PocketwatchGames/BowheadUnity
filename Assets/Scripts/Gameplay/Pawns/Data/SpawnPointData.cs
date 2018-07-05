// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;

namespace Bowhead.Actors {

	enum ESpawnPointTeam {
		Monster,
		NPC
	};

	public sealed class SpawnPointData : StaticVersionedAsset {
		[SerializeField]
		ESpawnPointTeam _team;
		[SerializeField]
		EntityData _entityData;

	}

}
