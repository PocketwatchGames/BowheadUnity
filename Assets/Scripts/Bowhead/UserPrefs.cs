// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System.Xml;
using System;

namespace Bowhead {
	public abstract class UserPrefs {

#if UNITY_EDITOR || UNITY_STANDALONE
		public static readonly string MY_DOCUMENTS = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "/Bowhead";
#endif

		static UserPrefs() {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			instance = new WindowsUserPrefs();
#else
			instance = new UnityUserPrefs();
#endif
		}

		public static UserPrefs instance {
			get;
			private set;
		}

		public static XmlDocument LoadXML(string path) {
			var xml = new XmlDocument();
			xml.Load(MY_DOCUMENTS + path);
			return xml;
		}

		public static void SaveXML(XmlDocument xml, string path) {
			path = MY_DOCUMENTS + path;
			System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
			xml.Save( path);
		}

		public static string[] GetFiles(string path, string searchPattern) {
			try {
				return System.IO.Directory.GetFiles(MY_DOCUMENTS + path, searchPattern);
			} catch {
				return new string[0];
			}
		}

		public abstract string GetString(string key, string defaultValue);
		public abstract int GetInt(string key, int defaultValue);
		public abstract float GetFloat(string key, float defaultValue);
		public abstract Color GetColor(string key, Color defaultValue);

		public abstract void SetString(string key, string value);
		public abstract void SetInt(string key, int value);
		public abstract void SetFloat(string key, float value);
		public abstract void SetColor(string key, Color value);

		public abstract void DeleteKey(string key);
		public abstract void DeleteAll();

		public abstract void Save();
	}

	public class UnityUserPrefs : UserPrefs {

		public override string GetString(string key, string defaultValue) {
			return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : defaultValue;
		}

		public override int GetInt(string key, int defaultValue) {
			return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : defaultValue;
		}

		public override float GetFloat(string key, float defaultValue) {
			return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetFloat(key) : defaultValue;
		}

		public override Color GetColor(string key, Color defaultValue) {
			var val = GetString(key, null);
			if (val == null) {
				return defaultValue;
			}

			var fields = val.Split(';');

			if (fields.Length != 4) {
				return defaultValue;
			}

			try {
				Color32 c32 = new Color32(byte.Parse(fields[0]), byte.Parse(fields[1]), byte.Parse(fields[2]), byte.Parse(fields[3]));
				return c32;
			} catch {
				return defaultValue;
			}
		}

		public override void SetString(string key, string value) {
			PlayerPrefs.SetString(key, value);
		}

		public override void SetInt(string key, int value) {
			PlayerPrefs.SetInt(key, value);
		}

		public override void SetFloat(string key, float value) {
			PlayerPrefs.SetFloat(key, value);
		}

		public override void SetColor(string key, Color value) {
			Color32 c32 = value;
			var str = string.Format("{0};{1};{2};{3}", c32.r, c32.g, c32.b, c32.a);
			PlayerPrefs.SetString(key, str);
		}

		public override void DeleteKey(string key) {
			PlayerPrefs.DeleteKey(key);
		}

		public override void DeleteAll() {
			PlayerPrefs.DeleteAll();
		}

		public override void Save() {}
	}

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

	public class WindowsUserPrefs : UserPrefs {
				
		static readonly string PREFS_FILE = "/usersettings.xml";

		Dictionary<string, string> _keyValues = new Dictionary<string, string>();

		public WindowsUserPrefs() {
			Load();
		}

		public override string GetString(string key, string defaultValue) {
			string value;
			if (_keyValues.TryGetValue(key, out value)) {
				return value;
			}
			return defaultValue;
		}

		public override int GetInt(string key, int defaultValue) {
			var str = GetString(key, null);
			if (str != null) {
				return int.Parse(str);
			}
			return defaultValue;
		}

		public override float GetFloat(string key, float defaultValue) {
			var str = GetString(key, null);
			if (str != null) {
				return Utils.ParseFloat(str);
			}
			return defaultValue;
		}

		public override Color GetColor(string key, Color defaultValue) {
			var val = GetString(key, null);
			if (val == null) {
				return defaultValue;
			}

			var fields = val.Split(';');

			if (fields.Length != 4) {
				return defaultValue;
			}

			try {
				Color32 c32 = new Color32(byte.Parse(fields[0]), byte.Parse(fields[1]), byte.Parse(fields[2]), byte.Parse(fields[3]));
				return c32;
			} catch {
				return defaultValue;
			}
		}

		public override void SetString(string key, string value) {
			Assert.IsNotNull(value);
			_keyValues[key] = value;
		}

		public override void SetInt(string key, int value) {
			_keyValues[key] = value.ToString();
		}

		public override void SetFloat(string key, float value) {
			_keyValues[key] = value.ToString();
		}

		public override void SetColor(string key, Color value) {
			Color32 c32 = value;
			var str = string.Format("{0};{1};{2};{3}", c32.r, c32.g, c32.b, c32.a);
			SetString(key, str);
		}

		public override void DeleteKey(string key) {
			_keyValues.Remove(key);
		}

		public override void DeleteAll() {
			_keyValues.Clear();
		}

		void Load() {
			_keyValues.Clear();

			try {
				var xml = LoadXML(PREFS_FILE);

				foreach (var outer in xml.ChildNodes) {
					var elem = outer as XmlElement;
					if (elem != null) {
						if (elem.Name == "settings") {

							foreach (var inner in elem.ChildNodes) {
								var child = inner as XmlElement;
								if (child != null) {
									_keyValues[child.Name] = child.GetAttribute("value");
								}
							}

						}
					}
				}
			} catch (Exception e) {
				_keyValues.Clear();
				Debug.LogWarning(e.Message);
			}
		}

		public override void Save() {
			try {
				var xml = new XmlDocument();

				var decl = xml.CreateXmlDeclaration("1.0", "UTF-8", null);
				var root = xml.DocumentElement;
				xml.InsertBefore(decl, root);

				var outer = xml.CreateElement("settings");
				xml.AppendChild(outer);

				foreach (var pair in _keyValues) {
					var inner = xml.CreateElement(pair.Key);
					outer.AppendChild(inner);

					inner.SetAttribute("value", pair.Value);
				}

				SaveXML(xml, PREFS_FILE);

			} catch (Exception e) {
				Debug.LogException(e);
			}
		}
	}

#endif
}