// Copyright (c) 2018 Pocketwatch Games LLC.
//#define PIE_DEDICATED_SERVER
#if !DEDICATED_SERVER
#define LEAK_TRACKER
#endif

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using CodeStage.AdvancedFPSCounter;

namespace Bowhead {

	public class GameManager : MonoBehaviour, IGameInstance {
		const int DEFAULT_MATCH_TIME = 60*5;
		const int DEFAULT_OVERTIME = 90;
		const float NETSTAT_FREQUENCY = 1f;
		const int SETTINGS_VERSION = 1;

		public struct Level {
			public string name;
			public string[] sublevels;
		}

		public static readonly List<Level> LEVELS = new List<Level>();

		public Transform hudOverlays;
		public StaticData staticData;

		public int ragdollLimit { get; set; }
		public int gibLimit { get; set; }
		public float uiScale { get; set; }

		public int physicsRate {
			get {
				return _physicsRate;
			}
			set {
				_physicsRate = Mathf.Clamp(value, 0, 3);
				var maxFPS = 30 * Mathf.Pow(2, _physicsRate);
				Time.fixedDeltaTime = 1f / maxFPS;
			}
		}

		//[SerializeField]
		//GameObject umaPrefab;
		[SerializeField]
		Transform _tooltipCanvas;
		[SerializeField]
		[ClassDropdown(typeof(Server.GameMode))]
		string PIEGameMode;
		[SerializeField]
		int PIENumPlayers;
		[SerializeField]
		int PIEMatchTime;
		[SerializeField]
		int PIEOvertime;
		[SerializeField]
		bool PIEServerOnly;

		bool _bIsPIE;
		int _physicsRate;
		bool _serverQuitFlag;

		static GameManager _instance;

		AFPSCounter _fpsCounter;
		GameObject _clientObjectGroup;
		GameObject _serverObjectGroup;
		GameObject _staticObjectPoolRoot;
		GameObject _transientObjectPoolRoot;
		Dictionary<Material, Dictionary<int, Material>> _teamColorMaterials = new Dictionary<Material, Dictionary<int, Material>>();

		Server.ServerWorld _server;
		Client.ClientWorld _client;
		INetDriver _netDriver;

		Type _travelGameMode;
		string _travelLevel;
		string _pendingLevel;
		string _startingMap;
		Type _startingGameMode;
		Type _startingNetDriver;
		int _startingPort;
		string _startingConnect;
		double _timeSinceStart;
		int _inMenus;
		Vector2 _mouseDelta = Vector2.zero;
		Vector2 _mousePos = Vector2.zero;
		Vector2 _lastMousePos = Vector2.zero;
		Dictionary<string, CFuncMethod> cvarMethods;
		int fixedUpateCount;
		int lastFixedUpdateCount;
		float netstat = -1f;
		NetIOMetrics[] clientNetStat = new NetIOMetrics[] { NetIOMetrics.zero, NetIOMetrics.zero };
		NetIOMetrics[] serverNetStat = new NetIOMetrics[] { NetIOMetrics.zero, NetIOMetrics.zero };
		SoundManager _soundManager;
		TMPro.TMP_Text _guiStatus;
		bool _pendingCommand;
		bool _startup;

#if LEAK_TRACKER
		LeakTracker _leakTracker;
#endif

#if (STEAM_API && UNITY_EDITOR) || BACKEND_SERVER
		List<Func<IEnumerator>> _steamWebCommands = new List<Func<IEnumerator>>();
#endif

		class AsyncSceneLoad : World.IAsyncSceneLoad {
			AsyncOperation _first;
			AsyncOperation _second;

			public AsyncSceneLoad(AsyncOperation first, AsyncOperation second) {
				_first = first;
				_second = second;
			}

			public bool isDone {
				get {
					return _first.isDone && ((_second == null) || _second.isDone);
				}
			}

			public float progress {
				get {
					if (_second != null) {
						return (_first.progress + _second.progress) / 2f;
					}
					return _first.progress;
				}
			}
		}

		World.IAsyncSceneLoad _asyncLoad;

		static Assembly[] _moduleAssemblies;

		//struct LoadingScreenState {
		//	public int loadingTipIndex;
		//	public List<int> loadingTipIndexes;
		//	public float loadingTipTime;
		//	public string levelText;

		//	public GameObject root;
		//	public LoadingText text;
		//	public LoadingTips tips;
		//	public RawImage background;

		//	public int cameraElements;
		//	public Camera mainCamera;
		//}

		//LoadingScreenState loadingScreenState;

		void EnableAll() {
			foreach (var component in GetComponents<MonoBehaviour>()) {
				component.enabled = true;
			}
		}

		public static Assembly[] GetModuleAssemblies() {
			if (_moduleAssemblies == null) {
				//#if RELEASE
				//				_moduleAssemblies = System.AppDomain.CurrentDomain.GetAssemblies();
				//#else
				_moduleAssemblies = new[] { Assembly.GetExecutingAssembly(), Assembly.Load("Engine") };
				//#endif
			}
			return _moduleAssemblies;
		}

