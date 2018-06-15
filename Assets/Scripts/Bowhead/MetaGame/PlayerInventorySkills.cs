// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Bowhead.Actors.Spells;
#if !DEDICATED_SERVER
using Bowhead.Client.Actors;
#endif
#if (UNITY_EDITOR && STEAM_API) || BACKEND_SERVER
using Bowhead.Online.SteamWebAPI;
#endif

namespace Bowhead.MetaGame {

	public sealed class PlayerInventorySkills : System.IDisposable {
		public const int NUM_RUNES_PER_DEITY = 1;
		public const int NUM_GEMS_PER_RUNE = 6;

		// skill versions must always increase
		// to force player gear reset, increment the skills version by SKILLS_FORCE_RESET_VERSION
		const int SKILLS_FORCE_RESET_DELTA = 100;
		const int FIRST_VERSION = 100;
		const int SEVERED_HEAD = FIRST_VERSION;
		const int SEVERED_HEAD_2 = SEVERED_HEAD+SKILLS_FORCE_RESET_DELTA;
		const int CURRENT_VERSION = SEVERED_HEAD_2;

		public const int SKILLS_VERSION = CURRENT_VERSION;
		public const int SKILLS_FORCE_RESET_VERSION = SKILLS_VERSION - SKILLS_FORCE_RESET_DELTA;
		
		public enum API {
			Client,
			Server
		}

		public class InventoryItem {
			public InventoryItem(ulong iid, int id, InventoryItemClass itemClass, int ilvl, int count) {
				this.iid = iid;
				this.id = id;
				this.ilvl = ilvl;
				this.count = count;
				this.itemClass = itemClass;
			}

			public readonly ulong iid;
			public readonly int id;
			public readonly int ilvl;
			public readonly int count;
			public readonly InventoryItemClass itemClass;
			public bool socketed;
		}

		public class GemSlot {
			public int id;
			public InventoryItem item;
		}

		public class RuneSlot {
			public RuneSlot() {
				gems = new GemSlot[NUM_GEMS_PER_RUNE];
				for (int i = 0; i < gems.Length; ++i) {
					gems[i] = new GemSlot();
				}
			}

			public int id;
			public InventoryItem item;
			public GemSlot[] gems;
		}

		public struct StatItem {
			public StatItem(InventoryGearItemClass item, int ilvl, int original_ilvl, float value) {
				this.item = item;
				this.ilvl = ilvl;
				this.original_ilvl = original_ilvl;
				this.value = value;
			}

			public readonly InventoryGearItemClass item;
			public readonly int ilvl;
			public readonly int original_ilvl;
			public readonly float value;
		}

		public sealed class StatItems {
			List<StatItem> _items;

			public StatItems(ItemStatClass itemStatClass, List<StatItem> items, float total) {
				this.itemStatClass = itemStatClass;
				this.total = total;

				_items = items;
				this.items = new ReadOnlyCollection<StatItem>(_items);
			}

			public readonly ItemStatClass itemStatClass;
			public readonly float total;
			public readonly ReadOnlyCollection<StatItem> items;
		}

		public sealed class ItemStats {
			public readonly DictionaryList<ItemStatClass, StatItems> stats;

			public ItemStats(RuneSlot[] runes, int min_ilvl, int max_ilvl) {
				stats = new DictionaryList<ItemStatClass, StatItems>();

				for (int i = 0; i < runes.Length; ++i) {
					var rune = runes[i];
					if (rune.item != null) {
						TryAdd((InventoryGearItemClass)rune.item.itemClass, runes, min_ilvl, max_ilvl);
					}
					if (rune.gems != null) {
						for (int k = 0; k < rune.gems.Length; ++k) {
							var gem = rune.gems[k];
							if (gem.item != null) {
								TryAdd((InventoryGearItemClass)gem.item.itemClass, runes, min_ilvl, max_ilvl);
							}
						}
					}
				}
			}

			void TryAdd(InventoryGearItemClass item, RuneSlot[] runes, int min_ilvl, int max_ilvl) {
				var stats = item.stats;
				if (stats != null) {
					for (int i = 0; i < stats.Length; ++i) {
						var stat = stats[i];
						TryAdd(stat.itemStatClass, runes, min_ilvl, max_ilvl);
					}
				}
			}

