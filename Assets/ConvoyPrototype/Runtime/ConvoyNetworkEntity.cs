using Fusion;
using UnityEngine;

namespace Convoy
{
    public sealed class ConvoyNetworkEntity : NetworkBehaviour
    {
        public enum EntityKind { Enemy = 1, Ammo = 2, Rock = 3, Bush = 4 }
        [Networked] public int Kind { get; set; }
        [Networked] public ConvoyEnemyState Enemy { get; set; }
        [Networked] public ConvoyAmmoState Pack { get; set; }
        [Networked] public Vector3 RockPosition { get; set; }
        public GameObject visual;
        private ConvoyPrototype game;
        private ConvoyAmmoPack ammo;
        private PursuerVan enemy;
        private ConvoyPlayer previousCarrier;
        private bool hadAuthority;

        public void InitializeState(ConvoyPrototype owner)
        {
            game = owner;
            CacheVisual();
            Capture();
        }

        public override void Spawned()
        {
            game = Runner.GetComponent<ConvoyRunnerLink>().session.Game;
            if (visual == null)
            {
                switch ((EntityKind)Kind)
                {
                    case EntityKind.Enemy:
                        enemy = game.SpawnPursuer(Enemy.Side, Enemy.Position.z);
                        visual = enemy.gameObject;
                        break;
                    case EntityKind.Ammo:
                        ammo = PrototypeGeometry.CreateAmmoPack(game, game.ammoSupply);
                        visual = ammo.gameObject;
                        break;
                    case EntityKind.Rock:
                        visual = game.roadRocks.SpawnRock(Mathf.RoundToInt(RockPosition.x / 4.8f), RockPosition.z).gameObject;
                        break;
                    case EntityKind.Bush:
                        visual = game.roadRocks.SpawnBush(Mathf.RoundToInt(RockPosition.x / 4.8f), RockPosition.z).gameObject;
                        break;
                    default:
                        Debug.LogError("Unknown convoy network entity type: " + Kind, this);
                        return;
                }
            }
            CacheVisual();
            game.network.World.Remember(visual);
            hadAuthority = HasStateAuthority;
            if (!HasStateAuthority) Apply(false);
        }

        private void CacheVisual()
        {
            enemy = visual.GetComponent<PursuerVan>();
            ammo = visual.GetComponent<ConvoyAmmoPack>();
        }

        private void Capture()
        {
            if (enemy != null) Enemy = enemy.CaptureOnline();
            else if (ammo != null) Pack = ammo.CaptureOnline();
            else if (visual != null) RockPosition = visual.transform.localPosition;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (visual == null || !visual.activeInHierarchy || (ammo != null && ammo.Consumed) ||
                (enemy != null && enemy.IsDestroyed))
            {
                Runner.Despawn(Object);
                return;
            }
            if (!hadAuthority)
            {
                Apply(true);
                hadAuthority = true;
            }
            Capture();
        }

        public override void Render()
        {
            if (HasStateAuthority || visual == null) return;
            hadAuthority = false;
            Apply(false);
        }

        private void Apply(bool authoritative)
        {
            if (enemy != null) enemy.ApplyOnline(Enemy, authoritative);
            else if (ammo != null)
            {
                if (previousCarrier != null && previousCarrier.CarriedAmmo == ammo &&
                    (int)previousCarrier.Role != Pack.Carrier)
                    previousCarrier.SetNetworkAmmo(null);
                ammo.ApplyOnline(Pack, authoritative);
                previousCarrier = game.network.PlayerForRole(Pack.Carrier);
            }
            else visual.transform.localPosition = authoritative ? RockPosition :
                Vector3.Lerp(visual.transform.localPosition, RockPosition, Mathf.Clamp01(Time.deltaTime * 25f));
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (previousCarrier != null && previousCarrier.CarriedAmmo == ammo)
                previousCarrier.SetNetworkAmmo(null);
            if (game == null) return;
            game.network.World?.Untrack(visual);
            if (enemy != null) game.Pursuers.Remove(enemy);
            if (visual != null)
            {
                game.roadRocks.Rocks.Remove(visual.transform);
                Destroy(visual);
            }
        }
    }
}