		void Awake() {

			if (_instance != null) {
				Utils.DestroyGameObject(gameObject);
				return;
			}

			SceneManager.sceneLoaded += LevelWasLoaded;

			_instance = this;
			applicationFocused = true;
			mainCamera = GameObject.FindGameObjectWithTag(Tags.MainCamera).GetComponent<Camera>();

			World.Streaming.StaticInit();
			DataManager.InitData();

			MainThreadTaskQueue.maxFrameTimeMicroseconds = 4000;

#if LEAK_TRACKER
			_leakTracker = new LeakTracker(GetModuleAssemblies());
#endif

			matchTime = DEFAULT_MATCH_TIME;
			matchOvertime = DEFAULT_OVERTIME;

#if PIE_DEDICATED_SERVER && UNITY_EDITOR
			dedicatedServer = true;
			batchMode = true;
			_startingMap = "KillingTime";
			_startingGameMode = Type.GetType(PIEGameMode);
			_startingNumPlayers = PIENumPlayers;
			_startingNetDriver = typeof(SocketNetDriver);
			_startingPort = 7777;
#else
#if DEDICATED_SERVER
			batchMode = true;
			dedicatedServer = true;
#else
			batchMode =	Utils.HasCommandLineArg("-batchmode");
			dedicatedServer = Utils.HasCommandLineArg("+dedicated");
#endif

			if (dedicatedServer) {
				Debug.Log("Dedicated server starting (" + BuildInfo.ID + ")...");
			} else {
				Debug.Log("Client starting (" + BuildInfo.ID + ")...");
			}

			LogSystemInfo();

			{
				var s = Utils.GetCommandLineArg("+matchTime");
				if (s != null) {
					matchTime = int.Parse(s);
					Debug.Log("+matchTime + " + s);
				}

				s = Utils.GetCommandLineArg("+overtime");
				if (s != null) {
					matchOvertime = int.Parse(s);
					Debug.Log("+matchOvertime " + s);
				}
			}

			_startingMap = Utils.GetCommandLineArg("+map");
			if (_startingMap != null) {
				Debug.Log("+map " + _startingMap);

				var s = Utils.GetCommandLineArg("+gamemode");
				if (s == null) {
					s = "Bowhead.Server.KingOfTheHill";
				}
				_startingGameMode = Type.GetType(s);
				if (_startingGameMode == null) {
					var x = "Bowhead.Server." + s;
					_startingGameMode = Type.GetType(x);
					if (_startingGameMode == null) {
						Debug.LogError("ERROR: +gamemode " + s + " not found");
						Application.Quit();
					} else {
						Debug.Log("+gamemode " + x);
					}
				} else {
					Debug.Log("+gamemode " + s);
				}

				s = Utils.GetCommandLineArg("+numplayers");
				if (s != null) {
					try {
						numPlayers = int.Parse(s);
						Debug.Log("+numplayers " + numPlayers);
					} catch (Exception e) {
						Debug.LogException(e);
						Debug.LogError("Failed to parse +numplayers");
						Application.Quit();
					}
				} else {
					numPlayers = 1;
				}

				s = Utils.GetCommandLineArg("+netdriver");
				if (s != null) {
					_startingNetDriver = Type.GetType(s);
					if (_startingNetDriver == null) {
						Debug.LogError("ERROR: +netdriver " + s + " not found");
						Application.Quit();
					} else {
						Debug.Log("+netdriver " + s);
					}
				} else {
					_startingNetDriver = (dedicatedServer || (numPlayers > 1)) ? typeof(SocketNetDriver) : typeof(LocalGameNetDriver);
				}

				s = Utils.GetCommandLineArg("+port");
				if (s != null) {
					try {
						_startingPort = int.Parse(s);
						Debug.Log("+port " + _startingPort);
					} catch (Exception e) {
						Debug.LogException(e);
						Debug.LogError("Failed to parse +port");
						Application.Quit();
					}
				} else {
					_startingPort = 7777;
				}
			} else {
				_startingConnect = Utils.GetCommandLineArg("+connect");
			}
#endif

			if (dedicatedServer) {
				if (Utils.HasCommandLineArg("+svperftest")) {
					serverPerfTest = true;
				}

				QualitySettings.vSyncCount = 0;

				var s = Utils.GetCommandLineArg("+framerate");
				if (s != null) {
					try {
						Application.targetFrameRate = int.Parse(s);
						Debug.Log("+framerate " + Application.targetFrameRate);
					} catch (Exception e) {
						Debug.LogException(e);
						Debug.LogError("Failed to parse +framerate");
						Application.Quit();
					}
				}

				s = Utils.GetCommandLineArg("+fixedupdate");
				if (s != null) {
					try {
						int fps = int.Parse(s);
						Debug.Log("+fixedupate " + fps);
						Time.fixedDeltaTime = 1f / fps;
					} catch (Exception e) {
						Debug.LogException(e);
						Debug.LogError("Failed to parse +fixedupate");
						Application.Quit();
					}
				}
			} else {
				PrecacheClientData();
				LoadClientSettings();

				if (_startingConnect != null) {
					if (Utils.HasCommandLineArg("+svperftest")) {
						serverPerfTest = true;
					}
				}
			}

#if BACKEND_SERVER
			QualitySettings.masterTextureLimit = 3; // use 1/8th size textures.
			prewarm = Utils.HasCommandLineArg("+prewarm");
			if (prewarm) {
				Debug.Log("+prewarm");
				_teamSchedule = new Bowhead.Server.StandardTeamSchedule(1);
			} else {
				telemetry = new Telemetry.Telemetry();
				try {
					var configPath = Utils.GetCommandLineArg("+telemetryConfig");
					Debug.Log("+telemetryConfig " + configPath);
					_teamSchedule = new Server.XMLTeamSchedule(configPath);
					numPlayers = _teamSchedule.numPlayers;
#if UNITY_EDITOR
					int port = Telemetry.NetMsgs.NetMsg.TELEMETRY_PORT+1;
					uint gameID = 1;
#else
					int port = int.Parse(Utils.GetCommandLineArg("+telemetryPort"));
					uint gameID = uint.Parse(Utils.GetCommandLineArg("+telemetryGameID"));

					var maxWaitTime = Utils.GetCommandLineArg("+maxWaitTime");
					if (maxWaitTime != null) {
						this.maxWaitTime = int.Parse(maxWaitTime);
						Debug.Log("+maxWaitTime " + this.maxWaitTime);
					}
#endif
					Debug.Log("+telemetryPort " + port);
					Debug.Log("+telemetryGameID " + gameID);
					telemetry.Connect(gameID, port);
					telemetry.Hello();
				} catch (Exception e) {
					Debug.LogException(e);
					Application.Quit();
				}
			}
#endif

#if !DEDICATED_SERVER
#if STEAM_API
			if (Steamworks.SteamAPI.RestartAppIfNecessary(Online.Steam.SteamServices.APP_ID)) {
#if UNITY_EDITOR
				UnityEditor.EditorApplication.isPaused = true;
#endif
				Application.Quit();
				return;
			}
#endif
#if LOGIN_SERVER
			loginServer = new LoginServer.LoginServer();
#endif

			onlineServices = Online.OnlineServicesAPI.Create();
			if (!onlineServices.Initialize()) {
				Debug.LogError("Online services failed to start.");
#if UNITY_EDITOR
				UnityEditor.EditorApplication.isPaused = true;
#endif
				Application.Quit();
				return;
			}

			onlineServices.AsyncLogin(OnlinePlayerLoggedIn);
#endif

#if (STEAM_API && UNITY_EDITOR) || BACKEND_SERVER
			StartCoroutine(CORunSteamWebCommands());
#endif

			cvarMethods = Console.GetCFuncs(GetModuleAssemblies());

			Application.logMessageReceived += LogCallback;

			Assert.raiseExceptions = true;

			EnableAll();

			_fpsCounter = GetComponent<AFPSCounter>();
			_soundManager = GetComponent<SoundManager>();

#if UNITY_EDITOR
			_fpsCounter.OperationMode = OperationMode.Normal;
#endif

			_clientObjectGroup = GameObject.Find("ClientGameObjects");
			if (_clientObjectGroup == null) {
				_clientObjectGroup = new GameObject("ClientGameObjects");
				GameObject.DontDestroyOnLoad(_clientObjectGroup);
			}

			_serverObjectGroup = GameObject.Find("ServerGameObjects");
			if (_serverObjectGroup == null) {
				_serverObjectGroup = new GameObject("ServerGameObjects");
				GameObject.DontDestroyOnLoad(_serverObjectGroup);
			}

			_staticObjectPoolRoot = GameObject.Find("StaticPooledGameObjects");
			if (_staticObjectPoolRoot == null) {
				_staticObjectPoolRoot = new GameObject("StaticPooledGameObjects");
				_staticObjectPoolRoot.SetActive(false);
				GameObject.DontDestroyOnLoad(_staticObjectPoolRoot);
			}

			staticData.randomNumberTable.seed = (uint)(DateTime.Now.Ticks & 0xffffffffL);
			DontDestroyOnLoad(transform.gameObject);

#if DEDICATED_SERVER
			MainMenuLoaded();
#else
			if (Utils.activeSceneName == "Entry") {
				_startup = true;
				SetPendingLevel("MainMenu", null);
			}
#if UNITY_EDITOR
			else {
				_bIsPIE = !Utils.activeSceneName.Contains("MainMenu");
				if (!_bIsPIE) {
					_travelLevel = "MainMenu";
					OnTravelFinished();
				}
			}
#endif
#endif
		}

		void LogSystemInfo() {
			System.Console.WriteLine(SystemInfo.operatingSystem);
			System.Console.WriteLine(SystemInfo.processorType + " X " + SystemInfo.processorCount);
			System.Console.WriteLine("Memory: " + SystemInfo.systemMemorySize);
		}

		//IEnumerator SteamWebAPITest() {
		//	{
		//		var req = Online.SteamWebAPI.SteamSetUserStatsForGame.Execute(76561198181964210UL, new[] { new KeyValuePair<string, uint>("stat_1", 100) });
		//		yield return req.Wait();
		//		Debug.Log(req.response.result);
		//	}
		//	{
		//		var req = Online.SteamWebAPI.SteamGetUserStatsForGame.Execute(76561198181964210UL);
		//		yield return req.Wait();
		//		Debug.Log(req.response);
		//	}
		//	{
		//		var req = Online.SteamWebAPI.SteamInventoryAddItem.Execute(76561198181964210UL, new[] { 1 });
		//		yield return req.Wait();
		//		Debug.Log(req.response);
		//	}
		//	{
		//		var req = Online.SteamWebAPI.SteamGetUserInventory.Execute(76561198181964210UL);
		//		yield return req.Wait();
		//		Debug.Log(req.response);
		//	}
		//}

		//void UnloadApex() {
		//	Apex.Services.GameServices.gameStateManager = null;
		//	Apex.Services.GameServices.cellCostStrategy = null;
		//	Apex.Services.GameServices.cellConstruction = null;
		//	Apex.Services.GameServices.messageBus = null;
		//	Apex.Services.GameServices.pathService = null;
		//	Apex.Services.GameServices.heightStrategy = null;
		//	Apex.Services.GameServices.vectorFieldManager = null;
		//	Apex.Services.GameServices.navigationSettings = null;
		//	Apex.LoadBalancing.LoadBalancer.marshaller = null;
		//	Apex.Units.GroupingManager.Clear();
		//}

		//void GetInventoryCallback(Online.MetaGame.ImmutableInventory inventory) {
		//	int b = 0;
		//}
		string _systemMessage;

		public string systemMessage {
			get {
				return _systemMessage;
			}
			set {
				_systemMessage = value;
				UpdateConnectionStatus();
			}
		}

		void MainMenuLoaded() {

			//UnloadApex();

			inMainMenu = true;
			didEverLoadMainMenu = true;
			_pendingCommand = false;

#if !DEDICATED_SERVER
			dialogManager = new Client.UI.DialogManager();

			//mainMenu = GameObject.Find("MainMenu").GetComponent<Client.UI.MainMenu.MainMenuScreen>();

			//menuManager = GameObject.Find("MainMenu").GetChildComponent<MenuManager>("TopMenuManager");

			_guiStatus = GameObject.Find("MainMenuCanvas/MainMenuPanel/VersionInfoStatus/Status").GetComponent<TMPro.TMP_Text>();
			_guiStatus.text = string.Empty;
			GameObject.Find("MainMenuCanvas/MainMenuPanel/VersionInfoStatus/Version").GetComponent<TMPro.TMP_Text>().text = BuildInfo.ID;

			Cursor.visible = true;
			SetScreenModeCursorLockState();
			UpdateConnectionStatus();

			if (_startup) {
				_startup = false;
				//if (UserPrefs.instance.GetInt("PlayedIntro", 0) == 0) {
				//	UserPrefs.instance.SetInt("PlayedIntro", 1);
				//	UserPrefs.instance.Save();
				//	StartCoroutine(LoadCinematic());
				//} else {
					StartCoroutine(LoadNormal());
				//	Play(Vector3.zero, clientData.sounds.ui.mainMenu.mainMenuStingers);
				//}
			} else {
				//if (clientInventory != null) {
				//	clientInventory.RefreshClientInventory();
				//}
				StartCoroutine(LoadNormal());
			}
#endif
			StartPlayOnAwakeSounds();
			GC();
		}

		IEnumerator LoadNormal() {
			yield return new WaitForSecondsRealtime(0.25f);
			//mainMenu.LoadNormal();
		}

		IEnumerator LoadCinematic() {
			yield return new WaitForSecondsRealtime(0.25f);
			//mainMenu.LoadCinematic();
		}

