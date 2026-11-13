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
        public readonly List<ConvoyNetworkEntity> entities = new List<ConvoyNetworkEntity>();
        public ConvoyPrototype Game { get; private set; }
        public NetworkRunner Runner { get; private set; }
        public ConvoyNetworkWorld World { get; set; }
        public bool ShowLobby;
        public bool Connected => Runner != null && Runner.IsRunning;
        public bool RolesPresent => Connected && World != null &&
            World.Gunner != PlayerRef.None && World.Driver != PlayerRef.None;
        public bool MatchReady => RolesPresent && World.ReplayPhase == ConvoyNetworkWorld.ReplayState.Playing;
        public ConvoyPrototype.PlayerRole RequestedRole = ConvoyPrototype.PlayerRole.Gunner;
        public Vector3 gunnerSpawn;
        public Quaternion gunnerRotation;
        public float SteeringInput;
        private string room = "";
        private string status = "Create a room or enter a code.";
        private bool connecting, matchActivated;
        private float messageUntil;
        private CancellationTokenSource cancellation;
        private int preparedRound;
        private bool readySent;

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
            if (World.ReplayPhase != ConvoyNetworkWorld.ReplayState.Playing)
            {
                ShowLobby = true;
                if (!Game.Paused) Game.SetPaused(true);
                matchActivated = false;
                return;
            }
            if (MatchReady && !matchActivated)
            {
                matchActivated = true;
                ShowLobby = false;
                status = "ESC / MENU";
                Game.SetPaused(false);
            }

            else if (!MatchReady && matchActivated)
            {
                matchActivated = false;
                ShowLobby = true;
                Game.SetPaused(true);
                status = "Partner disconnected. Waiting...";
            }
        }

        public void PrepareRound()
        {
            if (World == null || World.ReplayPhase != ConvoyNetworkWorld.ReplayState.Resetting)
                return;
            if (preparedRound != World.Round)
            {
                preparedRound = World.Round;
                readySent = false;
                SteeringInput = 0f;
                var role = World.Gunner == Runner.LocalPlayer ? ConvoyPrototype.PlayerRole.Gunner :
                    ConvoyPrototype.PlayerRole.Driver;
                Game.ResetRound(role, gunnerSpawn, gunnerRotation);
                matchActivated = false;
                ShowLobby = true;
            }
            if (!readySent && Input != null && entities.Count == 0)
            {
                Input.Enqueue(ConvoyNetworkWorld.Action.RoundReady);
                readySent = true;
            }
        }

        public void ActivateRound()
        {
            if (!MatchReady || Game.player.OnlineRole == ConvoyPrototype.PlayerRole.None) return;
            ShowLobby = false;
            matchActivated = true;
            Game.SetPaused(false);
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
            settings.AppVersion = "convoy-web-6";
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
                status = "Waiting for partner...";
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
            bool showMenu = ShowLobby || (Connected && (Game.Paused || Game.IsDead ||
                (World != null && World.ReplayPhase != ConvoyNetworkWorld.ReplayState.Playing)));
            if (!showMenu && Time.unscaledTime > messageUntil) return;
            Matrix4x4 previous = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                new Vector3(Screen.width / 1280f, Screen.height / 720f, 1f));
            if (showMenu)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                ConvoyPixelUI ui = Game.UI;
                bool choosing = World != null && World.ReplayPhase == ConvoyNetworkWorld.ReplayState.ChoosingRole;
                bool resetting = World != null && World.ReplayPhase == ConvoyNetworkWorld.ReplayState.Resetting;
                ui.Backdrop();
                ui.Center(choosing ? "CHOOSE YOUR ROLE" : resetting ? "RESTARTING..." :
                    Game.IsDead ? "END OF THE ROAD" : Connected ? "ROOM" : "CO-OP",
                    new Rect(160, 110, 960, 68), 5, ConvoyPixelUI.Gold);
                ConvoyPixelUI.Panel(new Rect(264, 200, 752, 106));
                if (Game.IsDead && !choosing && !resetting)
                {
                    ui.Text(Game.SurvivalClock, 288, 246, 4, ConvoyPixelUI.Paper);
                    ui.Text(Game.DestroyedVans + " VANS", 660, 254, 3, ConvoyPixelUI.Gold);
                }
                else ui.Paragraph(choosing ? World.ReplayChooser == Runner.LocalPlayer ?
                    "Partner gets the other role." :
                    "Partner choosing..." :
                    resetting ? "Waiting for both players..." :
                    status, new Rect(288, 222, 704, 78), 2);
                if (choosing)
                {
                    if (World.ReplayChooser == Runner.LocalPlayer)
                    {
                        ui.VanBadge(400, 330, true);
                        ui.VanBadge(800, 330, false);
                        if (ui.Button(new Rect(272, 402, 352, 88), "GUNNER"))
                            World.SendAction(ConvoyNetworkWorld.Action.ReplayGunner);
                        if (ui.Button(new Rect(656, 402, 352, 88), "DRIVER"))
                            World.SendAction(ConvoyNetworkWorld.Action.ReplayDriver);
                    }
                    else
                    {
                        ui.Center("WAITING...", new Rect(280, 356, 720, 68), 3, ConvoyPixelUI.Gold);
                    }
                }
                else if (Game.IsDead && !resetting)
                {
                    bool enabled = GUI.enabled;
                    GUI.enabled = RolesPresent;
                    if (ui.Button(new Rect(408, 366, 464, 84), "PLAY AGAIN"))
                        World.SendAction(ConvoyNetworkWorld.Action.Replay);
                    GUI.enabled = enabled;
                    if (!RolesPresent) ui.Center("WAITING FOR PARTNER...", new Rect(232, 472, 816, 40), 2, ConvoyPixelUI.Copper);
                }
                else if (!resetting && !connecting && !Connected)
                {
                    if (ui.Button(new Rect(272, 332, 352, 72), "GUNNER",
                        selected: RequestedRole == ConvoyPrototype.PlayerRole.Gunner))
                        RequestedRole = ConvoyPrototype.PlayerRole.Gunner;
                    if (ui.Button(new Rect(656, 332, 352, 72), "DRIVER",
                        selected: RequestedRole == ConvoyPrototype.PlayerRole.Driver))
                        RequestedRole = ConvoyPrototype.PlayerRole.Driver;
                    room = ui.RoomField(new Rect(392, 426, 496, 52), room);
                    if (ui.Button(new Rect(272, 500, 352, 68), "CREATE ROOM"))
                        Connect(Guid.NewGuid().ToString("N").Substring(0, 8), true, RequestedRole);
                    if (ui.Button(new Rect(656, 500, 352, 68), "JOIN ROOM"))
                        Connect(room, false, RequestedRole);
                }
                else if (!resetting && Connected && World != null && Game.player.OnlineRole == ConvoyPrototype.PlayerRole.None)
                {
                    if (Input != null && ui.Button(new Rect(272, 352, 352, 72), "TAKE GUNNER")) Input.RequestRole(1);
                    if (Input != null && ui.Button(new Rect(656, 352, 352, 72), "TAKE DRIVER")) Input.RequestRole(2);
                }
                else if (!resetting && !Game.IsDead && MatchReady && ui.Button(new Rect(408, 366, 464, 80),
                    "RESUME"))
                {
                    ShowLobby = false;
                    Game.SetPaused(false);
                }
                if (Connected) ui.Center("CODE / " + room, new Rect(280, 536, 720, 36), 3, ConvoyPixelUI.Gold);
                if (!connecting && ui.Button(new Rect(440, 604, 400, 44), Connected ? "LEAVE ROOM" : "BACK"))
                {
                    if (Connected || Game.player.OnlineRole != ConvoyPrototype.PlayerRole.None)
                        StartCoroutine(ReturnToPractice());
                    else ShowLobby = false;
                }
            }
            else
            {
                ConvoyPixelUI.Panel(new Rect(400, 580, 800, 76));
                Game.UI.Paragraph(status, new Rect(416, 596, 768, 48), 2, ConvoyPixelUI.Paper);
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
