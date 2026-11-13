using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;

public sealed class FusionConnectionTests
{
    private readonly List<NetworkRunner> runners = new List<NetworkRunner>();

    [UnityTest]
    [Explicit("Connects to the configured Photon application over the internet.")]
    public IEnumerator TwoSecureWebSocketPeersJoinSamePrivateRoomAndExchangeData()
    {
        string room = "convoy-test-" + Guid.NewGuid().ToString("N");
        using (CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
        {
            NetworkRunner first = CreateRunner("Photon connection test A");
            NetworkRunner second = CreateRunner("Photon connection test B");
            var receiver = new Receiver();
            var replyReceiver = new Receiver();
            second.AddCallbacks(receiver);
            first.AddCallbacks(replyReceiver);
            Task<StartGameResult> start = Start(first, room, cancellation.Token, true);
            while (!start.IsCompleted)
                yield return null;
            Assert.That(start.GetAwaiter().GetResult().Ok, Is.True, start.Result.ToString());
            start = Start(second, room, cancellation.Token, false);
            while (!start.IsCompleted)
                yield return null;
            Assert.That(start.GetAwaiter().GetResult().Ok, Is.True, start.Result.ToString());
            float deadline = Time.realtimeSinceStartup + 10f;
            while ((PlayerCount(first) != 2 || PlayerCount(second) != 2) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(PlayerCount(first), Is.EqualTo(2));
            Assert.That(PlayerCount(second), Is.EqualTo(2));
            Assert.That(first.SessionInfo.Name, Is.EqualTo(second.SessionInfo.Name));
            first.SendReliableDataToPlayer(second.LocalPlayer, ReliableKey.FromInts(1, 2, 3, 4),
                new byte[] { 17, 42, 99 });
            deadline = Time.realtimeSinceStartup + 10f;
            while (receiver.received == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(receiver.received, Is.EqualTo(new byte[] { 17, 42, 99 }),
                "The two cloud peers must exchange data, not merely report a connection.");
            Assert.That(receiver.receivedKey, Is.EqualTo(ReliableKey.FromInts(1, 2, 3, 4)));
            Assert.That(replyReceiver.received, Is.Null);
            second.SendReliableDataToPlayer(first.LocalPlayer, ReliableKey.FromInts(4, 3, 2, 1),
                new byte[] { 99, 42, 17 });
            deadline = Time.realtimeSinceStartup + 10f;
            while (replyReceiver.received == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(replyReceiver.received, Is.EqualTo(new byte[] { 99, 42, 17 }));
            Assert.That(replyReceiver.receivedKey, Is.EqualTo(ReliableKey.FromInts(4, 3, 2, 1)));
        }
    }

    private NetworkRunner CreateRunner(string name)
    {
        NetworkRunner runner = new GameObject(name).AddComponent<NetworkRunner>();
        runners.Add(runner);
        return runner;
    }

    private static Task<StartGameResult> Start(NetworkRunner runner, string room, CancellationToken token, bool create)
    {
        NetworkProjectConfig config = NetworkProjectConfig.Deserialize(
            NetworkProjectConfig.Serialize(NetworkProjectConfig.Global));
        config.PeerMode = NetworkProjectConfig.PeerModes.Multiple;
        FusionAppSettings settings = PhotonAppSettings.Global.AppSettings.GetCopy();
        settings.FixedRegion = "us";
        settings.Protocol = Photon.Client.ConnectionProtocol.WebSocketSecure;
        settings.EnableProtocolFallback = false;
        int sceneIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/ConvoyPrototype.unity");
        Assert.That(sceneIndex, Is.GreaterThanOrEqualTo(0), "The prototype scene must be included in Build Settings.");
        return runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = room,
            PlayerCount = 2,
            IsVisible = false,
            EnableClientSessionCreation = create,
            Scene = SceneRef.FromIndex(sceneIndex),
            SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
            Config = config,
            CustomPhotonAppSettings = settings,
            StartGameCancellationToken = token
        });
    }

    private static int PlayerCount(NetworkRunner runner)
    {
        int count = 0;
        foreach (PlayerRef player in runner.ActivePlayers)
            count++;
        return count;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (NetworkRunner runner in runners)
        {
            if (runner == null)
                continue;
            Task shutdown = runner.Shutdown();
            while (!shutdown.IsCompleted)
                yield return null;
            shutdown.GetAwaiter().GetResult();
            if (runner != null)
                UnityEngine.Object.Destroy(runner.gameObject);
        }
        runners.Clear();
        yield return null;
    }

    private sealed class Receiver : INetworkRunnerCallbacks
    {
        public byte[] received;
        public ReliableKey receivedKey;
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
        {
            receivedKey = key;
            received = data.ToArray();
        }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason reason) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessions) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken token) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
    }
}
