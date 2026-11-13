using UnityEngine;

namespace Convoy
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class ConvoyAmmoPack : MonoBehaviour
    {
        public const float Scale = 1f;
        public const float CarryHeight = 0.14f;
        public const float CarryDistance = 0.16f;
        public const float CarryRadius = 0.13f;
        public ConvoyPrototype game;
        public bool Consumed { get; private set; }
        public bool IsCarried { get; private set; }
        private Rigidbody body;
        private Collider shape;
        private bool dropped;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            shape = GetComponent<Collider>();
        }

        public void PickUp(ConvoyPlayer player)
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (shape == null) shape = GetComponent<Collider>();
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            shape.enabled = false;
            IsCarried = true;
            dropped = false;
            transform.SetParent(player.transform, false);
            transform.localPosition = new Vector3(0f, CarryHeight, CarryDistance);
            transform.localRotation = Quaternion.identity;
        }

        public void Drop(Transform cabin)
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (shape == null) shape = GetComponent<Collider>();
            IsCarried = false;
            dropped = true;
            transform.SetParent(cabin, true);
            shape.isTrigger = false;
            shape.enabled = true;
            body.isKinematic = !game.SimulationRunning;
        }

        public void Consume()
        {
            Consumed = true;
            IsCarried = false;
            gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(gameObject);
        }

        private void FixedUpdate()
        {
            if (!game.WorldAuthority)
                return;
            if (dropped)
                body.isKinematic = !game.SimulationRunning;
            if (dropped && transform.localPosition.y < -2f)
                Consume();
        }

        public ConvoyAmmoState CaptureOnline()
        {
            int carrier = transform.parent == game.player.transform ? (int)game.player.Role :
                game.otherPlayer != null && transform.parent == game.otherPlayer.transform ? (int)game.otherPlayer.Role : 0;
            return new ConvoyAmmoState
            {
                Position = transform.localPosition, Rotation = transform.localRotation,
                Carrier = carrier, Supply = game.supplyPack == this, Dropped = dropped,
                Velocity = body.isKinematic ? Vector3.zero : body.linearVelocity,
                AngularVelocity = body.isKinematic ? Vector3.zero : body.angularVelocity
            };
        }

        public void ApplyOnline(ConvoyAmmoState state, bool authoritative)
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (shape == null) shape = GetComponent<Collider>();
            ConvoyPlayer carrier = game.network.PlayerForRole(state.Carrier);
            IsCarried = carrier != null;
            dropped = state.Dropped;
            transform.SetParent(IsCarried ? carrier.transform : game.van, false);
            transform.SetLocalPositionAndRotation(state.Position, state.Rotation);
            shape.enabled = !IsCarried;
            body.isKinematic = !authoritative || !dropped || !game.SimulationRunning;
            if (!body.isKinematic)
            {
                body.linearVelocity = state.Velocity;
                body.angularVelocity = state.AngularVelocity;
            }
            if (IsCarried)
                carrier.SetNetworkAmmo(this);
            if (state.Supply)
                game.supplyPack = this;
        }
    }
}
