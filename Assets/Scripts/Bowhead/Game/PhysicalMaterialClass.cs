// Copyright (c) 2018 Pocketwatch Games LLC.

namespace Bowhead {
	public class PhysicalMaterialClass : StaticVersionedAsset {
		const int VERSION = 1;
		public bool defaultContact;

#if UNITY_EDITOR
		protected override void InitVersion() {
			base.InitVersion();

			if (version < 1) {
				defaultContact = true;
			}

			version = VERSION;
		}
#endif
	}
}