			void TryAdd(ItemStatClass itemStatClass, RuneSlot[] runes, int min_ilvl, int max_ilvl) {
				if (stats.ContainsKey(itemStatClass)) {
					return;
				}

				var total = 0f;
				List<StatItem> items = new List<StatItem>();

				for (int i = 0; i < runes.Length; ++i) {
					var rune = runes[i];
					if (rune.item != null) {
						total = TryAdd(itemStatClass, (InventoryGearItemClass)rune.item.itemClass, Mathf.Clamp(rune.item.ilvl, min_ilvl, max_ilvl), rune.item.ilvl, items, total);
					}
					for (int k = 0; k < rune.gems.Length; ++k) {
						var gem = rune.gems[k];
						if (gem.item != null) {
							total = TryAdd(itemStatClass, (InventoryGearItemClass)gem.item.itemClass, Mathf.Clamp(gem.item.ilvl, min_ilvl, max_ilvl), gem.item.ilvl, items, total);
						}
					}
				}

				if (items.Count > 0) {
					stats.Add(itemStatClass, new StatItems(itemStatClass, items, total));
				}
			}

			float TryAdd(ItemStatClass itemStatClass, InventoryGearItemClass item, int ilvl, int original_ilvl, List<StatItem> items, float total) {

				var add = 0f;

				if (item.stats != null) {
					for (int i = 0; i < item.stats.Length; ++i) {
						var stat = item.stats[i];
						if (stat.itemStatClass == itemStatClass) {
							add += stat.GetScaledValue(ilvl);
						}
					}
				}

				if (add > 0f) {
					items.Add(new StatItem(item, ilvl, original_ilvl, add));
					total += add;
				}

				return total;	
			}
		}

		const string FILENAME = "debug_inventory.xml";
		const float FLUSH_RATE = 30f;

		public class UnlockedAbility {

			public UnlockedAbility(AbilityClass abilityClass) {
				this.abilityClass = abilityClass;
				ilvls = new Dictionary<int, InventoryGrantAbilityItemClass>();
			}

			public readonly AbilityClass abilityClass;
			public readonly Dictionary<int, InventoryGrantAbilityItemClass> ilvls;
		}

		DictionaryList<ulong, InventoryItem> _inventory;
		Dictionary<int, List<InventoryItem>> _socketed;
		Dictionary<int, List<InventoryItem>> _unsocketed;
		Dictionary<AbilityClass, UnlockedAbility> _unlockedAbilities;
		RuneSlot[] _runes;
		ItemStats _stats;
		
		readonly ulong _uuid;
		readonly API _api;
		Coroutine _coLoadInventory;
		float _nextFlush;
		bool _disposed;
		bool _xpDirty;
		bool _welcomeDropDirty;
		bool _welcomeDrop;

		public PlayerInventorySkills(ulong uuid, API api) {
			_uuid = uuid;
			_api = api;
			_nextFlush = FLUSH_RATE;
			_inventory = new DictionaryList<ulong, InventoryItem>();
			_unlockedAbilities = new Dictionary<AbilityClass, UnlockedAbility>();

			//var spellLibrary = GameManager.instance.staticData.coopSpellLibrary;

			//for (int i = 0; i < spellLibrary.deities.Length; ++i) {
			//	var deity = spellLibrary.deities[i];
			//	if (deity != null) {
			//		_deityStats.Add(deity, new DeityStats(deity, i));
			//	}
			//}
			
			GameManager.instance.StartCoroutine(CoLoadInventory());
		}

