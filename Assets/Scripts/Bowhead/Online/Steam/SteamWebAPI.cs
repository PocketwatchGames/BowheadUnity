// Copyright (c) 2018 Pocketwatch Games LLC.
#if UNITY_EDITOR || BACKEND_SERVER

using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System;

namespace Bowhead.Online.SteamWebAPI {
	public interface SteamAsyncResult<T> {
		IEnumerator Wait(); // may throw exception
		T response { get; }
	}

	public abstract class SteamWebRequest<T> where T : new() {
		const string API_BASE_URL = "https://partner.steam-api.com";
		protected const string WEB_API_KEY = "433BCD4016A6A0BC6CA3F0CD3F4E11C9";
		protected const string APPID = "346930";
		const string URI_ARGS = "?key="+WEB_API_KEY+"&appid="+APPID;

		protected readonly string uri;
		protected readonly string uriWithArgs;

		static T _instance;

		static SteamWebRequest() {
			_instance = new T();
		}

		protected static T instance {
			get {
				return _instance;
			}
		}

		protected SteamWebRequest(string methodName) {
			uri = API_BASE_URL + methodName;
			uriWithArgs = uri + URI_ARGS;
		}

		protected string BuildCommandString(IList<KeyValuePair<string, string>> arguments) {
			var cmd = new StringBuilder(uriWithArgs);

			if ((arguments != null) && (arguments.Count > 0)) {
				for (int i = 0; i < arguments.Count; ++i) {
					var pair = arguments[i];
					cmd.Append("&");
					cmd.Append(pair.Key);
					cmd.Append("=");
					cmd.Append(pair.Value);
				}
			}

			return cmd.ToString();
		}
	}

	public abstract class SteamWebRequestGET<T> : SteamWebRequest<T> where T : SteamWebRequestGET<T>, new() {

		protected SteamWebRequestGET(string methodName) : base(methodName) { }

		protected UnityWebRequest HTTPGET(IList<KeyValuePair<string, string>> arguments) {
			return UnityWebRequest.Get(BuildCommandString(arguments));
		}
	}

	public abstract class SteamWebRequestPOST<T> : SteamWebRequest<T> where T : SteamWebRequestPOST<T>, new() {

		protected SteamWebRequestPOST(string methodName) : base(methodName) { }

		protected UnityWebRequest HTTPPOST(IList<KeyValuePair<string, string>> arguments) {
			var www = new WWWForm();

			www.AddField("key", WEB_API_KEY);
			www.AddField("appid", APPID);

			for (int i = 0; i < arguments.Count; ++i) {
				var arg = arguments[i];
				www.AddField(arg.Key, arg.Value);
			}

			return UnityWebRequest.Post(uri, www);
		}
	}

	public sealed class SteamGetUserStatsForGame : SteamWebRequestGET<SteamGetUserStatsForGame> {

		public SteamGetUserStatsForGame() : base("/ISteamUserStats/GetUserStatsForGame/v2/") { }

		// {"playerstats": {"steamID": "76561198181964210","gameName": "Bowhead","stats": [{"name": "stat_1","value": 100}]}}

		[Serializable]
		public struct Stat {
			public string name;
			public string value;
		}

		[Serializable]
		public struct Response {
			public Stat[] stats;
		}

		[Serializable]
		struct Json {
			public Response playerstats;
		}

		class AsyncResult : SteamAsyncResult<Response> {

			public IEnumerator Wait() {
				return enumerator;
			}

			public IEnumerator enumerator {
				get;
				set;
			}

			public Response response {
				get;
				set;
			}
		}

		public static SteamAsyncResult<Response> Execute(ulong steamID) {
			var result = new AsyncResult();
			result.enumerator = Execute(steamID, result);
			return result;
		}

