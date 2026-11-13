using UnityEngine;

namespace Convoy
{
    public sealed class ConvoyAccelerator : MonoBehaviour
    {
        public const int HitsToDislodge = 3;
        public const float StopDuration = 15f;
        public const float RecoveryDuration = 5f;
        public static readonly Vector3 PedalPosition = new Vector3(-0.72f, 0.26f, 3.35f);
        public static readonly Vector3 FallenPosition = new Vector3(-0.72f, 0.275f, 2.95f);
        public ConvoyPrototype game;
        public Transform pedal;
        public Transform rock;
        public bool RockOnPedal { get; private set; } = true;
        public bool IsCarried { get; private set; }
        public int Impacts { get; private set; }
        public float SpeedFraction { get; private set; } = 1f;
        public bool NeedsRock => !RockOnPedal;

        private float transitionStart = 1f, transitionElapsed, rockElapsed;
        private Vector3 rockStart;
        private static readonly Vector3 SeatedPosition = PedalPosition + Vector3.up * 0.085f;
        private Vector3 RestPosition => RockOnPedal
            ? SeatedPosition + Vector3.back * (Impacts * 0.045f) : FallenPosition;

        public void RegisterImpact()
        {
            if (!RockOnPedal)
                return;
            Impacts++;
            rockStart = rock.localPosition;
            rockElapsed = 0f;
            if (Impacts >= HitsToDislodge)
            {
                RockOnPedal = false;
                transitionStart = SpeedFraction;
                transitionElapsed = 0f;
            }
        }

        public void Step(float dt)
        {
            if (!game.WorldAuthority || !game.SimulationRunning)
                return;
            transitionElapsed += dt;
            SpeedFraction = Mathf.Lerp(transitionStart, RockOnPedal ? 1f : 0f,
                transitionElapsed / (RockOnPedal ? RecoveryDuration : StopDuration));
            pedal.localRotation = Quaternion.Euler(RockOnPedal ? 0f : -18f, 0f, 0f);
            if (IsCarried)
                return;
            rockElapsed += dt;
            float t = Mathf.Clamp01(rockElapsed / 0.45f);
            rock.localPosition = Vector3.Lerp(rockStart, RestPosition, Mathf.SmoothStep(0f, 1f, t));
            if (!RockOnPedal)
                rock.localPosition += Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.06f);
            rock.localRotation = Quaternion.Euler(0f, 20f, RockOnPedal ? Impacts * 9f : 25f);
        }

        public void InitializeRock()
        {
            rockStart = SeatedPosition;
            rock.localPosition = rockStart;
        }

        public bool TryPickUp(ConvoyPlayer player)
        {
            if (!CanPickUp(player))
                return false;
            IsCarried = true;
            rock.SetParent(player.transform, false);
            rock.localPosition = new Vector3(0f, ConvoyAmmoPack.CarryHeight, ConvoyAmmoPack.CarryDistance);
            rock.localRotation = Quaternion.identity;
            game.RecordLesson(player, ConvoyTutorial.Lesson.RockPickup);
            game.PlayInteractionSound(ConvoyPrototype.Sound.Pickup);
            return true;
        }

        public bool CanPickUp(ConvoyPlayer player) =>
            game.SimulationRunning && !RockOnPedal && !IsCarried && game.ContainsPlayer(player) &&
            !player.Mounted && !player.Driving && !player.IsRespawning && player.CarriedAmmo == null &&
            player.CanReachInteraction(rock.position);

        public bool CanPlace(ConvoyPlayer player) =>
            game.SimulationRunning && IsCarried && game.ContainsPlayer(player) &&
            rock.parent == player.transform && player.CanReachInteraction(pedal.position);

        public bool TryPlace(ConvoyPlayer player)
        {
            if (!CanPlace(player))
                return false;
            IsCarried = false;
            RockOnPedal = true;
            Impacts = 0;
            transitionStart = SpeedFraction;
            transitionElapsed = 0f;
            rock.SetParent(game.van, false);
            InitializeRock();
            rockElapsed = 0.45f;
            rock.localRotation = Quaternion.identity;
            pedal.localRotation = Quaternion.identity;
            game.RecordLesson(player, ConvoyTutorial.Lesson.RockPlace);
            return true;
        }

        public void ReleaseRock()
        {
            if (!IsCarried)
                return;
            IsCarried = false;
            rock.SetParent(game.van, false);
            rockStart = FallenPosition;
            rock.localPosition = FallenPosition;
            rockElapsed = 0.45f;
        }

        public void ResetForRound()
        {
            RockOnPedal = true;
            IsCarried = false;
            Impacts = 0;
            SpeedFraction = transitionStart = 1f;
            transitionElapsed = 0f;
            rockElapsed = 0.45f;
            rock.SetParent(game.van, false);
            InitializeRock();
            rock.localRotation = Quaternion.identity;
            pedal.localRotation = Quaternion.identity;
        }

        public ConvoyAcceleratorState CaptureOnline()
        {
            int carrier = rock.parent == game.player.transform ? (int)game.player.Role :
                game.otherPlayer != null && rock.parent == game.otherPlayer.transform ? (int)game.otherPlayer.Role : 0;
            return new ConvoyAcceleratorState
            {
                Seated = RockOnPedal, Carried = IsCarried, Carrier = carrier, Impacts = Impacts,
                Speed = SpeedFraction, Start = transitionStart, Elapsed = transitionElapsed,
                RockElapsed = rockElapsed, RockStart = rockStart, Position = rock.localPosition,
                Rotation = rock.localRotation
            };
        }

        public void ApplyOnline(ConvoyAcceleratorState state)
        {
            RockOnPedal = state.Seated;
            IsCarried = state.Carried;
            Impacts = state.Impacts;
            SpeedFraction = state.Speed;
            transitionStart = state.Start;
            transitionElapsed = state.Elapsed;
            rockElapsed = state.RockElapsed;
            rockStart = state.RockStart;
            ConvoyPlayer carrier = game.network.PlayerForRole(state.Carrier);
            rock.SetParent(carrier != null && IsCarried ? carrier.transform : game.van, false);
            rock.localPosition = state.Position;
            rock.localRotation = state.Rotation;
            pedal.localRotation = Quaternion.Euler(RockOnPedal ? 0f : -18f, 0f, 0f);
        }
    }
}
