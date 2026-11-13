using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Convoy
{
    public sealed class ConvoyRunnerLink : MonoBehaviour
    {
        public ConvoyMultiplayer session;
    }

    public sealed class ConvoyMultiplayer : MonoBehaviour, INetworkRunnerCallbacks
    {
        public NetworkObject worldPrefab, entityPrefab, inputPrefab;
        public ConvoyNetworkInput Input { get; set; }
        public readonly List<ConvoyNetworkInput> inputs = new List<ConvoyNetworkInput>();
        public ConvoyPrototype Game { get; private set; }
        public NetworkRunner Runner { get; private set; }
        public ConvoyNetworkWorld World { get; set; }
        public bool ShowLobby;
        public bool Connected => Runner != null && Runner.IsRunning;
        public bool MatchReady => Connected && World != null &&
            World.Gunner != PlayerRef.None && World.Driver != PlayerRef.None;
        public ConvoyPrototype.PlayerRole RequestedRole = ConvoyPrototype.PlayerRole.Gunner;
        public Vector3 gunnerSpawn;
        public Quaternion gunnerRotation;
        public float SteeringInput;
        private string room = "";
        private string status = "Create a room, then share its code with the other player.";
        private bool connecting, matchActivated;
        private float messageUntil;
        private CancellationTokenSource cancellation;

        private void Awake()
        {
            Game = GetComponent<ConvoyPrototype>();
            Game.network = this;
            gunnerSpawn = Game.player.transform.localPosition;
            gunnerRotation = Game.player.transform.localRotation;
        }

        public void Report(string message)
        {
            status = message;
            messageUntil = Time.unscaledTime + 6f;
            Debug.LogWarning("Convoy online: " + message, this);
        }

        public ConvoyPlayer PlayerForRole(int role)
        {
            if (role != 1 && role != 2) return null;
            if ((int)Game.player.Role == role) return Game.player;
            return Game.otherPlayer != null && (int)Game.otherPlayer.Role == role ? Game.otherPlayer : null;
        }

        public void EnsureActors()
        {
            if (!Connected || World == null) return;
            ConvoyPrototype.PlayerRole role = World.Gunner == Runner.LocalPlayer ? ConvoyPrototype.PlayerRole.Gunner :
                World.Driver == Runner.LocalPlayer ? ConvoyPrototype.PlayerRole.Driver : ConvoyPrototype.PlayerRole.None;
            if (role == ConvoyPrototype.PlayerRole.None) return;
            if (Game.player.OnlineRole == ConvoyPrototype.PlayerRole.None)
            {
                Game.BeginOnline(role);
                Game.SetPaused(true);
                var remote = Instantiate(Game.player.gameObject, Game.van).GetComponent<ConvoyPlayer>();
                remote.name = "Other online player";
                remote.IsLocal = false;
                remote.OnlineRole = role == ConvoyPrototype.PlayerRole.Gunner ?
                    ConvoyPrototype.PlayerRole.Driver : ConvoyPrototype.PlayerRole.Gunner;
                foreach (Camera camera in remote.GetComponentsInChildren<Camera>())
                {
                    camera.enabled = false;
                    camera.tag = "Untagged";
                }
                foreach (AudioListener listener in remote.GetComponentsInChildren<AudioListener>())
                    listener.enabled = false;
                remote.SetSpawn(remote.Role == ConvoyPrototype.PlayerRole.Driver ? ConvoyPrototype.DriverSpawn : gunnerSpawn,
                    remote.Role == ConvoyPrototype.PlayerRole.Driver ? Quaternion.identity : gunnerRotation);
                remote.GetComponent<CharacterController>().enabled = false;
                PrototypeGeometry.BuildAvatar(remote);
                Game.otherPlayer = remote;
            }
            bool otherPresent = role == ConvoyPrototype.PlayerRole.Gunner ?
                World.Driver != PlayerRef.None : World.Gunner != PlayerRef.None;
            Game.otherPlayer.gameObject.SetActive(otherPresent);
            if (MatchReady && !matchActivated)
            {
                matchActivated = true;
                ShowLobby = false;
                status = "Room " + room + " - online. Escape opens the room menu.";
                Game.SetPaused(false);
            }
            else if (!MatchReady && matchActivated)
            {
                matchActivated = false;
                ShowLobby = true;
                Game.SetPaused(true);
                status = "Your partner disconnected. Waiting for a replacement in room " + room + ".";
            }
        }

        public void Connect(string code, bool create, ConvoyPrototype.PlayerRole role)
        {
            if (connecting || Connected)
            {
                Report("A connection is already active.");
                return;
            }
            if (Game.player.OnlineRole != ConvoyPrototype.PlayerRole.None)
            {
                Report("Return to local practice before starting another online session.");
                return;
            }
            code = code.Trim().ToUpperInvariant();
            if (code.Length < 4 || code.Length > 16)
            {
                Report("Enter a room code of 4 to 16 letters or numbers.");
                return;
            }
            foreach (char c in code)
                if (!char.IsLetterOrDigit(c))
                {
                    Report("Room codes must contain only letters and numbers.");
                    return;
                }
            if (worldPrefab == null || entityPrefab == null || inputPrefab == null)
            {
                Report("Network prefabs are missing. Run Tools > Convoy > Configure Browser Multiplayer.");
                return;
            }
            room = code;
            RequestedRole = role;
            ShowLobby = true;
            StartCoroutine(ConnectRoutine(create));
        }

        private IEnumerator ConnectRoutine(bool create)
        {
            connecting = true;
            status = "Connecting securely to Photon...";
            Game.SetPaused(true);
            GameObject runnerObject = new GameObject("Convoy Photon connection");
            runnerObject.AddComponent<ConvoyRunnerLink>().session = this;
            Runner = runnerObject.AddComponent<NetworkRunner>();
            Runner.AddCallbacks(this);
            Application.runInBackground = true;
            FusionAppSettings settings = PhotonAppSettings.Global.AppSettings.GetCopy();
            settings.FixedRegion = "us";
            settings.AppVersion = "convoy-web-2";
            settings.Protocol = Photon.Client.ConnectionProtocol.WebSocketSecure;
            settings.EnableProtocolFallback = false;
            NetworkProjectConfig config = NetworkProjectConfig.Deserialize(NetworkProjectConfig.Serialize(NetworkProjectConfig.Global));
            config.PrefabTable = NetworkProjectConfig.Global.PrefabTable;
            config.PeerMode = NetworkProjectConfig.PeerModes.Single;
            config.AllowMasterClientReassignRequest = true;
            cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            Task<StartGameResult> task = Runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Shared, SessionName = room, PlayerCount = 2,
                IsVisible = false, EnableClientSessionCreation = create,
                CustomPhotonAppSettings = settings, Config = config,
                StartGameCancellationToken = cancellation.Token
            });
            while (!task.IsCompleted) yield return null;
            cancellation.Dispose();
            cancellation = null;
            connecting = false;
            if (task.IsFaulted)
            {
                Debug.LogException(task.Exception, this);
                Report("Connection failed: " + task.Exception.GetBaseException().Message);
            }
            else if (task.IsCanceled)
                Report("Connection was cancelled or timed out.");
            else if (!task.Result.Ok)
                Report("Could not enter room: " + task.Result.ShutdownReason + ". Check the code or try a different room.");
            else
            {
                status = "Room " + room + " - waiting for the other role.";
                if (Runner.IsSharedModeMasterClient)
                    Runner.Spawn(worldPrefab);
                Runner.Spawn(inputPrefab, onBeforeSpawned: (runner, obj) =>
                {
                    ConvoyNetworkInput input = obj.GetComponent<ConvoyNetworkInput>();
                    input.RequestedRole = (int)RequestedRole;
                    input.RoleRequest = 1;
                });
                yield break;
            }
            if (Runner != null)
            {
                Task shutdown = Runner.Shutdown();
                while (!shutdown.IsCompleted) yield return null;
                if (shutdown.IsFaulted) Debug.LogException(shutdown.Exception, this);
                Runner = null;
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && Connected && Runner.IsSharedModeMasterClient)
                foreach (PlayerRef player in Runner.ActivePlayers)
                    if (player != Runner.LocalPlayer)
                    {
                        Runner.SetMasterClient(player);
                        break;
                    }
        }

        private void OnGUI()
        {
            bool showMenu = ShowLobby || (Connected && (Game.Paused || Game.IsDead));
            if (!showMenu && Time.unscaledTime > messageUntil) return;
            Matrix4x4 previous = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                new Vector3(Screen.width / 1280f, Screen.height / 720f, 1f));
            if (showMenu)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                GUI.color = new Color(0f, 0f, 0f, 0.92f);
                GUI.DrawTexture(new Rect(0, 0, 1280, 720), Texture2D.whiteTexture);
                GUI.color = Color.white;
                var title = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
                var button = new GUIStyle(GUI.skin.button) { fontSize = 22 };
                var label = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                GUI.Label(new Rect(300, 115, 680, 70), "Online co-op - Driver + Gunner", title);
                GUI.Label(new Rect(300, 190, 680, 70), Game.IsDead ?
                    "Van shut down. Return to local practice, then create a new room to play again." : status, label);
                if (!connecting && !Connected)
                {
                    if (GUI.Button(new Rect(330, 280, 300, 55), (RequestedRole == ConvoyPrototype.PlayerRole.Gunner ? "[Selected] " : "") + "Player 1 - Gunner", button))
                        RequestedRole = ConvoyPrototype.PlayerRole.Gunner;
                    if (GUI.Button(new Rect(650, 280, 300, 55), (RequestedRole == ConvoyPrototype.PlayerRole.Driver ? "[Selected] " : "") + "Player 2 - Driver", button))
                        RequestedRole = ConvoyPrototype.PlayerRole.Driver;
                    room = GUI.TextField(new Rect(390, 365, 500, 45), room, 16,
                        new GUIStyle(GUI.skin.textField) { fontSize = 26, alignment = TextAnchor.MiddleCenter });
                    if (GUI.Button(new Rect(330, 440, 300, 55), "Create room", button))
                        Connect(Guid.NewGuid().ToString("N").Substring(0, 8), true, RequestedRole);
                    if (GUI.Button(new Rect(650, 440, 300, 55), "Join code", button))
                        Connect(room, false, RequestedRole);
                }
                else if (Connected && World != null && Game.player.OnlineRole == ConvoyPrototype.PlayerRole.None)
                {
                    if (Input != null && GUI.Button(new Rect(330, 330, 300, 55), "Take gunner role", button)) Input.RequestRole(1);
                    if (Input != null && GUI.Button(new Rect(650, 330, 300, 55), "Take driver role", button)) Input.RequestRole(2);
                }
                GUI.Label(new Rect(300, 505, 680, 45), Connected ? "Share room code: " + room : "Both browsers use the same game URL and room code.", label);
                if (MatchReady && !Game.IsDead && GUI.Button(new Rect(440, 555, 400, 40), "Resume my controls", button))
                {
                    ShowLobby = false;
                    Game.SetPaused(false);
                }
                if (!connecting && GUI.Button(new Rect(440, 605, 400, 45), "Back to local practice", button))
                {
                    if (Connected || Game.player.OnlineRole != ConvoyPrototype.PlayerRole.None)
                        StartCoroutine(ReturnToPractice());
                    else ShowLobby = false;
                }
            }
            else
            {
                GUI.color = Color.white;
                GUI.Label(new Rect(320, 650, 640, 60), status,
                    new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true });
            }
            GUI.matrix = previous;
            GUI.color = previousColor;
        }

        private IEnumerator ReturnToPractice()
        {
            if (Runner != null)
            {
                Task shutdown = Runner.Shutdown();
                while (!shutdown.IsCompleted) yield return null;
                if (shutdown.IsFaulted) Debug.LogException(shutdown.Exception, this);
            }
            yield return null;
            SceneManager.LoadScene(gameObject.scene.path);
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
        {
            World = null;
            ShowLobby = true;
            Game.SetPaused(true);
            status = "Connection closed: " + reason + ". Return to local practice before reconnecting.";
        }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) => Report("Disconnected: " + reason);
        public void OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason reason) => Report("Connection failed: " + reason);
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessions) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken token) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
    }
}
