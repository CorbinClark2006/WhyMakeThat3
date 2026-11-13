using System.Collections.Generic;
using UnityEngine;

namespace Convoy
{
    public sealed class ConvoyRoadRocks : MonoBehaviour
    {
        public const float SpawnDistance = 140f;
        public const float PressureInterval = 15f;
        public const float StartingSpawnSeconds = 7f;
        public const float MinimumSpawnSeconds = 0.85f;
        public const float RockHeight = 2.2f;
        public const float BushHeight = 1.8f;
        public const int HazardLimit = 8;
        public const int BushSpriteIndex = 5;
        public int PressureStage => Mathf.FloorToInt(game.SurvivalTime / PressureInterval);
        public float CurrentSpawnSeconds => Mathf.Max(MinimumSpawnSeconds,
            StartingSpawnSeconds / (1f + PressureStage * 1.25f));
        public float CurrentSpacing => game.CruiseSpeed * CurrentSpawnSeconds;
        public float CurrentSpawnDistance => Mathf.Max(SpawnDistance, game.CruiseSpeed * 6.5f);
        public ConvoyPrototype game;
        public readonly List<Transform> Rocks = new List<Transform>();
        private float travelSinceSpawn;
        private int spawnIndex;
        private bool started;

        public void BeginPractice()
        {
            if (started || !game.RoadHazardsEnabled)
                return;
            started = true;
            SpawnRock(0, SpawnDistance);
        }

        public Transform SpawnRock(int lane, float z)
        {
            return SpawnObstacle(lane, z, false);
        }

        public Transform SpawnBush(int lane, float z)
        {
            return SpawnObstacle(lane, z, true);
        }

        public bool IsBush(Transform obstacle) =>
            obstacle.GetComponent<SpriteRenderer>().sprite == game.natureSprites[BushSpriteIndex];

        private Transform SpawnObstacle(int lane, float z, bool bush)
        {
            if (!game.RoadHazardsEnabled)
                throw new System.InvalidOperationException("Road obstacles are only enabled in driver practice or online co-op.");
            if (lane < -1 || lane > 1)
                throw new System.ArgumentOutOfRangeException(nameof(lane));
            int spriteIndex = bush ? BushSpriteIndex : 1;
            if (game.natureSprites == null || game.natureSprites.Length <= spriteIndex || game.natureSprites[spriteIndex] == null ||
                game.natureMaterial == null)
                throw new System.InvalidOperationException("Assign the road obstacle sprites and unlit nature material.");
            Sprite sprite = game.natureSprites[spriteIndex];
            Transform rock = new GameObject(bush ? "Road obstacle bush" : "Road obstacle rock").transform;
            rock.SetParent(game.transform, false);
            rock.localPosition = new Vector3(lane * 4.8f, -0.02f, z);
            rock.localScale = Vector3.one * ((bush ? BushHeight : RockHeight) / sprite.bounds.size.y);
            SpriteRenderer renderer = rock.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = game.natureMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            rock.gameObject.AddComponent<ConvoyNatureSprite>();
            Rocks.Add(rock);
            return rock;
        }

        public void Step(float dt)
        {
            if (!game.WorldAuthority || !started || !game.RoadHazardsEnabled || !game.SimulationRunning)
                return;
            float travel = game.CurrentSpeed * dt;
            Physics.SyncTransforms();
            Bounds vanBounds = game.vanHitbox.bounds;
            for (int i = Rocks.Count - 1; i >= 0; i--)
            {
                Transform rock = Rocks[i];
                float oldZ = rock.position.z;
                rock.localPosition += Vector3.back * travel;
                float halfWidth = rock.GetComponent<SpriteRenderer>().sprite.bounds.size.x * rock.localScale.x * 0.5f;
                bool horizontalContact = rock.position.x + halfWidth >= vanBounds.min.x &&
                    rock.position.x - halfWidth <= vanBounds.max.x;
                // Sweep the rock's ground footprint so editor hitches cannot skip the van.
                bool crossedVan = oldZ + 0.8f >= vanBounds.min.z && rock.position.z - 0.8f <= vanBounds.max.z;
                if (travel > 0f && horizontalContact && crossedVan)
                {
                    game.RegisterRam(rock.position.x < vanBounds.center.x ? -1 : 1);
                    RemoveRock(i);
                    if (!game.SimulationRunning)
                        return;
                }
                else if (rock.localPosition.z < -25f)
                    RemoveRock(i);
            }
            travelSinceSpawn += travel;
            float spacing = CurrentSpacing;
            if (travelSinceSpawn >= spacing)
            {
                float overdue = travelSinceSpawn - spacing;
                travelSinceSpawn %= spacing;
                // Skip missed spawns after a hitch instead of creating a burst across all lanes.
                if (overdue >= spacing || Rocks.Count >= HazardLimit)
                    return;
                int index = spawnIndex++;
                int lane = index % 3 - 1;
                float z = CurrentSpawnDistance - overdue;
                if (z > game.van.localPosition.z + game.CruiseSpeed * 5f)
                    SpawnObstacle(lane, z, PressureStage > 0 && index % 2 == 1);
            }
        }

        private void RemoveRock(int index)
        {
            GameObject rock = Rocks[index].gameObject;
            Rocks.RemoveAt(index);
            rock.SetActive(false);
            if (Application.isPlaying) Destroy(rock);
            else DestroyImmediate(rock);
        }

        public Vector3 CaptureProgress() => new Vector3(travelSinceSpawn, spawnIndex, started ? 1f : 0f);
        public void ApplyProgress(Vector3 state)
        {
            travelSinceSpawn = state.x;
            spawnIndex = Mathf.RoundToInt(state.y);
            started = state.z > 0.5f;
        }
    }
}
