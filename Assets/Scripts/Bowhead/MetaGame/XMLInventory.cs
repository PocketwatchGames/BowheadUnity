// Copyright (c) 2018 Pocketwatch Games LLC.
#if !SHIP
using System;
using System.Xml;
using UnityEngine;

namespace Bowhead.MetaGame {
	public sealed class XMLInventory {

		public static void Save(string filename, PlayerInventorySkills data) {
			var xml = new XmlDocument();
			var decl = xml.CreateXmlDeclaration("1.0", "UTF-8", null);
			var root = xml.DocumentElement;
			xml.InsertBefore(decl, root);
			var outer = xml.CreateElement("data");
			outer.SetAttribute("skver", PlayerInventorySkills.SKILLS_VERSION.ToString());
			outer.SetAttribute("xp", data.xp.ToString());
			outer.SetAttribute("welcome_drop", data.welcomeDrop.ToString());
			xml.AppendChild(outer);

			var outerInv = xml.CreateElement("inventory");
			outer.AppendChild(outerInv);

			for (int i = 0; i < data.inventory.Count; ++i) {
				var inv = data.inventory[i];

				if (inv.iid > InventoryItemLibrary.FIRST_AUTOGRANT_ID) {
					var node = xml.CreateElement("item");
					if (SaveItem(inv, node)) {
						outerInv.AppendChild(node);
					}
				}
			}

			var outerDeities = xml.CreateElement("deities");
			outer.AppendChild(outerDeities);

			{
				var node = xml.CreateElement("skver");
				outerDeities.AppendChild(node);
				node.SetAttribute("value", PlayerInventorySkills.SKILLS_VERSION.ToString());
			}

			UserPrefs.SaveXML(xml, filename);
		}

		public static void Load(string filename, PlayerInventorySkills data, DictionaryList<ulong, PlayerInventorySkills.InventoryItem> mutableInventory, out int xp) {
			XmlDocument xml = null;
			xp = 0;

			try {
				xml = UserPrefs.LoadXML(filename);
			} catch (Exception e) {
				Debug.LogException(e);
				xml = null;
			}

			if (xml != null) {
				{
					var root = xml.DocumentElement;
					if (root.HasAttribute("skver")) {
						var skver = int.Parse(root.GetAttribute("skver"));
						if (skver <= PlayerInventorySkills.SKILLS_FORCE_RESET_VERSION) {
							return;
						}
					} else {
						return;
					}

					if (root.HasAttribute("xp")) {
						xp = int.Parse(root.GetAttribute("xp"));
					}
					if (root.HasAttribute("welcome_drop")) {
						data.welcomeDrop = bool.Parse(root.GetAttribute("welcome_drop"));
					} else {
						data.welcomeDrop = false;
					}
				}

				var inventory = xml.SelectNodes("//data/inventory");
				if (inventory.Count > 0) {
					inventory = inventory[0].ChildNodes;
					foreach (var node in inventory) {
						var elem = (XmlElement)node;
						if (elem.Name == "item") {
							var item = LoadItem(elem);
							if (item != null) {
								mutableInventory.Add(item.iid, item);
							}
						}
					}
				}
			}
		}

		static bool SaveItem(PlayerInventorySkills.InventoryItem item, XmlElement node) {
			int itemId;
			if (item.itemClass.TryGetItemID(item.ilvl, out itemId)) {
				node.SetAttribute("itemid", itemId.ToString());
				node.SetAttribute("instance", item.iid.ToString());
				node.SetAttribute("count", item.count.ToString());
				return true;
			}
			return false;
		}

		static PlayerInventorySkills.InventoryItem LoadItem(XmlElement node) {
			int id;
			ulong instance;
			int count;

			if (node.HasAttribute("itemid")) {
				id = int.Parse(node.GetAttribute("itemid"));
				if (node.HasAttribute("instance")) {
					instance = ulong.Parse(node.GetAttribute("instance"));
					if (node.HasAttribute("count")) {
						count = int.Parse(node.GetAttribute("count"));

						var itemLibrary = GameManager.instance.staticData.inventoryItemLibrary;
						InventoryItemClass itemClass;
						int ilvl;

						if (itemLibrary.TryGetItem(id, out itemClass, out ilvl)) {
							if (!(itemClass.deprecated || itemClass.hidden)) {
								return new PlayerInventorySkills.InventoryItem(instance, id, itemClass, ilvl, count);
							}
						}
					}
				}
			}

			return null;
		}
	}
}
#endif