using UnityEngine;
using UnityEngine.InputSystem;

namespace Convoy
{
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-20)]
    public sealed class ConvoyPlayer : MonoBehaviour
    {
        public ConvoyPrototype game;
        public bool IsLocal = true;
        public ConvoyPrototype.PlayerRole OnlineRole;
        public ConvoyPrototype.PlayerRole Role => OnlineRole == ConvoyPrototype.PlayerRole.None ? game.Role : OnlineRole;
        private Vector2 animationInput;
        public Camera view;
        public float moveSpeed = 3.2f;
        public float mouseSensitivity = 0.2f;
        public const float BodyHeight = 0.2f;
        public const float EyeHeight = 0.17f;
        public const float CameraDistance = 0.6f;
        public Transform avatar;
        public ConvoySpriteAvatar spriteAvatar;
        public bool Mounted { get; private set; }
        public bool Driving { get; private set; }
        public bool CanDrive
        {
            get
            {
                if (Role != ConvoyPrototype.PlayerRole.Driver)
                    return false;
                Vector3 origin = transform.TransformPoint(Vector3.up * EyeHeight);
                Vector3 target = game.van.TransformPoint(ConvoyPrototype.SteeringWheelPosition);
                Vector3 offset = target - origin;
                if (offset.magnitude > 1.2f)
                    return false;
                Physics.SyncTransforms();
                foreach (RaycastHit hit in Physics.RaycastAll(origin, offset.normalized, offset.magnitude,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore))
                    if (!hit.collider.transform.IsChildOf(transform))
                        return false;
                return true;
            }
        }
        public ConvoyAmmoPack CarriedAmmo { get; private set; }
        public bool CarryingRock => game.accelerator != null && game.accelerator.IsCarried &&
            game.accelerator.rock.parent == transform;
        public Vector3 ImpactVelocity { get; private set; }
        public const float RespawnDelay = 3f;
        public const float RespawnFadeDuration = 0.6f;
        public bool IsRespawning => RespawnRemaining > 0f;
        public float RespawnRemaining { get; private set; }
        public float RespawnFadeAlpha => IsRespawning ? 1f : respawnFadeRemaining / RespawnFadeDuration;
        public bool CanMount => Vector2.Distance(new Vector2(transform.localPosition.x, transform.localPosition.z),
            new Vector2(0f, -1.35f)) < 1.5f;

        private CharacterController controller;
        private float yaw = 180f, pitch = 18f, verticalVelocity, kickRoll;
        private float jumpBuffer, groundGrace;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float respawnFadeRemaining;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            yaw = transform.localEulerAngles.y;
            spawnPosition = transform.localPosition;
            spawnRotation = transform.localRotation;
        }

        private void Update()
        {
            if (!IsLocal || game.ChoosingRole || (game.Paused && !game.Online) || game.IsDead)
                return;
            if (game.Online && !game.network.MatchReady)
                return;
            AdvanceRespawn(game.Online ? Time.unscaledDeltaTime : Time.deltaTime);
            if (IsRespawning || game.Paused)
                return;
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse == null || keyboard == null)
                return;
            float dt = Time.deltaTime;
            if (!Mounted && !Driving && keyboard.eKey.wasPressedThisFrame)
                TryInteract();
            bool handlingAmmo = CarriedAmmo != null || CarryingRock;
            if (Role == ConvoyPrototype.PlayerRole.Driver)
            {
                if (Mounted)
                    Dismount();
                if (!handlingAmmo && mouse.rightButton.isPressed && (Driving || CanDrive))
                    MountDriver();
                else
                    DismountDriver();
                if (Driving)
                {
                    game.Steer((keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f), dt);
                    return;
                }
            }
            if (!handlingAmmo && mouse.rightButton.isPressed && (Mounted || CanMount))
                Mount();
            else
                Dismount();

            if (!Mounted)
            {
                Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, -35f, 65f);
                transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                Vector2 input = new Vector2(
                    (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                    (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
                input = Vector2.ClampMagnitude(input, 1f);
                jumpBuffer = Mathf.Max(0f, jumpBuffer - dt);
                groundGrace = controller.isGrounded ? 0.08f : Mathf.Max(0f, groundGrace - dt);
                if (keyboard.spaceKey.wasPressedThisFrame)
                    jumpBuffer = 0.12f;
                if (controller.isGrounded && verticalVelocity < 0f)
                    verticalVelocity = -2f;
                if (groundGrace > 0f && jumpBuffer > 0f)
                {
                    verticalVelocity = 4.2f;
                    jumpBuffer = groundGrace = 0f;
                }
                verticalVelocity -= 12f * dt;
                Vector3 walk = transform.localRotation * new Vector3(input.x, 0f, input.y);
                Vector3 beforeMove = transform.position;
                MoveWithCollisions((walk * moveSpeed + ImpactVelocity + Vector3.up * verticalVelocity) * dt);
                if (transform.localPosition.y < -0.5f)
                {
                    BeginRespawn();
                    return;
                }
                Vector3 moved = Vector3.ProjectOnPlane(transform.position - beforeMove, game.van.up);
                bool moving = Vector3.Dot(moved, game.van.TransformDirection(walk)) > 0.0001f;
                animationInput = moving ? input : Vector2.zero;
                if (spriteAvatar != null && spriteAvatar.enabled)
                    spriteAvatar.Animate(moving ? input : Vector2.zero, controller.isGrounded, dt);
            }
            ImpactVelocity = Vector3.MoveTowards(ImpactVelocity, Vector3.zero, dt * 5f);
            kickRoll = Mathf.MoveTowards(kickRoll, 0f, dt * 22f);
        }

        private void BeginRespawn()
        {
            Dismount();
            ReleaseAmmo(false);
            if (CarryingRock)
                ReleaseCarriedRock();
            RespawnRemaining = RespawnDelay;
            controller.enabled = false;
            if (avatar != null)
                avatar.gameObject.SetActive(false);
        }

        private void AdvanceRespawn(float dt)
        {
            respawnFadeRemaining = Mathf.Max(0f, respawnFadeRemaining - dt);
            if (!IsRespawning)
                return;
            float remaining = RespawnRemaining - dt;
            RespawnRemaining = Mathf.Max(0f, remaining);
            if (IsRespawning)
                return;
            transform.SetLocalPositionAndRotation(spawnPosition, spawnRotation);
            yaw = spawnRotation.eulerAngles.y;
            pitch = 18f;
            verticalVelocity = kickRoll = jumpBuffer = groundGrace = 0f;
            ImpactVelocity = Vector3.zero;
            Physics.SyncTransforms();
            controller.enabled = true;
            if (avatar != null)
                avatar.gameObject.SetActive(true);
            respawnFadeRemaining = Mathf.Max(0f, RespawnFadeDuration + remaining);
        }

        private void MoveWithCollisions(Vector3 localDisplacement)
        {
            Vector3 start = transform.localPosition;
            Vector3 target = start + localDisplacement;
            target.x = Mathf.Clamp(target.x, -1.43f, 1.43f);
            target.z = Mathf.Clamp(target.z, -3.1f, 5.4f);
            Vector3 displacement = game.van.TransformVector(target - start);
            // The whole cabin moves with the suspension; sync its colliders before sweeping.
            Physics.SyncTransforms();
            int steps = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / (controller.radius * 0.5f)));
            Vector3 step = displacement / steps;
            for (int i = 0; i < steps; i++)
            {
                CollisionFlags collision = controller.Move(step);
                if ((collision & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
                    verticalVelocity = 0f;
            }
        }

        private void LateUpdate()
        {
            if (!IsLocal)
            {
                if (spriteAvatar != null && game.player != null)
                    spriteAvatar.FaceCamera(game.player.Mounted ? game.gunCamera : game.player.view, game.van.up);
                return;
            }
            if (Mounted || IsRespawning)
                return;
            if (Driving)
            {
                view.transform.SetPositionAndRotation(game.van.TransformPoint(ConvoyPrototype.DriverCameraPosition),
                    game.van.rotation * Quaternion.Euler(8f, 0f, 0f));
                return;
            }
            if (CarriedAmmo != null)
                PositionCarriedObject(CarriedAmmo.transform);
            if (CarryingRock)
                PositionCarriedObject(game.accelerator.rock);
            Vector3 target = transform.TransformPoint(Vector3.up * EyeHeight);
            Quaternion orbit = transform.rotation * Quaternion.Euler(pitch, 0f, 0f);
            Vector3 backward = orbit * Vector3.back;
            float distance = CameraDistance;
            Physics.SyncTransforms();
            foreach (RaycastHit hit in Physics.SphereCastAll(target, 0.045f, backward,
                CameraDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform))
                    continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.025f));
            }
            view.transform.SetPositionAndRotation(target + backward * distance,
                orbit * Quaternion.Euler(0f, 0f, kickRoll));
            if (avatar != null)
                avatar.gameObject.SetActive(distance > 0.12f);
            if (spriteAvatar != null)
                spriteAvatar.FaceCamera(view, game.van.up);
        }

        public void AddImpact(Vector3 localVelocity, float cameraRoll)
        {
            if (IsRespawning)
                return;
            ImpactVelocity = Vector3.ClampMagnitude(ImpactVelocity + localVelocity, 6f);
            kickRoll = Mathf.Clamp(kickRoll + cameraRoll, -16f, 16f);
        }

        private void PositionCarriedObject(Transform item)
        {
            Vector3 carryOrigin = transform.TransformPoint(Vector3.up * ConvoyAmmoPack.CarryHeight);
            float carryDistance = ConvoyAmmoPack.CarryDistance;
            Physics.SyncTransforms();
            foreach (RaycastHit hit in Physics.SphereCastAll(carryOrigin, ConvoyAmmoPack.CarryRadius, transform.forward,
                carryDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform))
                    carryDistance = Mathf.Min(carryDistance, Mathf.Max(0f, hit.distance - 0.01f));
            item.localPosition = new Vector3(0f, ConvoyAmmoPack.CarryHeight, carryDistance);
        }

        public bool CanReachInteraction(Vector3 target, Transform targetObject = null)
        {
            Vector3 origin = transform.TransformPoint(Vector3.up * EyeHeight);
            Vector3 offset = target - origin;
            if (offset.magnitude > 0.75f)
                return false;
            Physics.SyncTransforms();
            foreach (RaycastHit hit in Physics.RaycastAll(origin, offset.normalized, offset.magnitude,
                Physics.AllLayers, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) &&
                    (targetObject == null || !hit.collider.transform.IsChildOf(targetObject)))
                    return false;
            return true;
        }

        public bool TryInteract()
        {
            if (game.Online && !game.network.World.ExecutingCommand)
            {
                game.network.World.SendAction(ConvoyNetworkWorld.Action.Interact);
                return false;
            }
            if (!game.SimulationRunning || Mounted || Driving || IsRespawning)
                return false;
            if (CarryingRock)
                return game.accelerator.TryPlace(this);
            if (CarriedAmmo != null)
            {
                if (!game.TryLoadAmmo(CarriedAmmo, this))
                    return false;
                CarriedAmmo = null;
                return true;
            }
            if (game.accelerator != null && game.accelerator.TryPickUp(this))
                return true;
            return TryPickUpAmmo();
        }

        public bool Mount()
        {
            if (Mounted)
                return true;
            if (!CanMount || Role == ConvoyPrototype.PlayerRole.Driver || game.ChoosingRole || game.IsDead || IsRespawning ||
                CarriedAmmo != null || CarryingRock)
                return false;
            if (controller == null)
                controller = GetComponent<CharacterController>();
            Mounted = true;
            if (avatar != null)
                avatar.gameObject.SetActive(false);
            controller.enabled = false;
            view.enabled = false;
            view.GetComponent<AudioListener>().enabled = false;
            game.gunCamera.enabled = true;
            game.gunCamera.GetComponent<AudioListener>().enabled = true;
            verticalVelocity = 0f;
            jumpBuffer = groundGrace = 0f;
            return true;
        }

        public void Dismount()
        {
            DismountDriver();
            if (!Mounted)
                return;
            Mounted = false;
            if (avatar != null)
                avatar.gameObject.SetActive(true);
            controller.enabled = IsLocal;
            if (!IsLocal)
                return;
            view.enabled = true;
            view.GetComponent<AudioListener>().enabled = true;
            game.gunCamera.enabled = false;
            game.gunCamera.GetComponent<AudioListener>().enabled = false;
        }

        public bool MountDriver()
        {
            if (Driving)
                return true;
            if (!CanDrive || !game.SimulationRunning || CarriedAmmo != null || CarryingRock)
                return false;
            if (controller == null)
                controller = GetComponent<CharacterController>();
            Driving = true;
            controller.enabled = false;
            verticalVelocity = 0f;
            jumpBuffer = groundGrace = 0f;
            if (avatar != null)
                avatar.gameObject.SetActive(false);
            return true;
        }

        public void DismountDriver()
        {
            if (!Driving)
                return;
            Driving = false;
            Transform wheel = game.van.Find("Driver steering wheel");
            if (wheel != null)
                wheel.localRotation = Quaternion.Euler(20f, 0f, 0f);
            controller.enabled = IsLocal;
            if (avatar != null)
                avatar.gameObject.SetActive(true);
        }

        public void SetSpawn(Vector3 position, Quaternion rotation)
        {
            if (controller == null)
                controller = GetComponent<CharacterController>();
            Dismount();
            controller.enabled = false;
            transform.SetLocalPositionAndRotation(position, rotation);
            spawnPosition = position;
            spawnRotation = rotation;
            yaw = rotation.eulerAngles.y;
            pitch = 18f;
            verticalVelocity = kickRoll = jumpBuffer = groundGrace = 0f;
            ImpactVelocity = Vector3.zero;
            Physics.SyncTransforms();
            controller.enabled = true;
        }

        public bool TryPickUpAmmo()
        {
            if (Mounted || Driving || CarriedAmmo != null || CarryingRock || !game.SimulationRunning)
                return false;
            Vector3 origin = transform.TransformPoint(Vector3.up * EyeHeight);
            Physics.SyncTransforms();
            ConvoyAmmoPack nearest = null;
            float distance = 0.75f;
            foreach (ConvoyAmmoPack pack in game.van.GetComponentsInChildren<ConvoyAmmoPack>())
            {
                if (pack.Consumed || pack.IsCarried)
                    continue;
                float candidate = Vector3.Distance(origin, pack.transform.position);
                if (candidate >= distance)
                    continue;
                if (!CanReachInteraction(pack.transform.position, pack.transform))
                    continue;
                nearest = pack;
                distance = candidate;
            }
            if (nearest == null)
                return false;
            CarriedAmmo = nearest;
            nearest.PickUp(this);
            game.ReplenishAmmoSupply(nearest);
            return true;
        }

        public void ReleaseAmmo(bool tryReload)
        {
            if (CarriedAmmo == null)
                return;
            if (game.Online && !game.WorldAuthority && !game.network.World.ExecutingCommand)
            {
                game.network.World.SendAction(ConvoyNetworkWorld.Action.Drop);
                return;
            }
            ConvoyAmmoPack pack = CarriedAmmo;
            CarriedAmmo = null;
            if (tryReload && game.TryLoadAmmo(pack, this))
                return;
            pack.Drop(game.van);
        }

        private void OnDisable()
        {
            if (IsLocal && game != null && game.gunCamera != null && view != null)
            {
                Dismount();
                ReleaseAmmo(false);
                if (CarryingRock)
                    ReleaseCarriedRock();
            }
        }

        private void ReleaseCarriedRock()
        {
            if (game.Online && !game.WorldAuthority && !game.network.World.ExecutingCommand)
                game.network.World.SendAction(ConvoyNetworkWorld.Action.Drop);
            else
                game.accelerator.ReleaseRock();
        }

        public void SetNetworkAmmo(ConvoyAmmoPack pack) => CarriedAmmo = pack;

        public ConvoyPlayerState CapturePlayer()
        {
            return new ConvoyPlayerState
            {
                Position = transform.localPosition, Yaw = transform.localEulerAngles.y,
                Mounted = Mounted, Driving = Driving, Respawn = RespawnRemaining,
                Grounded = controller.isGrounded, Animation = animationInput
            };
        }

        public void ApplyRemotePlayer(ConvoyPlayerState state, bool immediate)
        {
            if (IsLocal)
                return;
            controller.enabled = false;
            transform.localPosition = immediate ? state.Position :
                Vector3.Lerp(transform.localPosition, state.Position, Mathf.Clamp01(Time.deltaTime * 25f));
            transform.localRotation = Quaternion.Euler(0f, state.Yaw, 0f);
            Mounted = state.Mounted;
            Driving = state.Driving;
            RespawnRemaining = state.Respawn;
            if (avatar != null)
                avatar.gameObject.SetActive(!Mounted && !Driving && !IsRespawning);
            if (spriteAvatar != null)
                spriteAvatar.Animate(state.Animation, state.Grounded, Time.deltaTime);
        }
    }
}
