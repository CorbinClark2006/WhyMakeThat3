using Fusion;
using UnityEngine;

namespace Convoy
{
    public struct ConvoyWorldState : INetworkStruct
    {
        public float Distance, Steering, Cooldown, HitMarker, Flash, HealthAge, ShotKick;
        public float Yaw, Pitch, Roll, RollVelocity, SpawnTimer;
        public Vector3 RigOffset, RigVelocity;
        public int Rams, Destroyed, SpawnIndex;
    }

    public struct ConvoyPlayerState : INetworkStruct
    {
        public Vector3 Position;
        public float Yaw, Respawn;
        public NetworkBool Mounted, Driving, Grounded;
        public Vector2 Animation;
    }

    public struct ConvoyAcceleratorState : INetworkStruct
    {
        public NetworkBool Seated, Carried;
        public int Impacts, Carrier;
        public float Speed, Start, Elapsed, RockElapsed;
        public Vector3 RockStart, Position;
        public Quaternion Rotation;
    }

    public struct ConvoyEnemyState : INetworkStruct
    {
        public Vector3 Position, RetreatStart;
        public float Knockback, Slow, Timer, RetreatZ;
        public int Side, Hits, State;
        public NetworkBool RamDelivered;
    }

    public struct ConvoyAmmoState : INetworkStruct
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity, AngularVelocity;
        public int Carrier;
        public NetworkBool Supply, Dropped;
    }

    public struct ConvoyCommand : INetworkStruct
    {
        public int Sequence, Action;
        public ConvoyPlayerState Pose;
        public Vector2 Aim;
    }

    public struct ConvoyEvent : INetworkStruct
    {
        public int Sequence, Kind, Value;
        public PlayerRef Target;
        public Vector3 Position;
        public NetworkString<_128> Message;
    }
}