		void UpdateConnectionStatus() {
#if LOGIN_SERVER
			if (inMainMenu) {
				if (loginServer.state == LoginServer.EConnectionState.Disconnected) {
					_guiStatus.color = Color.red;
					_guiStatus.text = Utils.GetLocalizedText("UI.MainMenu.Offline");
				} else if (loginServer.state == LoginServer.EConnectionState.Connecting) {
					_guiStatus.color = Color.yellow;
					_guiStatus.text = Utils.GetLocalizedText("UI.MainMenu.Connecting");
				} else {
					if (string.IsNullOrEmpty(_systemMessage)) {
						_guiStatus.color = Color.yellow;
						_guiStatus.text = Utils.GetLocalizedText("UI.MainMenu.Online");
					} else {
						_guiStatus.color = Color.red;
						_guiStatus.text = _systemMessage;
					}
				}
			}
#endif
		}

		void MainMenuUnloaded() {
			inMainMenu = false;
			_guiStatus = null;
			//mainMenu = null;

#if !DEDICATED_SERVER
			if (onlineServices != null) {
				onlineServices.ReleaseAllAvatars();
			}
#endif
		}

		public void StartPlayOnAwakeSounds() {
			var sounds = GameObject.FindObjectsOfType<SoundEntity>();
			for (int i = 0; i < sounds.Length; ++i) {
				var s = sounds[i];
				if (s.playOnAwake) {
					s.PlayIfNotPlaying();
				}
			}
		}

		public Transform staticObjectPoolRoot {
			get {
				return _staticObjectPoolRoot.transform;
			}
		}

		public Transform transientObjectPoolRoot {
			get {
				if (_transientObjectPoolRoot == null) {
					_transientObjectPoolRoot = GameObject.Find("TransientPooledGameObjects");
					if (_transientObjectPoolRoot == null) {
						_transientObjectPoolRoot = new GameObject("TransientPooledGameObjects");
						_transientObjectPoolRoot.SetActive(false);
					}
				}
				return _transientObjectPoolRoot.transform;
			}
		}

		void Start() {
			transform.position = Vector3.zero;
			transform.rotation = Quaternion.identity;
			transform.Find("Console").gameObject.ActivateHierarchy(true);
			//hudOverlays.gameObject.SetActive(true);
			Console.SetExecutor(ConsoleExecutor);
			Console.SetTabComplete(ConsoleTabCompleter);
#if UNITY_EDITOR
			if (_bIsPIE && (PIEGameMode != null)) {
				StartPIE();
			}
#endif
		}

		void CreateUMA() {
			//_uma = GameObject.Instantiate(umaPrefab);
			//_umaGenerator = _uma.transform.Find("UMAGenerator").GetComponent<UMA.UMAGeneratorBase>();
		}

		// true if FixedUpdate() ran since the last call to Update()
		// since FixedUpdate() and Update() are staggered, Update() can sometimes
		// be called multiple times before FixedUpdate() runs again, causing 
		// computations based on physics to have zero velocities.

		public bool fixedUpdateDidRun {
			get;
			private set;
		}

		public float fixedUpdateDeltaTime {
			get;
			private set;
		}

		float _fixedUpdateDeltaTime;

		void FixedUpdate() {
			++fixedUpateCount;
			if (_server != null) {
				_server.FixedUpdate(Time.unscaledDeltaTime);
			}
		}

		void HandleStartupConnect() {
#if !DEDICATED_SERVER
			if (onlineLocalPlayer == null) {
				return;
			}
#endif
			if (_startingMap != null) {
				HostGame(_startingMap, _startingGameMode, _startingNetDriver, _startingPort);
				_startingMap = null;
			} else if (_startingConnect != null) {
				Connect(_startingConnect);
				_startingConnect = null;
			}
		}

		void Update() {
#if UNITY_EDITOR
			if (InputManager.GetKeyState(InputKey.Pause).pressed) {
				UnityEditor.EditorApplication.isPaused = true;
			}
#endif
#if BACKEND_SERVER
			if (!prewarm) {
				telemetry.Tick(Time.unscaledDeltaTime);
				if (!telemetry.connected) {
					Debug.LogError("Lost telemetry connection, quitting.");
					Application.Quit();
#if UNITY_EDITOR
					UnityEditor.EditorApplication.isPlaying = false;
#endif
					return;
				}
			}
#endif

			HandleStartupConnect();

			if ((_asyncLoad != null) && _asyncLoad.isDone) {
				_asyncLoad = null;
				OnTravelFinished();
			}

			if (didEverLoadMainMenu) {
#if !DEDICATED_SERVER
				onlineServices.Tick(Time.unscaledDeltaTime);
#if LOGIN_SERVER
				{
					var lobby = ((loginServer != null) && (loginServer.lobbies != null)) ? loginServer.lobbies.lobby : null;
					if ((lobby != null) && lobby.localPlayerIsOwner) {
						if (lobby.maxPlayers > lobby.players.Count) {
							if (lobby.type == LoginServer.NetMsgs.ELobbyType.Public) {
								onlineServices.SetLobbyID(lobby.lobbyID, Online.ELobbyType.Public);
							} else if (lobby.type == LoginServer.NetMsgs.ELobbyType.Friends) {
								onlineServices.SetLobbyID(lobby.lobbyID, Online.ELobbyType.Friends);
							} else {
								onlineServices.SetLobbyID(lobby.lobbyID, Online.ELobbyType.Private);
							}
						} else {
							onlineServices.SetLobbyID(lobby.lobbyID, Online.ELobbyType.Private);
						}
					} else {
						onlineServices.SetLobbyID(0, Online.ELobbyType.Private);
					}
				}

				loginServer.Tick(Time.unscaledDeltaTime);
				if ((loginServer.state == LoginServer.EConnectionState.Disconnected) && (_travelLevel == null) && !inMainMenu) {
					Debug.LogError("Disconnected from login server, traveling back to main menu.");
					SetPendingLevel("MainMenu", null);
				}
#endif
#endif
			}

			fixedUpdateDidRun = lastFixedUpdateCount != fixedUpateCount;
			_fixedUpdateDeltaTime += Time.deltaTime;
			fixedUpdateDeltaTime = _fixedUpdateDeltaTime;

			lastFixedUpdateCount = fixedUpateCount;

			var newMousePos = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
			_mouseDelta = new Vector2(Input.GetAxis("MouseAxis1"), Input.GetAxis("MouseAxis2"));

			if (newMousePos == _lastMousePos) {
				_mousePos += _mouseDelta;
			} else {
				_mousePos = newMousePos;
			}

			_lastMousePos = newMousePos;

			// normalize
			_mouseDelta.x /= Screen.width;
			_mouseDelta.y /= Screen.height;

			gameTimeDelta = Mathf.Min(Time.unscaledDeltaTime, 1f);
			_timeSinceStart += gameTimeDelta;

			if (!dedicatedServer && (EventSystem.current != null)) {
				InputManager.guiModalInput = (inMenus > 0) || modalInput/* || (Client.UI.MissionPrologue.mission != null)*/;
				InputManager.guiMouseFocused = EventSystem.current.IsPointerOverGameObject();
				InputManager.Tick(_timeSinceStart);
			}

			if (_pendingLevel != null) {
				if (_travelLevel == null) {
					if (_pendingLevel == "MainMenu") {
						TravelToMainMenu();
					} else {
						TravelToLevel();
						return;
					}
				}
			}

			if (_server != null) {
//#if UNITY_EDITOR
//				if (_server.gameMode.matchInProgress) {
//					if (_PIEClickToDropItem) {
//						_PIEClickToDropItem = false;
//						if (_PIETestDropItemClass != null) {
//							if (_PIEDropItemLocation != null) {
//								if (_PIEDropItemiLvl > 0) {
//									var id = _PIETestDropItemClass.GetiLvlID(_PIEDropItemiLvl);
//									int ilvl = 1;
//									if (id != MetaGame.TransientItemClass.TRANSIENT_ITEM_ID) {
//										ilvl = staticData.inventoryItemLibrary.GetIDiLvl(id);
//									}
//									for (int i = 0; i < _PIEDropItemCount; ++i) {
//										var pickup = _server.Spawn<Actors.ItemPickupActorServer>(null, default(SpawnParameters));
//										pickup.ServerInit(_PIEDropItemLocation.transform.position, _server.gameMode.players[0], _PIETestDropItemClass, ilvl, id, 1);
//									}
//								}
//							}
//						}
//					}
//				}
//#endif
				_server.Tick(Time.unscaledDeltaTime, this, ref serverNetStat[0], ref serverNetStat[1]);
			}

#if !DEDICATED_SERVER
			if (_client != null) {
				_client.Tick(Time.unscaledDeltaTime, this, ref clientNetStat[0], ref clientNetStat[1]);

				//if ((escapeMenu != null) && escapeMenu.gameObject.activeSelf && !dialogManager.dialogOpen) {
				//	if (InputManager.GetKeyState(InputKey.Escape).pressed) {
				//		InputManager.FlushKeyState(InputKey.Escape);
				//		Play(Vector3.zero, clientData.sounds.ui.widgets.buttonClick);
				//		CloseInGameEscapeMenu();
				//	}
				//}
			}
#endif

			MainThreadTaskQueue.Run();

			// don't exit until all web transactions have posted
			if (_serverQuitFlag) {
				if (activeTransactionCount <= 0) {
					_serverQuitFlag = false;
					Application.Quit();
				}
			}
		}