		IEnumerator CoLoadInventory() {
			_runes = new RuneSlot[NUM_RUNES_PER_DEITY];

			for (int i = 0; i < _runes.Length; ++i) {
				_runes[i] = new RuneSlot();
			}

			_stats = null;
			_inventory.Clear();
			_unlockedAbilities.Clear();
			_welcomeDropDirty = false;

#if (STEAM_API && UNITY_EDITOR) || BACKEND_SERVER
			if (_api == API.Server) {
				int skver = 0;
				{
					var req = SteamGetUserStatsForGame.Execute(_uuid);
					yield return req.Wait();
					var stats = req.response.stats;
					if (stats != null) {
						skver = ParseStatValue("skver", stats);

						if (skver >= SKILLS_VERSION) {
							// parse runes and gems of each deity
							xp = ParseStatValue("ss[0].xp", stats);

							for (int i = 0; i < _deityStats.Values.Count; ++i) {
								var dstat = _deityStats.Values[i];
								var baseStr = string.Format("ss[0].d[{0}]", dstat.statIndex);
								dstat.xp = ParseStatValue(baseStr + ".xp", stats);
							}
						} else {
							xp = 0;
							_xpDirty = true;
							for (int i = 0; i < _deityStats.Values.Count; ++i) {
								var dstat = _deityStats.Values[i];
								dstat.xp = 0;
								dstat.dirty = true;
							}
						}

						_welcomeDrop = ParseStatValue("welcome_drop", stats) != 0;
					}

					if (skver == 0) {
						skver = SKILLS_VERSION-1; // force version reset.
					}
				}
				{
					var req = SteamGetUserInventory.Execute(_uuid);
					yield return req.Wait();

					var itemLibrary = GameManager.instance.staticData.inventoryItemLibrary;

					if (skver > SKILLS_FORCE_RESET_VERSION) {
						if (skver < SKILLS_VERSION) {
							yield return SteamResetUserSkillVersion();
						}

						if (req.response != null) {
							// parse inventory
							for (int i = 0; i < req.response.Length; ++i) {
								var resItem = req.response[i];
								InventoryItemClass itemClass;
								int ilvl;

								var id = int.Parse(resItem.itemdefid);

								if (itemLibrary.TryGetItem(id, out itemClass, out ilvl)) {
									if (!(itemClass.hidden || itemClass.deprecated)) {
										var iid = ulong.Parse(resItem.itemid);
										_inventory.Add(iid, new InventoryItem(iid, id, itemClass, ilvl, resItem.quantity));
									}
								}/* else {
									SteamAsyncRemoveInventoryItem(ulong.Parse(resItem.itemid), resItem.quantity);
								}*/
							}
						}
					} else {
						yield return SteamAsyncResetUserInventory(req.response);
					}
				}
			}
#endif
#if STEAM_API && (UNITY_EDITOR || !DEDICATED_SERVER)
			if (_api == API.Client) {
				yield return CoLoadClientInventory();
			}
#elif !SHIP
			{
				int xp;
				XMLInventory.Load(string.Format("/{0}_{1}", _uuid, FILENAME), this, _inventory, out xp);
				this.xp = xp;
				_welcomeDropDirty = false;
			}
#endif

			// autoGrant items:
			// NOTE: autogrants are also set as steam promo items but those items don't always get delivered
			// to clients for some reason, so we need to make sure they are in the inventory here.
			{
				ulong id = InventoryItemLibrary.FIRST_AUTOGRANT_ID;
				var itemLibrary = GameManager.instance.staticData.inventoryItemLibrary;

				foreach (var dropItem in itemLibrary) {
					var invItem = dropItem as InventoryItemClass;

					var autoGrant = (invItem != null) && invItem.autoGrant && !(invItem.deprecated || invItem.hidden);

//#if UNITY_EDITOR
//					if (!autoGrant && (invItem != null) && (GameManager.instance.PIEAutoGrant != null) && GameManager.instance.PIEAutoGrant.Contains(invItem)) {
//						autoGrant = true;
//					}
//#endif

					if (autoGrant) {
						var range = invItem.generatedItems.ilvlRange;

						foreach (var pair in invItem.ilvl2id) {
							if ((pair.Key >= range.x) || (pair.Key <= range.y)) {
								var grant = true;
								foreach (var ownItem in _inventory.Values) {
									if ((ownItem.itemClass == invItem) && (ownItem.ilvl == pair.Key)) {
										grant = false;
										break;
									}
								}
								if (grant) {
									_inventory.Add(id, new InventoryItem(id, pair.Value, invItem, pair.Key, 1));
									++id;
								}
							}
						}
					}
				}
			}

			UnsocketAll();

			// hash unlocked ability ilvl's
			{
				foreach (var item in _inventory.Values) {
					var unlockItem = item.itemClass as InventoryGrantAbilityItemClass;
					if (unlockItem != null) {
						UnlockedAbility ability;
						if (!_unlockedAbilities.TryGetValue(unlockItem.abilityClass, out ability)) {
							ability = new UnlockedAbility(unlockItem.abilityClass);
							_unlockedAbilities[unlockItem.abilityClass] = ability;
						}

						InventoryGrantAbilityItemClass unlockedBy;

						if (ability.ilvls.TryGetValue(item.ilvl, out unlockedBy)) {
							if (unlockedBy != unlockItem) {
								Debug.LogError("Ability class " + ability.abilityClass.name + " at ilvl " + item.ilvl + " is already granted by " + unlockedBy.name + " but is also being granted by " + unlockItem.name);
							}
						} else {
							ability.ilvls.Add(item.ilvl, unlockItem);
						}
					}
				}
			}

			UpdateLevelXP();

			ready = true;
			yield break;
		}

#if STEAM_API && !DEDICATED_SERVER
		Online.MetaGame.ImmutableInventory _clientOnlineInventory;
		Online.MetaGame.PlayerSkills _clientOnlineSkills;

