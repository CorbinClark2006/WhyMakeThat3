using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Convoy
{
    public sealed class ConvoyNetworkWorld : NetworkBehaviour
    {
        public enum Action { Interact = 1, Fire = 2, Drop = 3 }
        public const int EventCapacity = 32;
        [Networked] public PlayerRef Gunner { get; set; }
        [Networked] public PlayerRef Driver { get; set; }
        [Networked] public ConvoyPlayerState GunnerPose { get; set; }
        [Networked] public ConvoyPlayerState DriverPose { get; set; }
        [Networked] public ConvoyWorldState State { get; set; }
        [Networked] public ConvoyAcceleratorState Accelerator { get; set; }
        [Networked] public Vector3 RoadProgress { get; set; }
        [Networked] public int Ammo { get; set; }
        [Networked] public int GunnerProcessed { get; set; }
        [Networked] public int DriverProcessed { get; set; }
        [Networked] public int EventSequence { get; set; }
        [Networked, Capacity(EventCapacity)] public NetworkArray<ConvoyEvent> Events => default;
        [Networked] public NetworkBool Started { get; set; }
        public bool ExecutingCommand { get; private set; }
        private ConvoyMultiplayer session;
        private readonly HashSet<GameObject> tracked = new HashSet<GameObject>();
        private readonly Dictionary<PlayerRef, int> roleAttempts = new Dictionary<PlayerRef, int>();
        private bool hadAuthority;
        private int seenEvents;
        private int lastDriverSequence;
        private int gunnerLessons, driverLessons;
        private float nextDiscovery, steering, lastDriverFrame;
        private ConvoyPrototype Game => session.Game;

        public override void Spawned()
        {
            session = Runner.GetComponent<ConvoyRunnerLink>().session;
            session.World = this;
            seenEvents = EventSequence;
            if (HasStateAuthority)
            {
                State = Game.CaptureWorld();
                Accelerator = Game.accelerator.CaptureOnline();
                Ammo = Game.ShotsAvailable;
                hadAuthority = true;
            }
            else
            {
                if (Game.supplyPack != null) Destroy(Game.supplyPack.gameObject);
                Game.supplyPack = null;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            session.EnsureActors();
            if (!hadAuthority)
            {
                Game.ApplyWorld(State);
                Game.accelerator.ApplyOnline(Accelerator);
                Game.roadRocks.ApplyProgress(RoadProgress);
                Game.ApplyAmmoCount(Ammo);
                hadAuthority = true;
            }
            ReconcileDepartedPlayers();
            foreach (ConvoyNetworkInput input in session.inputs)
            {
                if (input.Object == null) continue;
                PlayerRef source = input.Object.StateAuthority;
                AssignRole(input, source);
                int role = source == Gunner ? 1 : source == Driver ? 2 : 0;
                if (role == 0 || session.PlayerForRole(role) == null) continue;
                if (AcceptPose(source, input.Pose))
                {
                    if (role == 2 && input.FrameSequence != lastDriverSequence)
                    {
                        lastDriverSequence = input.FrameSequence;
                        steering = Finite(input.Steering) ? Mathf.Clamp(input.Steering, -1f, 1f) : 0f;
                        lastDriverFrame = Time.unscaledTime;
                    }
                    else if (input.Pose.Mounted && Finite(input.Aim.x) && Finite(input.Aim.y))
                        Game.SetGunAim(input.Aim.x, input.Aim.y);
                }
                int processed = role == 1 ? GunnerProcessed : DriverProcessed;
                int latest = input.CommandSequence;
                if (latest - processed > ConvoyNetworkInput.QueueCapacity)
                {
                    Message(source, "Input queue exceeded its capacity; reconnect to restore the input stream.");
                    processed = latest;
                }
                while (processed < latest)
                {
                    ConvoyCommand command = input.Commands[(processed + 1) % ConvoyNetworkInput.QueueCapacity];
                    if (command.Sequence != processed + 1) break;
                    Execute(source, command);
                    processed++;
                }
                if (role == 1) GunnerProcessed = processed;
                else DriverProcessed = processed;
            }
            if (session.MatchReady && !Started)
            {
                Started = true;
                Game.SpawnPursuer(-1, -65f);
                Game.roadRocks.BeginPractice();
            }
            State = Game.CaptureWorld();
            Accelerator = Game.accelerator.CaptureOnline();
            RoadProgress = Game.roadRocks.CaptureProgress();
            Ammo = Game.ShotsAvailable;
        }

        private void AssignRole(ConvoyNetworkInput input, PlayerRef source)
        {
            if (source == PlayerRef.None || source == Gunner || source == Driver) return;
            if (roleAttempts.TryGetValue(source, out int attempt) && attempt == input.RoleRequest) return;
            roleAttempts[source] = input.RoleRequest;
            int role = input.RequestedRole;
            if (role != 1 && role != 2)
            {
                Message(source, "Choose the gunner or driver role.");
                return;
            }
            if ((role == 1 && Gunner != PlayerRef.None) || (role == 2 && Driver != PlayerRef.None))
            {
                Message(source, "That role is occupied. Choose the other role.");
                return;
            }
            var pose = new ConvoyPlayerState
            {
                Position = role == 2 ? ConvoyPrototype.DriverSpawn : session.gunnerSpawn,
                Yaw = role == 2 ? 0f : session.gunnerRotation.eulerAngles.y, Grounded = true
            };
            if (role == 1) { Gunner = source; GunnerPose = pose; GunnerProcessed = 0; gunnerLessons = 0; }
            else { Driver = source; DriverPose = pose; DriverProcessed = 0; driverLessons = 0; }
            session.EnsureActors();
        }

        public override void Render()
        {
            session.EnsureActors();
            if (!HasStateAuthority)
            {
                hadAuthority = false;
                Game.ApplyWorld(State);
                Game.ApplyAmmoCount(Ammo);
                Game.accelerator.ApplyOnline(Accelerator);
                Game.roadRocks.ApplyProgress(RoadProgress);
            }
            session.PlayerForRole(1)?.ApplyRemotePlayer(GunnerPose, HasStateAuthority);
            session.PlayerForRole(2)?.ApplyRemotePlayer(DriverPose, HasStateAuthority);
            if (Game.IsDead) Game.player.Dismount();
            ProcessEvents();
        }

        private void ProcessEvents()
        {
            if (EventSequence - seenEvents > EventCapacity)
            {
                session.Report("The browser was delayed; some visual effects were skipped. Shared gameplay state is restored.");
                seenEvents = EventSequence - EventCapacity;
            }
            while (seenEvents < EventSequence)
            {
                ConvoyEvent message = Events[(seenEvents + 1) % EventCapacity];
                if (message.Sequence != seenEvents + 1) break;
                seenEvents++;
                if (message.Kind == 1 && message.Target == Runner.LocalPlayer)
                    session.Report(message.Message.ToString());
                else if (message.Kind == 5 && message.Target == Runner.LocalPlayer)
                    Game.Tutorial.Complete((ConvoyTutorial.Lesson)message.Value);
                else if (!HasStateAuthority)
                {
                    if (message.Kind == 2)
                        Game.player.AddImpact(Vector3.left * (message.Value * 3.8f), -message.Value * 9f);
                    else if (message.Kind == 3)
                        Game.ShowRemoteShot(Game.transform.TransformPoint(message.Position), message.Value != 0);
                    else if (message.Kind == 4)
                        Game.ShowExplosion(Game.transform.TransformPoint(message.Position));
                }
            }
        }

        private void Emit(int kind, int value, Vector3 position, PlayerRef target, string text = "")
        {
            if (!HasStateAuthority)
                throw new System.InvalidOperationException("Only world authority can publish shared events.");
            int sequence = EventSequence + 1;
            Events.Set(sequence % EventCapacity, new ConvoyEvent
            {
                Sequence = sequence, Kind = kind, Value = value, Position = position,
                Target = target, Message = text
            });
            EventSequence = sequence;
        }

        private void Message(PlayerRef target, string message) => Emit(1, 0, Vector3.zero, target, message);
        public void BroadcastImpact(int side) => Emit(2, side, Vector3.zero, PlayerRef.None);
        public void BroadcastShot(Vector3 end, bool hit) =>
            Emit(3, hit ? 1 : 0, Game.transform.InverseTransformPoint(end), PlayerRef.None);
        public void BroadcastExplosion(Vector3 position) =>
            Emit(4, 0, Game.transform.InverseTransformPoint(position), PlayerRef.None);
        public void Remember(GameObject visual) => tracked.Add(visual);
        public void Untrack(GameObject visual) => tracked.Remove(visual);

        public void RecordLesson(ConvoyPrototype.PlayerRole role, ConvoyTutorial.Lesson lesson)
        {
            if (!HasStateAuthority)
                throw new System.InvalidOperationException("Only world authority can confirm shared interaction lessons.");
            int learned = role == ConvoyPrototype.PlayerRole.Driver ? driverLessons : gunnerLessons;
            if ((learned & (int)lesson) == (int)lesson)
                return;
            if (role == ConvoyPrototype.PlayerRole.Gunner) gunnerLessons |= (int)lesson;
            else if (role == ConvoyPrototype.PlayerRole.Driver) driverLessons |= (int)lesson;
            else throw new System.ArgumentOutOfRangeException(nameof(role));
            Emit(5, (int)lesson, Vector3.zero, role == ConvoyPrototype.PlayerRole.Driver ? Driver : Gunner);
        }

        private void LateUpdate()
        {
            if (Object == null || !HasStateAuthority) return;
            ConvoyPlayer driver = session.PlayerForRole(2);
            if (driver != null && Game.SimulationRunning)
            {
                ExecutingCommand = true;
                try
                {
                    Game.Steer(Time.unscaledTime - lastDriverFrame < 0.35f ? steering : 0f,
                        Time.deltaTime, driver);
                }
                finally { ExecutingCommand = false; }
            }
            if (Time.unscaledTime < nextDiscovery) return;
            nextDiscovery = Time.unscaledTime + 0.1f;
            foreach (PursuerVan enemy in Game.Pursuers) Track(enemy.gameObject, ConvoyNetworkEntity.EntityKind.Enemy);
            foreach (ConvoyAmmoPack pack in Game.van.GetComponentsInChildren<ConvoyAmmoPack>())
                if (!pack.Consumed) Track(pack.gameObject, ConvoyNetworkEntity.EntityKind.Ammo);
            foreach (Transform rock in Game.roadRocks.Rocks)
                Track(rock.gameObject, Game.roadRocks.IsBush(rock) ?
                    ConvoyNetworkEntity.EntityKind.Bush : ConvoyNetworkEntity.EntityKind.Rock);
        }

        private void Track(GameObject visual, ConvoyNetworkEntity.EntityKind kind)
        {
            if (!tracked.Add(visual)) return;
            Runner.Spawn(session.entityPrefab, onBeforeSpawned: (runner, obj) =>
            {
                ConvoyNetworkEntity entity = obj.GetComponent<ConvoyNetworkEntity>();
                entity.Kind = (int)kind;
                entity.visual = visual;
                entity.InitializeState(Game);
            });
        }

        public void SendAction(Action action)
        {
            if (!session.MatchReady || session.Input == null)
            {
                session.Report("Wait for both players to join before interacting.");
                return;
            }
            session.Input.Enqueue(action);
        }

        private bool AcceptPose(PlayerRef source, ConvoyPlayerState pose)
        {
            int role = source == Gunner ? 1 : source == Driver ? 2 : 0;
            ConvoyPlayer actor = session.PlayerForRole(role);
            if (actor == null) return false;
            Vector3 p = pose.Position;
            if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z) || !Finite(pose.Yaw) ||
                !Finite(pose.Respawn) || Mathf.Abs(p.x) > 1.6f || p.z < -3.3f || p.z > 5.6f ||
                p.y < -3.5f || p.y > 10f || pose.Respawn < 0f || pose.Respawn > ConvoyPlayer.RespawnDelay)
            {
                Message(source, "Invalid player position or respawn state.");
                return false;
            }
            actor.ApplyRemotePlayer(pose, true);
            if (pose.Respawn > 0f)
            {
                actor.ReleaseAmmo(false);
                if (actor.CarryingRock) Game.accelerator.ReleaseRock();
            }
            pose.Mounted = pose.Mounted && role == 1 && actor.CanMount && actor.CarriedAmmo == null &&
                !actor.CarryingRock && pose.Respawn <= 0f;
            pose.Driving = pose.Driving && role == 2 && actor.CanDrive && actor.CarriedAmmo == null &&
                !actor.CarryingRock && pose.Respawn <= 0f;
            if (role == 1) GunnerPose = pose;
            else DriverPose = pose;
            actor.ApplyRemotePlayer(pose, true);
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void Execute(PlayerRef source, ConvoyCommand command)
        {
            if (!AcceptPose(source, command.Pose)) return;
            ConvoyPlayer actor = session.PlayerForRole(source == Gunner ? 1 : 2);
            ExecutingCommand = true;
            try
            {
                if (command.Action == (int)Action.Drop)
                {
                    actor.ReleaseAmmo(false);
                    if (actor.CarryingRock) Game.accelerator.ReleaseRock();
                    return;
                }
                if (!session.MatchReady || Game.IsDead || actor.IsRespawning)
                {
                    Message(source, "Controls are unavailable while waiting, respawning, or after van shutdown.");
                    return;
                }
                if (command.Action == (int)Action.Fire && source == Gunner && Finite(command.Aim.x) && Finite(command.Aim.y))
                {
                    Game.SetGunAim(command.Aim.x, command.Aim.y);
                    if (!Game.TryFire(actor)) Message(source, "Mount the gun, load ammo, and wait for it to recharge.");
                }
                else if (command.Action == (int)Action.Interact)
                {
                    if (!actor.TryInteract()) Message(source, "Move closer to the item or pedal; release the gun or wheel to interact.");
                }
                else Message(source, "That action is not available to your role.");
            }
            finally { ExecutingCommand = false; }
        }

        private void ReconcileDepartedPlayers()
        {
            if (Gunner != PlayerRef.None && !Present(Gunner)) { ReleaseRole(1); Gunner = PlayerRef.None; }
            if (Driver != PlayerRef.None && !Present(Driver)) { ReleaseRole(2); Driver = PlayerRef.None; steering = 0f; }
        }

        private bool Present(PlayerRef player)
        {
            foreach (PlayerRef active in Runner.ActivePlayers) if (active == player) return true;
            return false;
        }

        private void ReleaseRole(int role)
        {
            ConvoyPlayer actor = session.PlayerForRole(role);
            if (actor == null) return;
            ExecutingCommand = true;
            try
            {
                actor.ReleaseAmmo(false);
                if (actor.CarryingRock) Game.accelerator.ReleaseRock();
            }
            finally { ExecutingCommand = false; }
        }
    }
}