		void LateUpdate() {
			if (_server != null) {
				_server.LateUpdate(Time.unscaledDeltaTime, ref serverNetStat[0], ref serverNetStat[1]);
			}
			if (_client != null) {
				_client.LateUpdate();
			}

			if (netstat >= 0f) {
				netstat -= Time.unscaledDeltaTime;
				if (netstat <= 0f) {
					netstat = NETSTAT_FREQUENCY;

					if (_server != null) {
						Debug.LogWarning("netstat(server) reliable: " + serverNetStat[0] + ", unreliable: " + serverNetStat[1]);
						for (int i = 0; i < _server.clientConnections.Count; ++i) {
							var c = _server.clientConnections[i];
							Debug.LogWarning("netstat(server) connection " + c.connection.id + ": ping(" + c.ping + "ms)");
						}
					}

					if (_client != null) {
						if (_client.serverChannel != null) {
							Debug.LogWarning("netstat(client) ping(" + _client.serverChannel.ping + "ms) reliable: " + clientNetStat[0] + ", unreliable: " + clientNetStat[1]);
						} else {
							Debug.LogWarning("netstat(client) ping(0ms) reliable: " + clientNetStat[0] + ", unreliable: " + clientNetStat[1]);
						}
					}

					for (int i = 0; i < 2; ++i) {
						serverNetStat[i] = NetIOMetrics.zero;
						clientNetStat[i] = NetIOMetrics.zero;
					}
				}
			}

			if (fixedUpdateDidRun) {
				_fixedUpdateDeltaTime = 0f;
			}
		}

		void TravelToMainMenu() {
			Debug.Log("TravelToMainMenu");
#if !DEDICATED_SERVER
			//loadingScreenState.root.SetActive(false);
#endif
			UnloadGame();

			serverPerfTest = false;
			_pendingLevel = null;
			_travelLevel = "MainMenu";

			FreeTeamColorMaterials();

#if DEDICATED_SERVER
			SceneManager.LoadScene("Entry");
#else
			SceneManager.LoadScene("MainMenu");
#endif
		}

		void UnloadGame() {
			_asyncLoad = null;

			MainThreadTaskQueue.Flush();

			if (_client != null) {
				try {
					_client.DisconnectFromServer(EDisconnectReason.User);
					_client.Dispose();
				} catch (Exception e) {
					Debug.LogException(e);
				}
				_client = null;
			}

			if (_server != null) {
				try {
					_server.Dispose();
				} catch (Exception e) {
					Debug.LogException(e);
				}
				_server = null;
			}

			if (_netDriver != null) {
				try {
					_netDriver.Dispose();
				} catch (Exception e) {
					Debug.LogException(e);
				}
				_netDriver = null;
			}

			//escapeMenu = null;
			//_uma = null;
			//_umaGenerator = null;
		}

		void TravelToLevel() {
			Debug.Log("Traveling to " + _pendingLevel);
			GC();

#if UNITY_EDITOR
			_bIsPIE = false;
#endif

			_travelLevel = _pendingLevel;
			_pendingLevel = null;

			if (_server != null) {
				_server.BeginTravel(_travelLevel, _travelGameMode);
			}

			FreeTeamColorMaterials();

#if !DEDICATED_SERVER
			OpenLoadingScreen();
#endif
			SceneManager.LoadScene("Loading");
		}

		void LevelWasLoaded(Scene scene, LoadSceneMode mode) {
			if (instance != this) {
				return;
			}
			//if (Client.UI.MissionPrologue.mission != null) {
			//	return;
			//}

			var loaded = Utils.activeSceneName;
			if ((loaded == "MainMenu") || (loaded == "Loading")) {
				
				// finished loading scenes
				OnTravelFinished();
			}
		}

		public void StartAsyncTravel() {

			//int startIndex = travelLevel.IndexOf(".");
			//if (startIndex == -1) {
			//	throw new Exception(travelLevel + " is missing game mode extension!");
			//}

			//var mainScene = travelLevel.Substring(0, startIndex);

			//var first = SceneManager.LoadSceneAsync(mainScene);
			//if (first != null) {
			//	var second = SceneManager.LoadSceneAsync(travelLevel, LoadSceneMode.Additive);
			//	if (second != null) {
			//                 SetAsyncLevelLoadOperation(new AsyncSceneLoad(first, second));
			//		return;
			//	} else {
			//		Debug.LogError("Failed to load " + travelLevel);
			//	}
			//} else {
			//	Debug.LogError("Failed to load " + mainScene);
			//}

			var level = SceneManager.LoadSceneAsync(travelLevel);
			if (level != null) {
				SetAsyncLevelLoadOperation(new AsyncSceneLoad(level, null));
				return;
			}
						
			TravelToMainMenu();
		}
		
		void GetLoadingScreenElements() {
			//Assert.IsNotNull(loadingScreenState.root);
			//loadingScreenState.background = loadingScreenState.root.transform.Find("Panel").GetComponent<RawImage>();
			//loadingScreenState.text = loadingScreenState.root.transform.Find("Panel/Prompt").GetComponent<LoadingText>();
			//loadingScreenState.tips = loadingScreenState.root.transform.Find("Panel/Tips").GetComponent<LoadingTips>();
			//CaptureLoadingScreenCameraMask();
		}

		public void SetLoadingScreenText(string text) {
			//if (loadingScreenState.text != null) {
			//	loadingScreenState.text.text.text = Utils.GetLocalizedText(text);
			//}
		}

		void CaptureLoadingScreenCameraMask() {
			//loadingScreenState.mainCamera = GameObject.FindGameObjectWithTag(Tags.MainCamera).GetComponent<Camera>();
			//loadingScreenState.cameraElements = loadingScreenState.mainCamera.cullingMask;
		}

		void OpenLoadingScreen() {
			//loadingScreenState.root.SetActive(true);
			//loadingScreenState.text.Init();
			//loadingScreenState.tips.Init();

			var level = travelLevel;
#if UNITY_EDITOR
			if (_bIsPIE) {
				level = Utils.activeSceneName;
			}
#endif

			int startIndex = level.IndexOf(".");
			if (startIndex != -1) {
				level = level.Substring(0, startIndex);
			}

			//loadingScreenState.background.texture = Resources.Load<Texture2D>(level + "/LoadingScreen");
		}

		public void CloseLoadingScreen() {
			//loadingScreenState.root.SetActive(false);
			//loadingScreenState.background.texture = null;
			//loadingScreenState.mainCamera.cullingMask = loadingScreenState.cameraElements;
		}

		void CreateInGameUI() {
			//escapeMenu = GameObject.Instantiate(clientData.escapeMenuCanvasPrefab);
			//escapeMenu.gameObject.SetActive(false);
			//if ((_server != null) && (_server.gameMode is Server.Tutorial)) {
			//	escapeMenu.transform.FindChild("Canvas/Panel/OptionListPanel/Quit/Button/Quit_Text").GetComponent<TMPro.TextMeshProUGUI>().text = "Exit Tutorial";
			//}
		}

		public void OpenInGameEscapeMenu() {
			//if (!escapeMenu.gameObject.activeSelf) {
			//	++inMenus;
			//	escapeMenu.gameObject.SetActive(true);
			//	InputManager.ClearUIFocus();
			//}
		}

		public void CloseInGameEscapeMenu() {
			//if (escapeMenu.gameObject.activeSelf) {
			//	escapeMenu.Close();
			//	if (!escapeMenu.gameObject.activeSelf) {
			//		--inMenus;
			//	}
			//}
		}

		void OnTravelFinished() {
			Debug.Log("OnTravelFinished: " + Utils.activeSceneName);
#if LOGIN_SERVER
			loginServer.ResetTimeout();
#endif
			mainCamera = GameObject.FindGameObjectWithTag(Tags.MainCamera).GetComponent<Camera>();
			mainCamera.gameObject.SetActive(true);

			_pendingCommand = false;
			modalInput = false;
			inMenus = 0;

			// At this point the other scene has been unloaded
			// and all non-persistent game objects have been destroyed.

			if (Utils.activeSceneName != "Loading") {
				if (_travelLevel == "MainMenu") {
					MainMenuLoaded();
				} else if (_pendingLevel == null) {
					if (_client != null) {
						CreateInGameUI();
						CreateUMA();
					}

					if (_server != null) {
						_server.NotifySceneLoaded();
					}
					if (_client != null) {
						_client.NotifySceneLoaded();
					}

					if (_client != null) {
						//loadingScreenState.root.SetActive(true);
						CaptureLoadingScreenCameraMask();
                        //loadingScreenState.mainCamera.cullingMask = Layers.UIMask;
					}

#if BACKEND_SERVER
					if (prewarm) {
						Debug.Log("Prewarming complete, exiting.");
						Application.Quit();
					}
#endif
				}

				_travelLevel = null;
			} else {
				MainMenuUnloaded();
				StartCoroutine(CoCaptureLoadingScreen());	
			}

			LogMemStat();
			StartCoroutine(CoLogMemStat(2f));
		}

		IEnumerator CoLogMemStat(float delay) {
			yield return new WaitForSeconds(delay);
			LogMemStat();
		}

		IEnumerator CoCaptureLoadingScreen() {
			yield return null;
			if (_client != null) {
				CaptureLoadingScreenCameraMask();
			}
		}

#if UNITY_EDITOR
		[CFunc]
		static void LookupLevelName(string levelName, string gameType, int numPlayers, int numPlayersPerTeam) {
			var type = Type.GetType(gameType);
			if (type != null) {
				var lvl = GetLevelName(levelName, type, numPlayers, numPlayersPerTeam);
				Debug.Log(lvl);
			}
		}
#endif