		static IEnumerator Execute(ulong steamID, AsyncResult result) {
			List<KeyValuePair<string, string>> args = new List<KeyValuePair<string, string>>();

			args.Add(new KeyValuePair<string, string>("steamid", steamID.ToString()));

			using (var webReq = instance.HTTPGET(args)) {
				yield return webReq.SendWebRequest();

				if (webReq.isNetworkError) {
					throw new Exception(webReq.error);
				} else {
					Debug.Log(webReq.downloadHandler.text);
					var json = JsonUtility.FromJson<Json>(webReq.downloadHandler.text);
					result.response = json.playerstats;
				}
			}
		}
	}

	public sealed class SteamSetUserStatsForGame : SteamWebRequestPOST<SteamSetUserStatsForGame> {

		public SteamSetUserStatsForGame() : base("/ISteamUserStats/SetUserStatsForGame/v1/") { }

		[Serializable]
		public struct Response {
			public int result;
		}

		[Serializable]
		struct Json {
			public Response response;
		}

		class AsyncResult : SteamAsyncResult<Response> {

			public IEnumerator Wait() {
				return enumerator;
			}

			public IEnumerator enumerator {
				get;
				set;
			}

			public Response response {
				get;
				set;
			}
		}

		public static SteamAsyncResult<Response> Execute(ulong steamID, IList<KeyValuePair<string, string>> values) {
			var result = new AsyncResult();
			result.enumerator = Execute(steamID, values, result);
			return result;
		}

		static IEnumerator Execute(ulong steamID, IList<KeyValuePair<string, string>> values, AsyncResult result) {
			List<KeyValuePair<string, string>> args = new List<KeyValuePair<string, string>>();

			args.Add(new KeyValuePair<string, string>("steamid", steamID.ToString()));
			args.Add(new KeyValuePair<string, string>("count", values.Count.ToString()));

			for (int i = 0; i < values.Count; ++i) {
				var value = values[i];
				args.Add(new KeyValuePair<string, string>("name[" + i + "]", value.Key));
				args.Add(new KeyValuePair<string, string>("value[" + i + "]", value.Value));
			}

			using (var webReq = instance.HTTPPOST(args)) {
				yield return webReq.SendWebRequest();

				if (webReq.isNetworkError) {
					throw new Exception(webReq.error);
				} else {
					Debug.Log(webReq.downloadHandler.text);
					var json = JsonUtility.FromJson<Json>(webReq.downloadHandler.text);
					result.response = json.response;
				}
			}
		}
	}

	public sealed class SteamGetUserInventory : SteamWebRequestGET<SteamGetUserInventory> {

		public SteamGetUserInventory() : base("/IInventoryService/GetInventory/v1/") { }

		/*
		  "{\n\t\"response\": {\n\t\t\"item_json\": \"[{ \\\"accountid\\\":\\\"76561198181964210\\\",\\\"itemid\\\":\\\"620775784293909101\\\",\\\"quantity\\\":1,\\\"originalitemid\\\":\\\"620775784293909101\\\",\\\"itemdefid\\\":\\\"0\\\",\\\"appid\\\":346930,\\\"acquired\\\":\\\"20170331T204422Z\\\",\\\"state\\\":\\\"\\\",\\\"origin\\\":\\\"external\\\",\\\"state_changed_timestamp\\\":\\\"20170331T204422Z\\\" },{ \\\"accountid\\\":\\\"76561198181964210\\\",\\\"itemid\\\":\\\"620775784293911393\\\",\\\"quantity\\\":1,\\\"originalitemid\\\":\\\"620775784293911393\\\",\\\"itemdefid\\\":\\\"0\\\",\\\"appid\\\":346930,\\\"acquired\\\":\\\"20170331T204515Z\\\",\\\"state\\\":\\\"\\\",\\\"origin\\\":\\\"external\\\",\\\"state_changed_timestamp\\\":\\\"20170331T204515Z\\\" },{ \\\"accountid\\\":\\\"76561198181964210\\\",\\\"itemid\\\":\\\"620775784293911656\\\",\\\"quantity\\\":1,\\\"originalitemid\\\":\\\"620775784293911656\\\",\\\"itemdefid\\\":\\\"0\\\",\\\"appid\\\":346930,\\\"acquired\\\":\\\"20170331T204521Z\\\",\\\"state\\\":\\\"\\\",\\\"origin\\\":\\\"external\\\",\\\"state_changed_timestamp\\\":\\\"20170331T204521Z\\\" }]\"\n\t}\n}"
		*/