		void OnOnlineLocalPlayerGetSkills(Online.MetaGame.PlayerSkills skills) {
			_clientOnlineSkills = skills;
		}

		void OnOnlineLocalPlayerGetInventory(Online.MetaGame.ImmutableInventory inventory) {
			_clientOnlineInventory = inventory;
		}

		bool _loading;
		IEnumerator CoLoadClientInventory() {

			_loading = true;

			var localPlayer = GameManager.instance.onlineLocalPlayer;
			localPlayer.AsyncGetSkills(OnOnlineLocalPlayerGetSkills);
			localPlayer.AsyncGetInventory(OnOnlineLocalPlayerGetInventory);

			while ((_clientOnlineSkills == null) || (_clientOnlineInventory == null)) {
				yield return null;
			}

			_inventory = new DictionaryList<ulong, InventoryItem>();

			if (_clientOnlineSkills.skver > SKILLS_FORCE_RESET_VERSION) {
				for (int i = 0; i < _clientOnlineInventory.items.Count; ++i) {
					var item = _clientOnlineInventory.items[i];
					if (!(item.itemClass.deprecated || item.itemClass.hidden)) {
						_inventory.Add(item.iid, new InventoryItem(item.iid, item.id, item.itemClass, item.ilvl, item.count));
					}
				}

				var skillSheet = _clientOnlineSkills.skillSheets[0];
				xp = skillSheet.xp;

				for (int i = 0; i < _deityStats.Values.Count; ++i) {
					var dstat = _deityStats.Values[i];
					if (dstat.statIndex < skillSheet.deities.Count) {
						var skd = skillSheet.deities[dstat.statIndex];
						dstat.xp = skd.xp;
					}
				}
			} else {
				xp = 0;
				for (int i = 0; i < _deityStats.Values.Count; ++i) {
					var dstat = _deityStats.Values[i];
					dstat.xp = 0;
				}
			}

			_clientOnlineInventory = null;
			_clientOnlineSkills = null;

			_loading = false;
		}
#endif

#if (STEAM_API && UNITY_EDITOR) || BACKEND_SERVER
		static int ParseStatValue(string key, SteamGetUserStatsForGame.Stat[] stats) {
			for (int i = 0; i < stats.Length; ++i) {
				var stat = stats[i];
				if (stat.name == key) {
					return int.Parse(stat.value);
				}
			}
			return 0;
		}

		void SteamAsyncRemoveInventoryItem(ulong itemid, int count) {
			GameManager.instance.EnqueueSteamWebCommand(() => {
				var req = SteamInventoryTradeSetUnowned.Execute(_uuid, itemid, count);
				return req.Wait();
			});
		}

		bool resetPending;

		IEnumerator SteamAsyncResetUserInventory(SteamGetUserInventory.Item[] items) {
			resetPending = true;

			if (items != null) {
				var itemLibrary = GameManager.instance.staticData.inventoryItemLibrary;

				for (int i = 0; i < items.Length; ++i) {
					var iitem = items[i];
		
					InventoryItemClass itemClass;
					int ilvl;

					var id = int.Parse(iitem.itemdefid);

					if (itemLibrary.TryGetItem(id, out itemClass, out ilvl)) {
						if (itemClass.autoGrant) {
							continue; // don't wipe autogrant items.
						}
					}

					SteamAsyncRemoveInventoryItem(ulong.Parse(iitem.itemid), iitem.quantity);
				}
			}

			// Set skills version
			GameManager.instance.EnqueueSteamWebCommand(() => {
				return SteamResetUserSkillVersion();
			});

			while (resetPending) {
				yield return null;
			}
		}