		static string GetLevelName(string levelName, Type gameMode, int numPlayers, int numPlayersPerTeam) {
			var levelToLoad = levelName + "." + gameMode.FullName;
			string test;

			if (numPlayersPerTeam == 1) {
				test = levelToLoad + "." + numPlayers + "Players";
			} else {
				test = levelToLoad + "." + numPlayersPerTeam + "v" + numPlayersPerTeam;
			}

			var index = LEVELS.FindIndex((x) => x.name == levelName);
			if (index != -1) { // check for player count specialized sublevel.
				var lvl = LEVELS[index];
				for (int i = 0; i < lvl.sublevels.Length; ++i) {
					if (lvl.sublevels[i] == test) {
						return test;
					}
				}
			}
			return levelToLoad;
		}

		bool HostGame(string levelName, Type gameModeType, Type netDriverType, int port) {
			UnloadGame();

			Assert.IsNull(_server);
			Assert.IsNull(_client);
			Assert.IsNull(_netDriver);

			_pendingCommand = true;

			string serverName;
#if BACKEND_SERVER
			if (telemetry != null) {
				serverName = "DH Game Server " + telemetry.gameID + " - " + telemetry.guid;
			} else {
				serverName = "Telemetry prewarm";
			}
#else
			serverName = onlineLocalPlayer.name + "'s Server";
#endif

			var asms = GetModuleAssemblies();
			_netDriver = (INetDriver)System.Activator.CreateInstance(netDriverType);

			_server = new Bowhead.Server.ServerWorld(this, dedicatedServer ? staticData.serverTerrainChunkComponent : clientData.clientTerrainChunkComponent, _serverObjectGroup.transform, serverName, null, asms, _netDriver);

			if (!dedicatedServer) {
				_client = new Bowhead.Client.ClientWorld(this, _server != null ? _server.worldStreaming : null, clientData.clientTerrainChunkComponent, _clientObjectGroup.transform, asms, _netDriver);
			}

			if (!_server.Listen(port, numPlayers)) {
				Debug.LogError("Unable to start game server on port " + port);
				if (_client != null) {
					_client.Dispose();
					_client = null;
				}
				_server.Dispose();
				_server = null;
				_netDriver.Dispose();
				_netDriver = null;
				return false;
			}

			if (!dedicatedServer) {
				_client.Connect("localhost", port);
			}

			_pendingLevel = levelName;
			_travelGameMode = gameModeType;
			TravelToLevel();
			return true;
		}

		public bool JoinGame(string ip, Type netDriverType) {
			if (dedicatedServer) {
				return false;
			}

			UnloadGame();
			PrecacheClientData();
			_pendingCommand = true;

			Assert.IsNull(_server);
			Assert.IsNull(_client);
			Assert.IsNull(_netDriver);

			int port = 7777;

			if (ip.Contains(":")) {
				var parts = ip.Split(':');
				ip = parts[0];
				port = int.Parse(parts[1]);
			}

			var asms = GetModuleAssemblies();
			_netDriver = (INetDriver)Activator.CreateInstance(netDriverType);

			_client = new Client.ClientWorld(this, null, clientData.clientTerrainChunkComponent, _clientObjectGroup.transform, asms, _netDriver);
			if (!_client.Connect(ip, port)) {
				Debug.LogError("Failed to connect to: " + ip + ":" + port);
				_client.Dispose();
				_client = null;
				_netDriver.Dispose();
				_netDriver = null;
				_pendingCommand = false;
                return false;
			}
			return true;
		}

#if UNITY_EDITOR
		void StartPIE() {
#if LOGIN_SERVER
			throw new System.Exception("PIE does not work with the login server enabled! Please disable it under Bowhead->Editor Settings->Enable Login Server.");
#else
			Assert.IsNull(_server);
			Assert.IsNull(_client);
			Assert.IsNull(_netDriver);

			matchTime = PIEMatchTime;
			matchOvertime = PIEOvertime;
			numPlayers = Mathf.Max(1, PIENumPlayers);

			_pendingCommand = true;
#if !DEDICATED_SERVER
			dialogManager = new Client.UI.DialogManager();
#endif
			if (!PIEServerOnly) {
				PrecacheClientData();
				LoadClientSettings();
			}

			var asms = GetModuleAssemblies();

			if ((PIENumPlayers > 1) || PIEServerOnly) {
				_netDriver = new SocketNetDriver();
			} else {
				_netDriver = new LocalGameNetDriver();
			}

			_server = new Server.ServerWorld(this, PIEServerOnly ? staticData.serverTerrainChunkComponent : clientData.clientTerrainChunkComponent, _serverObjectGroup.transform, "PIEServer", null, asms, _netDriver);
			if (!PIEServerOnly) {
				_client = new Client.ClientWorld(this, _server != null ? _server.worldStreaming : null, clientData.clientTerrainChunkComponent, _clientObjectGroup.transform, asms, _netDriver);
			}

			var gameMode = Type.GetType(PIEGameMode);

			_server.Listen(7777, 8);
			if (!PIEServerOnly) {
				_client.Connect("127.0.0.1", 7777);
			}
			_server.BeginTravel(Utils.loadedSubLevelName, gameMode);

			if (!PIEServerOnly) {
				CreateInGameUI();
				CreateUMA();
			}

			_server.NotifySceneLoaded();

			if (!PIEServerOnly) {
				_client.NotifySceneLoaded();
				OpenLoadingScreen();
				//loadingScreenState.mainCamera.cullingMask = Layers.UIMask;
			}
#endif
		}
#endif

		public void SetAsyncLevelLoadOperation(World.IAsyncSceneLoad asyncOp) {
			_asyncLoad = asyncOp;

			if (_server != null) {
				_server.SetAsyncLoadOperation(asyncOp);
			}
			if (_client != null) {
				_client.SetAsyncLoadOperation(asyncOp);
			}
		}


		void PrecacheClientData() {
#if !DEDICATED_SERVER
			if (clientData == null) {
				clientData = Resources.Load<ClientData>("ClientData");

				for (int i = 0; i < staticData.indexedObjects.Length; ++i) {
					var indexed = (StaticData.Indexed)staticData.indexedObjects[i];
                    if (indexed != null) {
						indexed.ClientPrecache();
					}
				}

				staticData.physicalContactMatrix.ClientPrecache();

				//var loadingCanvas = Instantiate(clientData.loadingCanvasPrefab);
				//loadingCanvas.name = "LoadingCanvas";
				//loadingCanvas.transform.SetParent(transform, false);
				//loadingCanvas.transform.SetSiblingIndex(1);
				//loadingCanvas.SetActive(false);

				//loadingScreenState.root = loadingCanvas;
				GetLoadingScreenElements();

				
			}
#endif
		}

		void LoadClientSettings() {
#if !DEDICATED_SERVER
			ragdollLimit = UserPrefs.instance.GetInt("Gameplay.MaxRagdolls", 25);
			gibLimit = UserPrefs.instance.GetInt("Gameplay.MaxGibs", 25);
			uiScale = UserPrefs.instance.GetFloat("UIScale", 0);
			physicsRate = UserPrefs.instance.GetInt("PhysicsRate", 0);

			graphicsSettings = new Client.GraphicsSettings();

			if (UserPrefs.instance.GetInt("RunVersion", 0) != SETTINGS_VERSION) {
				UserPrefs.instance.SetInt("RunVersion", SETTINGS_VERSION);
				graphicsSettings.Detect();
				graphicsSettings.screenWidth = 1280;
				graphicsSettings.screenHeight = 720;
				graphicsSettings.fullscreen = false;
				graphicsSettings.Apply();
				graphicsSettings.Save();
				UserPrefs.instance.Save();
			} else {
				graphicsSettings.Apply();
			}
#endif
		}

		void OnDestroy() {

			destroying = true;

			UnloadGame();

			if (_instance == this) {
				World.Streaming.StaticShutdown();
			}

			SceneManager.sceneLoaded -= LevelWasLoaded;

			FreeTeamColorMaterials();

#if !DEDICATED_SERVER && LOGIN_SERVER
			if (loginServer != null) {
				loginServer.Dispose();
				loginServer = null;
			}
#endif

#if !DEDICATED_SERVER
			if (onlineServices != null) {
				onlineServices.Dispose();
				onlineServices = null;
			}
#endif

#if BACKEND_SERVER
			if (telemetry != null) {
				telemetry.Dispose();
				telemetry = null;
			}
#endif
		}

		public Material InstanceTeamColorMaterial(Material master, int hashID, Color primary, Color secondary) {
			Dictionary<int, Material> dict;
			if (!_teamColorMaterials.TryGetValue(master, out dict)) {
				dict = new Dictionary<int, Material>();
				_teamColorMaterials[master] = dict;
			}

			Material instance;
			if (!dict.TryGetValue(hashID, out instance)) {
				instance = GameObject.Instantiate(master);
				instance.SetColor("_TeamPrimaryColor", primary);
				instance.SetColor("_TeamSecondaryColor", secondary);
				dict[hashID] = instance;
			}

			return instance;
		}

