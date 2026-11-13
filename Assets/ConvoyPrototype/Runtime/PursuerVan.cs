using UnityEngine;

namespace Convoy
{
    public sealed class PursuerVan : MonoBehaviour
    {
        public enum ChaseState { Approaching, Telegraphing, Ramming, Retreating }
        public ConvoyPrototype game;
        public Transform[] wheels;
        public Renderer damageIndicator;
        public Transform visualRoot;
        [System.NonSerialized] public Light headlights;
        public float KnockbackSpeed { get; private set; }
        public const float SlowDuration = 8f;
        public float SlowRemaining { get; private set; }
        public int Hits { get; private set; }
        public bool IsDestroyed { get; private set; }
        public ChaseState State { get; private set; }
        public int Side { get; private set; }
        public const float ContactX = 3.15f;
        public const float FollowingDistance = 8f;
        public const float ApproachSpeed = 4.5f;
        public const float TelegraphDuration = 2.5f;
        public const float RetreatDuration = 6f;
        private float timer, retreatZ;
        private Vector3 retreatStart;
        private bool ramDelivered;

        public void Initialize(ConvoyPrototype owner, int side)
        {
            game = owner;
            Side = side < 0 ? -1 : 1;
            State = ChaseState.Approaching;
        }

        private void Update()
        {
            if (game.WorldAuthority && game.SimulationRunning)
                Step(Time.deltaTime);
        }

        public void Step(float dt)
        {
            if (IsDestroyed || !game.SimulationRunning)
                return;
            timer += dt;
            SlowRemaining = Mathf.Max(0f, SlowRemaining - dt);
            Vector3 p = transform.localPosition;
            if (game.accelerator.Escaping)
            {
                ramDelivered = false;
                EnterState(ChaseState.Approaching);
                p.x = Mathf.MoveTowards(p.x, Side * 4.8f, dt * 3f);
                p.z -= (8f + Mathf.Max(0f, game.CurrentSpeed - game.CruiseSpeed)) * dt;
            }
            else
            switch (State)
            {
                case ChaseState.Approaching:
                    p.x = Mathf.MoveTowards(p.x, Side * 4.8f, dt * 3f);
                    float targetZ = -0.25f;
                    foreach (PursuerVan other in game.Pursuers)
                    {
                        if (other != this && !other.IsDestroyed && other.Side == Side && other.transform.localPosition.z > p.z)
                            targetZ = Mathf.Min(targetZ, other.transform.localPosition.z - FollowingDistance);
                    }
                    // A falling-back leader pushes its queue back instead of passing through it.
                    p.z = Mathf.MoveTowards(p.z, targetZ,
                        dt * (targetZ < p.z ? Mathf.Max(14f, game.CurrentSpeed) :
                            KnockbackSpeed > 0f ? 0f : SlowRemaining > 0f ? 1.5f * game.SpeedMultiplier :
                            ApproachSpeed * game.SpeedMultiplier * game.ChasePressure));
                    if (SlowRemaining <= 0f && Mathf.Abs(p.z + 0.25f) < 0.01f && Mathf.Abs(targetZ + 0.25f) < 0.01f)
                        EnterState(ChaseState.Telegraphing);
                    break;
                case ChaseState.Telegraphing:
                    p.x = Side * (4.8f + Mathf.Sin(timer * 16f) * 0.08f);
                    if (timer >= Mathf.Max(1.2f, TelegraphDuration / game.ChasePressure))
                        EnterState(ChaseState.Ramming);
                    break;
                case ChaseState.Ramming:
                    p.x = Mathf.MoveTowards(p.x, Side * ContactX, dt * Mathf.Min(6f, 3f * game.ChasePressure));
                    if (!ramDelivered && Mathf.Abs(p.x) <= ContactX + 0.01f && game.TryRegisterRam(this, p))
                    {
                        ramDelivered = true;
                        retreatStart = p;
                        retreatZ = Random.Range(-38f, -23f);
                        EnterState(ChaseState.Retreating);
                    }
                    break;
                case ChaseState.Retreating:
                    float retreatDuration = Mathf.Max(2f, RetreatDuration / game.ChasePressure);
                    p = Vector3.Lerp(retreatStart, new Vector3(Side * 4.8f, 0f, retreatZ), Mathf.Clamp01(timer / retreatDuration));
                    if (timer >= retreatDuration)
                    {
                        ramDelivered = false;
                        EnterState(ChaseState.Approaching);
                    }
                    break;
            }
            if (KnockbackSpeed > 0f)
                p.z = LimitKnockback(p.z - KnockbackSpeed * dt);
            KnockbackSpeed = Mathf.MoveTowards(KnockbackSpeed, 0f, dt * 10f);
            p.y = 0.025f * Mathf.Sin(Time.time * 12f + Side);
            transform.localPosition = p;
            UpdateHeadlights();
            if (wheels != null)
                foreach (Transform wheel in wheels)
                    wheel.Rotate(Vector3.right, game.CurrentSpeed * dt * Mathf.Rad2Deg / 0.5f, Space.World);
            if (damageIndicator != null)
                damageIndicator.sharedMaterial = State == ChaseState.Telegraphing && Mathf.Sin(timer * 15f) > 0f
                    ? game.orange : Hits == 0 ? game.cyan : game.orange;
        }