		IEnumerator SteamResetUserSkillVersion() {
			var statPairs = new KeyValuePair<string, string>[1];
			statPairs[0] = new KeyValuePair<string, string>("skver", SKILLS_VERSION.ToString());
			var req = SteamSetUserStats(_uuid, statPairs);
			yield return req;
			resetPending = false;
		}
#endif

		public void Tick(float dt) {

			if (_api == API.Server) {
				_nextFlush -= dt;
				if (_nextFlush <= 0f) {
					_nextFlush = FLUSH_RATE;
					ServerFlush();
				}
			}
		}

		public void RefreshClientInventory() {
			if (ready) {
				ready = false;
				GameManager.instance.StartCoroutine(CoLoadInventory());
			}
		}

		void ServerFlush() {
#if (STEAM_API && UNITY_EDITOR) || BACKEND_SERVER

			List<KeyValuePair<string, string>> statPairs = null;

			if (_xpDirty) {
				var value = xp.ToString();

				if (statPairs == null) {
					statPairs = new List<KeyValuePair<string, string>>();
				}
				statPairs.Add(new KeyValuePair<string, string>("ss[0].xp", value));
				_xpDirty = false;
			}

			for (int i = 0; i < _deityStats.Values.Count; ++i) {
				var stat = _deityStats.Values[i];
				if (stat.dirty) {
					var key = string.Format("ss[0].d[{0}].xp", stat.statIndex);
					var value = stat.xp.ToString();
					if (statPairs == null) {
						statPairs = new List<KeyValuePair<string, string>>();
					}
					statPairs.Add(new KeyValuePair<string, string>(key, value));
					stat.dirty = false;
				}
			}

			if (_welcomeDropDirty) {
				_welcomeDropDirty = false;
				if (statPairs == null) {
					statPairs = new List<KeyValuePair<string, string>>();
				}
				statPairs.Add(new KeyValuePair<string, string>("welcome_drop", _welcomeDrop ? "1" : "0"));
			}

			if (statPairs != null) {
				RunTransaction(SteamSetUserStats(_uuid, statPairs));
			}

#elif !SHIP
			var didFlush = false;

			if (_xpDirty) {
				didFlush = true;
				_xpDirty = false;
				XMLInventory.Save(string.Format("/{0}_{1}", _uuid, FILENAME), this);
			}
#endif
		}
				
		public void AsyncGrantItem(int id, int count) {
#if (STEAM_API && UNITY_EDITOR) || BACKEND_SERVER
			RunTransaction(SteamGrantItem(_uuid, id));
#elif !SHIP
			InventoryItemClass itemClass;
			int ilvl;

			var library = GameManager.instance.staticData.inventoryItemLibrary;
			if (library.TryGetItem(id, out itemClass, out ilvl)) {
				var guid = System.Guid.NewGuid();
				var itemid = System.BitConverter.ToUInt64(guid.ToByteArray(), 0);
				_inventory.Add(itemid, new InventoryItem(itemid, id, itemClass, ilvl, count));
			}

			if (_api == API.Client) {
				if (GameManager.instance.clientWorld != null) {
					return;
				}
			}

			XMLInventory.Save(string.Format("/{0}_{1}", _uuid, FILENAME), this);
#endif
		}

		public ItemStats RecalcItemStats(int min_ilvl, int max_ilvl) {
			_stats = new ItemStats(_runes, min_ilvl, max_ilvl);
			return _stats;
		}

		public ItemStats GetItemStats(int min_ilvl, int max_ilvl) {
			return _stats ?? RecalcItemStats(min_ilvl, max_ilvl);
		}

		public void Socket(InventoryItem item, int rune, int gem) {
			if (gem >= 0) {
				var gemslot = _runes[rune].gems[gem];
				if (gemslot.item != item) {
					if (gemslot.item != null) {
						Unsocket(gemslot.id, gemslot.item);
					}
					gemslot.item = item;

					if (item != null) {
						gemslot.id = item.id;
						if (!item.socketed) {
							Socket(item);
						}
					} else {
						gemslot.id = 0;
					}
				}
			} else {
				var runeslot = _runes[rune];
				if (runeslot.item != item) {
					if (runeslot.item != null) {
						Unsocket(runeslot.id, runeslot.item);
					}
					runeslot.item = item;

					if (item != null) {
						runeslot.id = item.id;
						if (!item.socketed) {
							Socket(item);
						}
					} else {
						runeslot.id = 0;
					}
				}
			}
		}

