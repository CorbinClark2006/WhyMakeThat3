using System.Collections.Generic;
using UnityEngine;

namespace Convoy
{
    public sealed class ConvoyRoadRocks : MonoBehaviour
    {
        public const float SpawnDistance = 140f;
        public const float Spacing = 300f;
        public const float RockHeight = 2.2f;
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
            if (!game.RoadHazardsEnabled)
                throw new System.InvalidOperationException("Road rocks are only enabled in Player 2 practice.");
            if (lane < -1 || lane > 1)
                throw new System.ArgumentOutOfRangeException(nameof(lane));
            if (game.natureSprites == null || game.natureSprites.Length < 2 || game.natureSprites[1] == null ||
                game.natureMaterial == null)
                throw new System.InvalidOperationException("Assign the road rock sprite and unlit nature material.");
            Sprite sprite = game.natureSprites[1];
            Transform rock = new GameObject("Road obstacle rock").transform;
            rock.SetParent(game.transform, false);
            rock.localPosition = new Vector3(lane * 4.8f, -0.02f, z);
            rock.localScale = Vector3.one * (RockHeight / sprite.bounds.size.y);
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
            while (travelSinceSpawn >= Spacing)
            {
                travelSinceSpawn -= Spacing;
                int lane = spawnIndex++ % 3 - 1;
                float z = SpawnDistance - travelSinceSpawn;
                if (z >= -25f)
                    SpawnRock(lane, z);
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