		void FreeTeamColorMaterials() {
			foreach (var dict in _teamColorMaterials.Values) {
				foreach (var m in dict.Values) {
					GameObject.Destroy(m);
				}
			}
			_teamColorMaterials.Clear();
		}

		List<RaycastResult> _guiRaycast;

		void OnGUI() {
			//var ev = Event.current;
			//if (InputManager.guiFocused && (ev.type == EventType.ScrollWheel)) {
			//	var lp = Client.Actors.ClientPlayerController.localPlayer;
			//	if ((lp != null) && lp.gameState.isUnitTrading) {
			//		PointerEventData data = new PointerEventData(EventSystem.current);
			//		data.position = Input.mousePosition;

			//		if (_guiRaycast == null) {
			//			_guiRaycast = new List<RaycastResult>();
			//		}

			//		EventSystem.current.RaycastAll(data, _guiRaycast);

			//		var discard = false;

			//		if (_guiRaycast.Count > 0) {
			//			for (int i = 0; i < _guiRaycast.Count; ++i) {
			//				var item = _guiRaycast[i];
			//				if (item.gameObject != null) {
			//					discard = !item.gameObject.CompareTag(Tags.Minimap);
			//					break;
			//				}
			//			}
			//			_guiRaycast.Clear();
			//		}

			//		if (discard) {
			//			return;
			//		}
			//	}
			//}
			InputManager.ProcessEvent(Event.current);
		}

#if UNITY_EDITOR
		void Reset() {
			PIEGameMode = typeof(Server.BowheadGame).AssemblyQualifiedName;
			staticData = GetComponent<StaticData>();
			PIEMatchTime = 60*5;
			PIEOvertime = 90;
		}
#endif

		void LogCallback(string message, string stackTrace, LogType type) {
			Console.Print(type, message);
		}

		void ConsoleExecutor(string command) {
			string[] args;
			var cfuncMethod = ParseConsoleCommand(command, out args);

			if (cfuncMethod != null) {
				InvokeConsoleCommand(cfuncMethod, args, command, null);
			} else {
				Debug.LogError(args[0] + " is an unrecognized command.");
			}
		}

		List<string> ConsoleTabCompleter(string command) {
			List<string> matches = new List<string>();

			command = command.ToLower();

			foreach (var cfuncMethod in cvarMethods.Values) {
				{
					var lower = cfuncMethod.method.Name.ToLower();
					if (lower.StartsWith(command)) {
						if (!matches.Contains(lower)) {
							matches.Add(lower);
						}
					}
				}
				var shortcuts = cfuncMethod.cfunc.Shortcuts;
				if (shortcuts != null) {
					foreach (var s in shortcuts) {
						var lower = s.ToLower();
						if (lower.StartsWith(command)) {
							if (!matches.Contains(lower)) {
								matches.Add(lower);
							}
						}
					}
				}
			}

			matches.Sort((a, b) =>
				(a.Length < b.Length) ? -1 :
				(a.Length > b.Length) ? 1 :
				a.CompareTo(b)
			);
			return matches;
		}

		public void InvokeConsoleCommand(CFuncMethod cfuncMethod, string[] args, string command, Bowhead.Server.Actors.ServerPlayerController serverPlayer) {
			string error;
			var parms = Console.TryParseCFuncArguments(cfuncMethod.method, args, out error);
			if (error != null) {
				Debug.LogError(error);
				Debug.LogError("Usage: " + Console.GetFuncPrototype(cfuncMethod));
				return;
			}

			object retVal = null;

			if (cfuncMethod.method.IsStatic && ((serverWorld != null) || !cfuncMethod.cfunc.IsServer)) {
				retVal = cfuncMethod.method.Invoke(null, parms);
			} else if (cfuncMethod.cfunc.IsServer) {
				if (serverPlayer != null) {
					object context = null;

					if (cfuncMethod.method.DeclaringType.IsAssignableFrom(typeof(Bowhead.Server.Actors.ServerPlayerController))) {
						context = serverPlayer;
					}

					if (context == null) {
						Debug.LogError("Non static CFunc server methods must be located inside the PlayerController class and must be executed from a client!");
					}

					try {
						retVal = cfuncMethod.method.Invoke(context, parms);
					} catch (Exception e) {
						Debug.LogException(e);
					}
				} else {
					if (command.Length > 1024) {
						Debug.LogError("Server CFunc command exceeds max length of 1024, cannot execute on server.");
					} else {
						var localPlayer = Bowhead.Client.Actors.ClientPlayerController.localPlayer;
						if (localPlayer == null) {
							Debug.LogError("You must be playing a game to execute server CFuncs.");
							return;
						}
						localPlayer.ExecuteServerCFunc(cfuncMethod, command);
					}
				}
			} else {
				object context = null;

				if (cfuncMethod.method.DeclaringType.IsAssignableFrom(typeof(Bowhead.Client.Actors.ClientPlayerController))) {
					context = Bowhead.Client.Actors.ClientPlayerController.localPlayer;
				}

				if (context == null) {
					Debug.LogError("You must be in a game to do that.");
					return;
				}

				try {
					retVal = cfuncMethod.method.Invoke(context, parms);
				} catch (Exception e) {
					Debug.LogException(e);
				}
			}

			if (retVal != null) {
				Debug.Log(args[0] + " is " + retVal);
			}
		}

		public void ExecuteConsoleCommand(string command) {
			ConsoleExecutor(command);
		}

		public CFuncMethod ParseConsoleCommand(string command, out string[] tokens) {
			var result = Console.Tokenize(command);

			if (result.Count > 0) {
				CFuncMethod cvarMethod;
				if (cvarMethods.TryGetValue(result[0].ToLower(), out cvarMethod)) {
					tokens = result.ToArray();
					return cvarMethod;
				}
			}

			tokens = new[] { command };
			return null;
		}

		public CFuncMethod FindConsoleCommand(string command) {
			CFuncMethod cvarMethod;
			if (cvarMethods.TryGetValue(command.ToLower(), out cvarMethod)) {
				return cvarMethod;
			}
			return null;
		}

		[CFunc]
		static void CVarList() {
			if (instance != null) {
				Console.PrintCFuncList(instance.cvarMethods);
			}
		}

		[CFunc]
		static void VidModeList() {
			var resolutions = Screen.resolutions;

			for (int i = 0; i < resolutions.Length; ++i) {
				var r = resolutions[i];
				Debug.LogWarning(i + ":= " + r);
			}
		}

		[CFunc]
		static void SetScreenSize(int w, int h) {
			Screen.SetResolution(w, h, Screen.fullScreen);
		}

		[CFunc(Shortcuts = new[] { "vm" })]
		static void SetVidMode(params object[] args) {
			if (args.Length > 0) {
#if UNITY_EDITOR
				Debug.LogError("Video modes aren't supported in the Unity Editor.");
#else
				var resolutions = Screen.resolutions;
                int index = int.Parse((string)args[0]);
				if ((index < 0) || (index >= resolutions.Length)) {
					Debug.LogError("That is not a valid video mode index.");
				} else {
					var r = resolutions[index];
					Screen.SetResolution(r.width, r.height, Screen.fullScreen, Screen.fullScreen ? r.refreshRate : 0);
				}
#endif
			} else {
				Debug.LogWarning("vidmode is currently " + (Screen.fullScreen ? Screen.currentResolution.ToString() : (Screen.width + " x " + Screen.height + " @ " + Screen.currentResolution.refreshRate + "Hz")));
			}
		}

		[CFunc(Shortcuts = new[] { "fullscreen" })]
		static void SetFullscreen(params object[] args) {
			if (args.Length > 0) {
#if UNITY_EDITOR
				Debug.LogError("Video modes aren't supported in the Unity Editor.");
#else
				bool val = false;
				try {
					val = bool.Parse((string)args[0]);
				} catch {
					val = int.Parse((string)args[0]) != 0;
				}
				Screen.fullScreen = val;
#endif
			} else {
				Debug.LogWarning("fullscreen is currently " + (Screen.fullScreen ? "1" : "0"));
			}
		}

		static void CheckLogin() {
#if !DEDICATED_SERVER && LOGIN_SERVER
			if (instance.loginServer.state != LoginServer.EConnectionState.Connected) {
				throw new Exception("Not logged into Bowhead login servers!");
			}
#endif
		}

		[CFunc]
		static void Host(string mapName, int port, int numPlayers) {
			CheckLogin();

			if ((numPlayers < 1) || (numPlayers > 8)) {
				throw new Exception("Player count must be between 1 and 8");
			}

			//if (LEVELS.FindIndex((x) => x.name == mapName) == -1) {
			//	throw new Exception(mapName + " is not a valid map");
			//}

			Type gameModeType = typeof(Bowhead.Server.BowheadGame);
			//if (gameModeType == null) {
			//	var s = "Bowhead.Server." + gameMode;
			//	gameModeType = Type.GetType(s);
			//	if (gameModeType == null) {
			//		throw new Exception("The game mode " + gameMode + " could not be found, tried '" + gameMode + "' and '" + s + "'");
			//	}
			//}

			if (instance._pendingCommand) {
				throw new Exception("There is already a command running.");
			}

			instance.matchTime = 0;

			instance._pendingCommand = true;
			instance.StartCoroutine(instance.CoHost(mapName, port, numPlayers, gameModeType));
		}