		public bool Socket(int id, int rune, int gem) {
			var changed = false;

			if (gem >= 0) {
				var gemslot = _runes[rune].gems[gem];
				if (gemslot.id != id) {
					if (gemslot.id != 0) {
						Unsocket(gemslot.id, gemslot.item);
					}

					gemslot.item = (id != 0) ? Socket(id) : null;
					if ((gemslot.item != null) && (((InventorySocketItemClass)gemslot.item.itemClass).socket == ESocketItemType.Rune)) {
						Unsocket(id, gemslot.item);
						gemslot.item = null;
					}
					gemslot.id = (gemslot.item != null) ? id : 0;
					changed = true;
				}
			} else {
				var runeslot = _runes[rune];
				if (runeslot.id != id) {
					if (runeslot.id != 0) {
						Unsocket(runeslot.id, runeslot.item);
					}

					runeslot.item = (id != 0) ? Socket(id) : null;
					if ((runeslot.item != null) && (((InventorySocketItemClass)runeslot.item.itemClass).socket != ESocketItemType.Rune)) {
						Unsocket(id, runeslot.item);
						runeslot.item = null;
					}
					runeslot.id = (runeslot.item != null) ? id : 0;
					changed = true;
				}
			}

			return changed;
		}

		public bool Unsocket(int rune, int gem) {
			var changed = false;

			if (gem >= 0) {
				var gemslot = _runes[rune].gems[gem];
				if (gemslot.item != null) {
					Unsocket(gemslot.id, gemslot.item);
					gemslot.id = 0;
					gemslot.item = null;
					changed = true;
				}
			} else {
				var runeslot = _runes[rune];
				if (runeslot.item != null) {
					Unsocket(runeslot.id, runeslot.item);
					runeslot.id = 0;
					runeslot.item = null;
					changed = true;
				}
			}

			return changed;
		}

		InventoryItem Socket(int id) {
			List<InventoryItem> items;
			if (_unsocketed.TryGetValue(id, out items)) {
				var ii = items[items.Count-1];

				ii.socketed = true;
				items.RemoveAt(items.Count-1);
				if (items.Count < 1) {
					_unsocketed.Remove(id);
				}

				if (!_socketed.TryGetValue(id, out items)) {
					items = new List<InventoryItem>();
					_socketed[id] = items;
				}

				items.Add(ii);

				return ii;
			}
			return null;
		}

		void Socket(InventoryItem item) {
			var id = item.itemClass.GetiLvlID(item.ilvl);

			List<InventoryItem> items;
			if (_unsocketed.TryGetValue(id, out items)) {
				if (items.Remove(item)) {
					item.socketed = true;

					if (!_socketed.TryGetValue(id, out items)) {
						items = new List<InventoryItem>();
						_socketed[id] = items;
					}

					items.Add(item);
				}
			}
		}

		void Unsocket(int id, InventoryItem item) {
			List<InventoryItem> items;

			if (_socketed.TryGetValue(id, out items)) {
				items.Remove(item);
				if (items.Count < 1) {
					_socketed.Remove(id);
				}

				if (!_unsocketed.TryGetValue(id, out items)) {
					items = new List<InventoryItem>();
					_unsocketed[id] = items;
				}

				items.Add(item);
				item.socketed = false;
			}
		}

		public void UnsocketAll() {
			_socketed = new Dictionary<int, List<InventoryItem>>();
			_unsocketed = new Dictionary<int, List<InventoryItem>>();

			// all items go into unsocketed first
			for (int i = 0; i < _inventory.Values.Count; ++i) {
				var ii = _inventory.Values[i];
				if (ii.itemClass.HasiLvl(ii.ilvl)) {
					var id = ii.itemClass.GetiLvlID(ii.ilvl);
					List<InventoryItem> items;

					if (!_unsocketed.TryGetValue(id, out items)) {
						items = new List<InventoryItem>();
						_unsocketed[id] = items;
					}
					items.Add(ii);
				}
			}
		}

