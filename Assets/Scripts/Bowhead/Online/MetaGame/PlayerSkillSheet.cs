// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections.ObjectModel;

namespace Bowhead.Online.MetaGame {
	public static class Constants {
		public const int NUM_SKILL_SHEETS = 1;
		public const int NUM_DEITIES = 3;
	}

	public interface DeitySkillSheet {
		int xp { get; }
	}

	public interface PlayerSkillSheet {
		int xp { get; }
		ReadOnlyCollection<DeitySkillSheet> deities { get; }
	}

	public interface PlayerSkills {
		int skver { get; }
		ReadOnlyCollection<PlayerSkillSheet> skillSheets { get; }
	}
}