		IEnumerator CoHost(string mapName, int port, int numPlayers, Type gameModeType) {

			this.numPlayers = numPlayers;

			if (_guiStatus != null) {
				_guiStatus.color = Color.white;
				_guiStatus.text = "Starting " + mapName + "...";
				yield return null;
			}

			Type netDriver = (dedicatedServer || (numPlayers > 1)) ? typeof(SocketNetDriver) : typeof(LocalGameNetDriver);
			if (HostGame(mapName, gameModeType, netDriver, port)) {
				Console.CloseImmediate();
			} else {
				_pendingCommand = false;
				if (_guiStatus != null) {
					_guiStatus.text = string.Empty;
				}
			}
		}

//		public void PlayTutorial(bool mp) {
//			Client.UI.MessageBox.OKCancel(Client.UI.MessageBox.EType.Question, Utils.GetLocalizedText("UI.MessageBox.Tutorial.Title"), Utils.GetLocalizedText(mp ? "UI.MessageBox.Tutorial.LobbyPrompt" : "UI.MessageBox.Tutorial.Prompt"), Client.UI.ModalDialog.EMaskEffect.Darken, (Client.UI.ModalDialog.EResult r) => {
//				if (r == Client.UI.ModalDialog.EResult.OK) {
//#if LOGIN_SERVER
//					if ((loginServer.lobbies != null) && (loginServer.lobbies.lobby != null)) {
//						loginServer.lobbies.lobby.Leave();
//					}
//					if ((loginServer.rooms != null) && (loginServer.rooms.mainRoom != null)) {
//						loginServer.LeaveMainRoom();
//					}
//#endif
//					numPlayers = 1;
//					dedicatedServer = false;
//					HostGame("AttitudeAdjuster", 1, typeof(Server.Tutorial), typeof(LocalGameNetDriver), 0);
//				}
//			});
//		}

		[CFunc]
		public static void Connect(string address) {
			CheckLogin();

			if (instance._pendingCommand) {
				throw new Exception("There is already a command running.");
			}

			instance._pendingCommand = true;
			instance.StartCoroutine(instance.CoConnect(address));
		}

		IEnumerator CoConnect(string address) {
			Debug.Log("Traveling to " + address);
			if (_guiStatus != null) {
				_guiStatus.color = Color.white;
				_guiStatus.text = "Connecting to " + address + " ...";
				yield return null;
			}

			if (JoinGame(address, typeof(SocketNetDriver))) {
				Console.CloseImmediate();
			} else {
				_pendingCommand = false;
				if (_guiStatus != null) {
					_guiStatus.text = string.Empty;
				}
			}
		}

		[CFunc]
		static bool Profile(params object[] args) {
			if (args.Length == 1) {
				UnityEngine.Profiling.Profiler.logFile = (string)args[0];
				UnityEngine.Profiling.Profiler.enableBinaryLog = true;
				UnityEngine.Profiling.Profiler.enabled = true;
				return true;
			} else {
				UnityEngine.Profiling.Profiler.enabled = false;
				return false;
			}
		}

		[CFunc]
		static void SVPerfTest(string mapName, int port, int numPlayers, int teamSize, string gameMode) {
			CheckLogin();

			if (LEVELS.FindIndex((x) => x.name == mapName) == -1) {
				throw new Exception(mapName + " is not a valid map");
			}

			Type gameModeType = Type.GetType(gameMode);
			if (gameModeType == null) {
				var s = "Bowhead.Server." + gameMode;
				gameModeType = Type.GetType(s);
				if (gameModeType == null) {
					throw new Exception("The game mode " + gameMode + " could not be found, tried '" + gameMode + "' and '" + s + "'");
				}
			}

			if (instance._pendingCommand) {
				throw new Exception("There is already a command running.");
			}

			instance._pendingCommand = true;
			instance.StartCoroutine(instance.CoSVPerfTest(mapName, teamSize, port, numPlayers, gameModeType));
		}

		IEnumerator CoSVPerfTest(string mapName, int port, int numPlayers, int teamSize, Type gameModeType) {

			this.numPlayers = numPlayers;
			serverPerfTest = true;

			if (_guiStatus != null) {
				_guiStatus.color = Color.white;
				_guiStatus.text = "Starting " + mapName + "...";
				yield return null;
			}

			Type netDriver = (dedicatedServer || (numPlayers > 1)) ? typeof(SocketNetDriver) : typeof(LocalGameNetDriver);
			if (HostGame(mapName, gameModeType, netDriver, port)) {
				Console.CloseImmediate();
			} else {
				_pendingCommand = false;
				if (_guiStatus != null) {
					_guiStatus.text = string.Empty;
				}
			}
		}

		[CFunc]
		static void FPS() {
			if (instance._fpsCounter.OperationMode == OperationMode.Background) {
				instance._fpsCounter.OperationMode = OperationMode.Normal;
				Debug.LogWarning("fps on");
			} else {
				instance._fpsCounter.OperationMode = OperationMode.Background;
				Debug.LogWarning("fps off");
			}
		}

		[CFunc]
		static void Quit() {
			if ((instance._client != null) || (instance._server != null)) {
				instance.SetPendingLevel("MainMenu", null);
				Console.Close();
				return;
			}
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}

		[CFunc]
		static void NetStat() {
			if (instance.netstat >= 0f) {
				instance.netstat = -1f;
				Debug.LogWarning("netstat disabled.");
			} else {
				instance.netstat = NETSTAT_FREQUENCY;
				for (int i = 0; i < 2; ++i) {
					instance.clientNetStat[i] = NetIOMetrics.zero;
					instance.serverNetStat[i] = NetIOMetrics.zero;
				}
				Debug.LogWarning("netstat enabled.");
			}
		}

		[CFunc]
		static void GC() {
			System.GC.Collect();
			Resources.UnloadUnusedAssets();
			MemStat();
			Actor.DumpLeakedActors();
		}

		[CFunc]
		static void MemStat() {
			instance.LogMemStat();
		}

		[CFunc]
		static void ShaderLOD(params object[] args) {
			if (args.Length > 0) {
				Shader.globalMaximumLOD = int.Parse((string)args[0]);
			}

			Debug.LogWarning("ShaderLOD is " + Shader.globalMaximumLOD);			
		}

#if LEAK_TRACKER
		[CFunc]
		static void DumpLeaks() {
			instance._leakTracker.WriteCSV("leaks.txt", true);
		}

		[CFunc]
		static void DumpObjects() {
			instance._leakTracker.WriteCSV("objects.txt", false);
		}
#endif

		public void SetPendingLevel(string level, Type gameMode) {
			if (_travelLevel == null) {
				Debug.Log("SetPendingLevel: " + level);
				_pendingLevel = level;
				_travelGameMode = gameMode;
#if !DEDICATED_SERVER
				dialogManager = new Client.UI.DialogManager();
#endif
			}
		}

		public AudioSource Play(Actor instigator, SoundCue sound) {
			if (!(dedicatedServer || batchMode)) {
				return _soundManager.Play(instigator, sound, timeSinceStart, randomNumber, randomNumber, randomNumber, randomNumber, randomNumber);
			}
			return null;
		}

		public AudioSource Play(Vector3 position, SoundCue sound) {
			if (!(dedicatedServer || batchMode)) {
				return _soundManager.Play(position, sound, timeSinceStart, randomNumber, randomNumber, randomNumber, randomNumber, randomNumber);
			}
			return null;
		}

		public AudioSource Play(GameObject instigator, SoundCue sound) {
			if (!(dedicatedServer || batchMode)) {
				return _soundManager.Play(instigator, sound, timeSinceStart, randomNumber, randomNumber, randomNumber, randomNumber, randomNumber);
            }
			return null;
		}

		public static GameManager instance {
			get {
				return _instance;
			}
		}

		public string travelLevel {
			get {
				return _travelLevel;
			}
		}

		public string pendingLevel {
			get {
				return _pendingLevel;
			}
		}

		public AFPSCounter fpsCounter {
			get {
				return _fpsCounter;
			}
		}

		// Absolute time since application was started.
		public double timeSinceStart {
			get {
				return _timeSinceStart;
			}
		}

		// Absolute game time, does not advance when game is paused.
		public double gameTimeSinceStart {
			get {
				return _timeSinceStart;
			}
		}

		public float gameTimeDelta {
			get;
			private set;
		}

		public Client.ClientWorld clientWorld {
			get {
				return _client;
			}
		}

		public Server.ServerWorld serverWorld {
			get {
				return _server;
			}
		}

		public float travelProgress {
			get {
				if (_server != null) {
					return _server.travelProgress;
				}
				if (_client != null) {
					return _client.travelProgress;
				}
				return 0f;
			}
		}

		public float randomNumber {
			get {
				return staticData.randomNumberTable.randomValue;
			}
        }

		public float signedRandomNumber {
			get {
				return (randomNumber-0.5f)*2f; ;
			}
		}

		public int RandomRange(int min, int maxExclusive) {
			return Utils.LerpRange(min, maxExclusive, randomNumber);
		}

		public float RandomFromIndex(uint index) {
			return staticData.randomNumberTable.RandomFromIndex(index);
		}

		public float SignedRandomFromIndex(uint index) {
			return (RandomFromIndex(index) - 0.5f) * 2f;
		}

		public Color RandomColor() {
			return new Color(randomNumber, randomNumber, randomNumber, 1f);
		}

		public Color RandomColorIndex(uint index) {
			return new Color(RandomFromIndex(index), RandomFromIndex(index+1), RandomFromIndex(index+2), 1f);
		}