		void ResocketAll() {
			_stats = null;
			UnsocketAll();

			for (int i = 0; i < _runes.Length; ++i) {
				var rune = _runes[i];
				if (rune.id != 0) {
					rune.item = null;
					Socket(rune.id, i, -1);
				}

				for (int k = 0; k < rune.gems.Length; ++k) {
					var gem = rune.gems[k];
					if (gem.id != 0) {
						gem.item = null;
						Socket(gem.id, i, k);
					}
				}
			}
		}

		public bool HasInventorySpell(AbilityClass spell, int ilvl) {
			UnlockedAbility unlocked;
			if (_unlockedAbilities.TryGetValue(spell, out unlocked)) {
				return unlocked.ilvls.ContainsKey(ilvl);
			}
			return false;
		}

		public InventoryGrantAbilityItemClass GetUnlockedSpellItem(AbilityClass spell, int ilvl) {
			UnlockedAbility ability;
			if (_unlockedAbilities.TryGetValue(spell, out ability)) {
				InventoryGrantAbilityItemClass itemClass;
				if (ability.ilvls.TryGetValue(ilvl, out itemClass)) {
					return itemClass;
				}
			}
			return null;
		}

		public Dictionary<AbilityClass, UnlockedAbility>.ValueCollection GetUnlockedAbilities() {
			return _unlockedAbilities.Values;
		}

		public RuneSlot[] runeSlots {
			get {
				return _runes;
			}
		}

//#if !DEDICATED_SERVER
//		public void SendServerSocketedItems() {
//			var lp = ClientPlayerController.localPlayer;
//			if (lp != null) {
//				for (int i = 0; i < _runes.Length; ++i) {
//					var rune = _runes[i];
//					lp.ServerSocketItem(rune.id, i, -1);

//					for (int k = 0; k < rune.gems.Length; ++k) {
//						var gem = rune.gems[k];
//						lp.ServerSocketItem(gem.id, i, k);
//					}
//				}

//				lp.ServerFlushSocketedItems();
//			}
//		}
//#endif

#if (STEAM_API && UNITY_EDITOR) || BACKEND_SERVER
		static IEnumerator SteamGrantItem(ulong uuid, int id) {
			var req = SteamInventoryAddItem.Execute(uuid, new[] { id });
			yield return req.Wait();
			if (req.response.error != null) {
				Debug.LogError("ERROR: giving itemdefid " + id + " to " + uuid + "response:");
				Debug.LogError(req.response.error);
			}
		}

		static IEnumerator SteamSetUserStats(ulong uuid, IList<KeyValuePair<string, string>> values) {
			var req = SteamSetUserStatsForGame.Execute(uuid, values);
			yield return req.Wait();
		}
#endif

		static void RunTransaction(IEnumerator call) {
			GameManager.instance.StartCoroutine(WrapTransaction(call));
		}

		static IEnumerator WrapTransaction(IEnumerator call) {
			++GameManager.instance.activeTransactionCount;
			try {
				yield return call;
			} finally {
				if (GameManager.instance != null) {
					--GameManager.instance.activeTransactionCount;
				}
			}
		}

		public void Dispose() {
			if (_disposed) {
				throw new System.ObjectDisposedException(ToString());
			}

			_disposed = true;

			if (_coLoadInventory != null) {
				if (GameManager.instance != null) {
					GameManager.instance.StopCoroutine(_coLoadInventory);
				}
				_coLoadInventory = null;
			} else if (ready && (_api == API.Server)) {
				ServerFlush();
			}
		}

		void UpdateLevelXP() {
			level = 1;// GameManager.instance.staticData.xpTable.GetXPLevel(xp);
		}

		public InventoryItem GetInventoryItem(ulong id) {
			InventoryItem item;
			if (_inventory.TryGetValue(id, out item)) {
				return item;
			}
			return null;
		}

		public int xp {
			get;
			private set;
		}
		
		public int ilvl {
			get {
				return 1;// return Mathf.Max(1, level * GameManager.instance.staticData.xpTable.ilvlPerLevel);
			}
		}

		public int level {
			get;
			private set;
		}

		public bool ready {
			get;
			private set;
		}

		public bool welcomeDrop {
			get {
				return _welcomeDrop;
			}
			set {
				if (_welcomeDrop != value) {
					_welcomeDrop = value;
					_welcomeDropDirty = true;
				}
			}
		}

		public ReadOnlyCollection<InventoryItem> inventory {
			get {
				return _inventory.Values;
			}
		}
	}
}