        private void UpdateHeadlights()
        {
            if (headlights == null) return;
            bool nearest = transform.localPosition.z > -35f;
            foreach (PursuerVan other in game.Pursuers)
                if (other != this && !other.IsDestroyed && other.Side == Side &&
                    other.transform.localPosition.z > transform.localPosition.z)
                    nearest = false;
            headlights.enabled = nearest;
        }

        private void EnterState(ChaseState state)
        {
            State = state;
            timer = 0f;
        }

        private float LimitKnockback(float z)
        {
            foreach (PursuerVan other in game.Pursuers)
            {
                if (other != this && !other.IsDestroyed && other.Side == Side &&
                    other.transform.localPosition.z < transform.localPosition.z)
                    z = Mathf.Max(z, other.transform.localPosition.z + FollowingDistance);
            }
            return z;
        }

        public bool TakeHit()
        {
            if (IsDestroyed)
                return false;
            Hits++;
            if (Hits < 2)
            {
                KnockbackSpeed = 12f;
                SlowRemaining = SlowDuration;
                ramDelivered = false;
                EnterState(ChaseState.Approaching);
                if (damageIndicator != null) damageIndicator.sharedMaterial = game.orange;
                return false;
            }
            IsDestroyed = true;
            foreach (Collider shape in GetComponentsInChildren<Collider>())
                shape.enabled = false;
            game.OnEnemyDestroyed(this);
            gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(gameObject);
            return true;
        }

        public ConvoyEnemyState CaptureOnline()
        {
            return new ConvoyEnemyState
            {
                Position = transform.localPosition, RetreatStart = retreatStart, Knockback = KnockbackSpeed,
                Slow = SlowRemaining, Timer = timer, RetreatZ = retreatZ, Side = Side, Hits = Hits,
                State = (int)State, RamDelivered = ramDelivered
            };
        }

        public void ApplyOnline(ConvoyEnemyState state, bool immediate)
        {
            Side = state.Side;
            Hits = state.Hits;
            State = (ChaseState)state.State;
            KnockbackSpeed = state.Knockback;
            SlowRemaining = state.Slow;
            timer = state.Timer;
            retreatZ = state.RetreatZ;
            retreatStart = state.RetreatStart;
            ramDelivered = state.RamDelivered;
            transform.localPosition = immediate ? state.Position :
                Vector3.Lerp(transform.localPosition, state.Position, Mathf.Clamp01(Time.deltaTime * 25f));
            UpdateHeadlights();
            if (damageIndicator != null)
                damageIndicator.sharedMaterial = Hits > 0 ? game.orange : game.cyan;
        }
    }
}