		public int inMenus {
			get {
#if DEDICATED_SERVER
				return 0;
#else
				return _inMenus + (dialogManager.dialogOpen ? 1 : 0);
#endif
			}
			set {
				_mouseDelta = Vector2.zero;

				_inMenus = value;

				if ((_inMenus > 0) || (Client.Actors.ClientPlayerController.localPlayer != null)) {
					SetScreenModeCursorLockState();
					Cursor.visible = true;
				} else if (Client.Actors.ClientPlayerController.localPlayer != null) {
					Client.Actors.ClientPlayerController.localPlayer.SetCursorState();
				}
			}
		}

		public bool modalInput {
			get;
			set;
		}

		public void SetScreenModeCursorLockState() {
#if !UNITY_EDITOR
			if (Screen.fullScreen) {
				Cursor.lockState = CursorLockMode.Confined;
			} else
#endif
			{
				Cursor.lockState = CursorLockMode.None;
			}
		}

		public void ClearMouseDelta() {
			_mouseDelta = Vector2.zero;
		}

		public void LogMemStat() {
			Debug.Log("Mem usage: " + memStat);
		}

		public void OnApplicationFocus(bool focus) {
			applicationFocused = focus;
		}

		public bool applicationFocused {
			get;
			private set;
		}

		public Vector2 mousePosition {
			get {
				return _mousePos;
			}
		}

		public Vector2 mouseDelta {
			get {
				return _mouseDelta;
			}
		}

		public ClientData clientData {
			get;
			private set;
		}

		public Transform tooltipCanvas {
			get {
				return _tooltipCanvas;
			}
		}

		public bool dedicatedServer {
			get;
			private set;
		}

		public bool serverPerfTest {
			get;
			private set;
		}

		public bool batchMode {
			get;
			private set;
		}

		public string fpsStat {
			get {
				var fps = _fpsCounter.fpsCounter.LastValue;
				var avg = _fpsCounter.fpsCounter.LastAverageValue;
				var millis = _fpsCounter.fpsCounter.LastMillisecondsValue;
				var min = _fpsCounter.fpsCounter.LastMinimumValue;
				var max = _fpsCounter.fpsCounter.LastMaximumValue;
				return string.Format("{0} fps | {1} ms | {2} avg | [{3}/{4}]", fps, millis, avg, min, max);
			}
		}

		public string memStat {
			get {
				var total = _fpsCounter.memoryCounter.LastTotalValue;
				var alloc = _fpsCounter.memoryCounter.LastAllocatedValue;
				var mono = _fpsCounter.memoryCounter.LastMonoValue;
				return string.Format("{0:0.00} total | {1:0.00} alloc | {2:0.00} mono", total/1048576f, alloc/1048576f, mono/1048576f);
			}
		}

		[NonSerialized]
		public int matchTime;
		[NonSerialized]
		public int matchOvertime;
		[NonSerialized]
		public int numPlayers;
		[NonSerialized]
		public int maxWaitTime;

		public Camera mainCamera {
			get;
			private set;
		}

		public bool inMainMenu {
			get;
			private set;
		}

		[NonSerialized]
		public ulong challenge;

		public bool destroying {
			get;
			private set;
		}

		bool didEverLoadMainMenu;

		[NonSerialized]
		public int activeTransactionCount;

		public void ServerQuit() {
			_serverQuitFlag = true;
		}

		public bool isServer => _server != null;
		public bool isClient => _client != null;
		public bool isDedicatedServer => isServer && !isClient;
		global::Server.ServerWorld IGameInstance.serverWorld => _server;
		
#if UNITY_EDITOR
		public bool debugDrawAI {
			get;
			private set;
		}

		[CFunc(Shortcuts = new[] { "ddai" })]
		static void DebugDrawUnitClustering() {
			instance.debugDrawAI = !instance.debugDrawAI;
			if (instance.debugDrawAI) {
				Debug.Log("debugDrawAI is on");
			} else {
				Debug.Log("debugDrawAI is off");
			}
		}
#endif

#if !DEDICATED_SERVER
		public Client.UI.DialogManager dialogManager {
			get;
			private set;
		}

		public Client.GraphicsSettings graphicsSettings {
			get;
			private set;
		}

		//public MetaGame.PlayerInventorySkills clientInventory {
		//	get;
		//	private set;
		//}
#endif

#if BACKEND_SERVER
		public Telemetry.Telemetry telemetry {
			get;
			private set;
		}

		public int hostedPort {
			get {
				return _startingPort;
			}
		}
		public bool prewarm {
			get;
			private set;
		}
#endif
		
#if (STEAM_API && UNITY_EDITOR) || BACKEND_SERVER
		public void EnqueueSteamWebCommand(Func<IEnumerator> action) {
			_steamWebCommands.Add(action);
		}

		IEnumerator CORunSteamWebCommands() {
			for (;;) {
				while (_steamWebCommands.Count < 1) {
					yield return null;
				}
				var cmd = _steamWebCommands[0];
				_steamWebCommands.RemoveAt(0);

				var startTime = Time.unscaledTime;

				yield return cmd();

				var endTime = Time.unscaledTime;
				var dt = endTime - startTime;

				// max of 8 calls/sec
				while (dt < 0.125f) {
					yield return null;
					dt += Time.unscaledDeltaTime;
				}
			}
		}
#endif

#if !DEDICATED_SERVER
#if LOGIN_SERVER
		public LoginServer.LoginServer loginServer {
			get;
			private set;
		}

		public void LoginServerConnected() {
			if (_guiStatus != null) {
				_guiStatus.color = Color.yellow;
				_guiStatus.text = Utils.GetLocalizedText("UI.MainMenu.Online");
			}
			if (mainMenu != null) {
				mainMenu.LoginServerConnected();
			}
		}

		public void LoginServerConnecting() {
			if (_guiStatus != null) {
				_guiStatus.color = Color.white;
				_guiStatus.text = Utils.GetLocalizedText("UI.MainMenu.Connecting");
			}
		}

		public void LoginServerDisconnected(LoginServer.NetMsgs.EPlayerDisconnectReason reason, string errMsg) {
			Debug.LogError("Disconnected from login server - " + reason.ToString() + ((errMsg != null ? (" - " + errMsg) : string.Empty)));
			if (destroying) {
				return;
			}
			if (_guiStatus != null) {
				_guiStatus.color = Color.red;
				if (reason == LoginServer.NetMsgs.EPlayerDisconnectReason.WrongVersion) {
					_guiStatus.text = Utils.GetLocalizedText("UI.MessageBox.UpdateRequiredTitle");
				} else {
					_guiStatus.text = Utils.GetLocalizedText("UI.MainMenu.Offline");
				}
			}
			if (mainMenu != null) {
				mainMenu.LoginServerDisconnected();
				menuManager.UpdateMenuSelection(0);

				if (reason == LoginServer.NetMsgs.EPlayerDisconnectReason.WrongVersion) {
					Client.UI.MessageBox.OK(
						Client.UI.MessageBox.EType.Error,
						Utils.GetLocalizedText("UI.MessageBox.UpdateRequiredTitle"),
						string.Format(Utils.GetLocalizedText("UI.MessageBox.UpdateRequiredMsg"), errMsg),
						Client.UI.ModalDialog.EMaskEffect.Darken,
						null
					);
				} else if (reason == LoginServer.NetMsgs.EPlayerDisconnectReason.AuthenticationFailed) {
					Client.UI.MessageBox.OK(
						Client.UI.MessageBox.EType.Error,
						Utils.GetLocalizedText("UI.MessageBox.AuthenticationFailedTitle"),
						string.Format(Utils.GetLocalizedText("UI.MessageBox.AuthenticationFailedMsg"), errMsg),
						Client.UI.ModalDialog.EMaskEffect.Darken,
						null
					);
				} else if (reason == LoginServer.NetMsgs.EPlayerDisconnectReason.Shutdown ||
						   reason == LoginServer.NetMsgs.EPlayerDisconnectReason.ShutdownInProgress) {
					Client.UI.MessageBox.OK(
						Client.UI.MessageBox.EType.Error,
						Utils.GetLocalizedText("UI.MessageBox.ShutdownTitle"),
						string.Format(Utils.GetLocalizedText("UI.MessageBox.ShutdownMsg"), errMsg),
						Client.UI.ModalDialog.EMaskEffect.Darken,
						null
					);
				} else {
					Client.UI.MessageBox.OK(
						Client.UI.MessageBox.EType.Error,
						Utils.GetLocalizedText("UI.MessageBox.LoginServerErrorTitle"),
						string.Format(Utils.GetLocalizedText("UI.MessageBox.LoginServerErrorMsg"), errMsg),
						Client.UI.ModalDialog.EMaskEffect.Darken,
						null
					);
				}
			}
		}
#endif

		string onlineLoginError;

		void OnlinePlayerLoggedIn(Online.OnlineLocalPlayer player, string errorMsg) {
			if (player != null) {
				onlineLocalPlayer = player;
				//clientInventory = new MetaGame.PlayerInventorySkills(player.id.uuid, MetaGame.PlayerInventorySkills.API.Client);
				Debug.Log("Online user " + player.id.uuid + " logged in.");
			} else if (errorMsg != null) {
				onlineLoginError = errorMsg;
            }
		}

		public Online.OnlineLocalPlayer onlineLocalPlayer {
			get;
			private set;
		}

		public Online.OnlineServices onlineServices {
			get;
			private set;
		}
#endif
	}
}