		[Serializable]
		public struct Item {
			public string itemid;
			public string itemdefid;
			public int quantity;
		}

		[Serializable]
		struct ItemJson {
			public Item[] items;
		};

		[Serializable]
		struct Response {
			public string error;
			public string item_json;
		}

		[Serializable]
		struct Json {
			public Response response;
		}

		class AsyncResult : SteamAsyncResult<Item[]> {

			public IEnumerator Wait() {
				return enumerator;
			}

			public IEnumerator enumerator {
				get;
				set;
			}

			public Item[] response {
				get;
				set;
			}
		}

		public static SteamAsyncResult<Item[]> Execute(ulong steamID) {
			var result = new AsyncResult();
			result.enumerator = Execute(steamID, result);
			return result;
		}

		static IEnumerator Execute(ulong steamID, AsyncResult result) {
			List<KeyValuePair<string, string>> args = new List<KeyValuePair<string, string>>();

			args.Add(new KeyValuePair<string, string>("appid", APPID));
			args.Add(new KeyValuePair<string, string>("key", WEB_API_KEY));
			args.Add(new KeyValuePair<string, string>("steamid", steamID.ToString()));

			using (var webReq = instance.HTTPGET(args)) {
				yield return webReq.SendWebRequest();

				if (webReq.isNetworkError) {
					throw new Exception(webReq.error);
				} else {
					result.response = null;

					var json = JsonUtility.FromJson<Json>(webReq.downloadHandler.text);
					if (json.response.item_json != null) {
						var item_json = json.response.item_json;
						item_json = item_json.Replace("\\\"", "\"");
						item_json = "{\"items\":"+item_json+"}";
						var items = JsonUtility.FromJson<ItemJson>(item_json);
						result.response = items.items;
					}
				}
			}
		}
	}

	public sealed class SteamInventoryAddItem : SteamWebRequestPOST<SteamInventoryAddItem> {

		public SteamInventoryAddItem() : base("/IInventoryService/AddItem/v1/") { }

		[Serializable]
		public struct Response {
			public string item_json;
			public string error;
		}

		[Serializable]
		struct Json {
			public Response response;
		}

		class AsyncResult : SteamAsyncResult<Response> {

			public IEnumerator Wait() {
				return enumerator;
			}

			public IEnumerator enumerator {
				get;
				set;
			}

			public Response response {
				get;
				set;
			}
		}

		public static SteamAsyncResult<Response> Execute(ulong steamID, IList<int> items) {
			var result = new AsyncResult();
			result.enumerator = Execute(steamID, items, result);
			return result;
		}

		static IEnumerator Execute(ulong steamID, IList<int> items, AsyncResult result) {
			List<KeyValuePair<string, string>> args = new List<KeyValuePair<string, string>>();

			args.Add(new KeyValuePair<string, string>("steamid", steamID.ToString()));

			for (int i = 0; i < items.Count; ++i) {
				var itemid = items[i];
				args.Add(new KeyValuePair<string, string>("itemdefid[" + i + "]", itemid.ToString()));
			}

			using (var webReq = instance.HTTPPOST(args)) {
				yield return webReq.SendWebRequest();

				if (webReq.isNetworkError) {
					throw new Exception(webReq.error);
				} else {
					var json = JsonUtility.FromJson<Json>(webReq.downloadHandler.text);
					result.response = json.response;
				}
			}
		}
	}

	public sealed class SteamInventoryTradeSetUnowned : SteamWebRequestPOST<SteamInventoryTradeSetUnowned> {

