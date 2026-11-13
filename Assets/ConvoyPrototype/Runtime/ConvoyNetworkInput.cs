using Fusion;
using UnityEngine;

namespace Convoy
{
    public sealed class ConvoyNetworkInput : NetworkBehaviour
    {
        public const int QueueCapacity = 32;
        [Networked] public int RequestedRole { get; set; }
        [Networked] public int RoleRequest { get; set; }
        [Networked] public ConvoyPlayerState Pose { get; set; }
        [Networked] public Vector2 Aim { get; set; }
        [Networked] public float Steering { get; set; }
        [Networked] public int FrameSequence { get; set; }
        [Networked] public int CommandSequence { get; set; }
        [Networked, Capacity(QueueCapacity)] public NetworkArray<ConvoyCommand> Commands => default;
        private ConvoyMultiplayer session;

        public override void Spawned()
        {
            session = Runner.GetComponent<ConvoyRunnerLink>().session;
            session.inputs.Add(this);
            if (HasStateAuthority)
                session.Input = this;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || session.Game.player.OnlineRole == ConvoyPrototype.PlayerRole.None)
                return;
            Pose = session.Game.player.CapturePlayer();
            Aim = session.Game.GunAim;
            Steering = session.Game.Paused ? 0f : session.SteeringInput;
            FrameSequence++;
        }

        public void RequestRole(int role)
        {
            if (!HasStateAuthority)
                throw new System.InvalidOperationException("Only the owning player can request their role.");
            RequestedRole = role;
            RoleRequest++;
        }

        public void Enqueue(ConvoyNetworkWorld.Action action)
        {
            if (!HasStateAuthority || session.World == null)
                throw new System.InvalidOperationException("A local input stream and shared world are required.");
            int processed = session.World.Gunner == Runner.LocalPlayer ?
                session.World.GunnerProcessed : session.World.DriverProcessed;
            if (CommandSequence - processed >= QueueCapacity)
            {
                session.Report("The connection is catching up. Wait before interacting again.");
                return;
            }
            int sequence = CommandSequence + 1;
            Commands.Set(sequence % QueueCapacity, new ConvoyCommand
            {
                Sequence = sequence, Action = (int)action,
                Pose = session.Game.player.CapturePlayer(), Aim = session.Game.GunAim
            });
            CommandSequence = sequence;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (session == null) return;
            session.inputs.Remove(this);
            if (session.Input == this) session.Input = null;
        }
    }
}
