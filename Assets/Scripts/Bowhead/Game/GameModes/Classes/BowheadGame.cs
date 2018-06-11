// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using Bowhead.Server.Actors;
using Bowhead.Actors;

namespace Bowhead.Server {

	public abstract class BowheadGame<T> : GameMode<T> where T: GameState<T>{
		public BowheadGame(ServerWorld world) : base(world) { }
	}

	public class BowheadGame : BowheadGame<GSBowheadGame> {
		public BowheadGame(ServerWorld world) : base(world) { }
	}

	public class GSBowheadGame : GameState<GSBowheadGame> {	}
}