		public SteamInventoryTradeSetUnowned() : base("/IGameInventory/TradeSetUnowned/v1/") { }

		static readonly DateTime Jan1970 = new DateTime(1970, 1, 1);

		[Serializable]
		public struct Response {
			public bool success;
		}

		[Serializable]
		struct Json {
			public Response response;
		}

		class AsyncResult : SteamAsyncResult<Response> {

			public IEnumerator Wait() {
				return enumerator;
			}

			public IEnumerator enumerator {
				get;
				set;
			}

			public Response response {
				get;
				set;
			}
		}

		public static SteamAsyncResult<Response> Execute(ulong steamID, ulong assetid, int count) {
			var result = new AsyncResult();
			result.enumerator = Execute(steamID, assetid, count, result);
			return result;
		}

		static IEnumerator Execute(ulong steamID, ulong assetid, int count, AsyncResult result) {
			List<KeyValuePair<string, string>> args = new List<KeyValuePair<string, string>>();

			args.Add(new KeyValuePair<string, string>("owner", steamID.ToString()));
			args.Add(new KeyValuePair<string, string>("contextid", "2"));
			args.Add(new KeyValuePair<string, string>("assetid", assetid.ToString()));
			args.Add(new KeyValuePair<string, string>("amount", count.ToString()));
			args.Add(new KeyValuePair<string, string>("audit_action", "101"));
			args.Add(new KeyValuePair<string, string>("audit_reference", "0"));
			args.Add(new KeyValuePair<string, string>("leave_original", "0"));
			args.Add(new KeyValuePair<string, string>("request_repeated", "0"));
			args.Add(new KeyValuePair<string, string>("is_market", "0"));
			args.Add(new KeyValuePair<string, string>("trade_start_time", ((int)Math.Floor((DateTime.UtcNow-Jan1970).TotalSeconds)).ToString()));

			using (var webReq = instance.HTTPPOST(args)) {
				yield return webReq.SendWebRequest();

				if (webReq.isNetworkError) {
					throw new Exception(webReq.error);
				} else {
					var json = JsonUtility.FromJson<Json>(webReq.downloadHandler.text);
					result.response = json.response;
				}
			}
		}
	}
#if UNITY_EDITOR
	public sealed class SteamInventoryUpdateItemDefs : SteamWebRequestPOST<SteamInventoryUpdateItemDefs> {

		public SteamInventoryUpdateItemDefs() : base("/IGameInventory/UpdateItemDefs/v0001/") { }

		class AsyncResult : SteamAsyncResult<bool> {

			public IEnumerator Wait() {
				return enumerator;
			}

			public IEnumerator enumerator {
				get;
				set;
			}

			public bool response {
				get;
				set;
			}
		}

		public static SteamAsyncResult<bool> Execute(IList<string> itemDefs) {
			var result = new AsyncResult();
			result.enumerator = Execute(itemDefs, result);
			return result;
		}

		static IEnumerator Execute(IList<string> itemDefs, AsyncResult result) {
			List<KeyValuePair<string, string>> args = new List<KeyValuePair<string, string>>();

			var stringBuilder = new StringBuilder();

			stringBuilder.Append("[");

			for (int i = 0; i < itemDefs.Count; ++i) {
				if (i != 0) {
					stringBuilder.Append(",");
				}
				stringBuilder.Append(itemDefs[i]);
			}

			stringBuilder.Append("]");

			args.Add(new KeyValuePair<string, string>("itemdefs", stringBuilder.ToString()));

			using (var webReq = instance.HTTPPOST(args)) {
				var req = webReq.SendWebRequest();

				while (!req.isDone) {
					yield return null;
				}

				if (webReq.isNetworkError) {
					throw new Exception(webReq.error);
				} else {
					var text = webReq.downloadHandler.text;

					result.response = text.IndexOf("\"success\": true") != -1;	
				}
			}
		}
	}
#endif
}

#endif
