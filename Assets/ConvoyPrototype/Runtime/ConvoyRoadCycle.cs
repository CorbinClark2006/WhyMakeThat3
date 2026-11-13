using System.Collections.Generic;
using UnityEngine;

namespace Convoy
{
    public sealed class ConvoyRoadCycle : MonoBehaviour
    {
        public const float DirtDuration = 30f;
        public const float HighwayDuration = 15f;
        public const float CycleDuration = DirtDuration + HighwayDuration;
        public static bool HighwayAt(float time) => Mathf.Repeat(time, CycleDuration) >= DirtDuration;
        public static float RemainingAt(float time) => HighwayAt(time) ?
            CycleDuration - Mathf.Repeat(time, CycleDuration) : DirtDuration - Mathf.Repeat(time, CycleDuration);

        private ConvoyPrototype game;
        private readonly List<Tile> tiles = new List<Tile>();
        private readonly List<Vector2> highways = new List<Vector2>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>() };
        private Material asphalt, shoulder, paint, dust, rut;
        private float lastTime, lastDistance;
        public Vector4 History { get; private set; }
        public int BoundarySequence { get; private set; }

        private sealed class Tile
        {
            public Transform Root;
            public GameObject Surface;
            public Mesh Mesh;
            public readonly List<Vector2> Ranges = new List<Vector2>();
        }

        public static float CruiseDistanceAt(float time, float speed)
        {
            int stage = Mathf.FloorToInt(time / ConvoyPrototype.DifficultyInterval);
            return speed * (ConvoyPrototype.DifficultyInterval * (stage + 0.05f * stage * (stage - 1)) +
                (time - stage * ConvoyPrototype.DifficultyInterval) * (1f + stage * 0.1f));
        }

        public static float BoundaryTime(int sequence) => (sequence / 2) * CycleDuration +
            (sequence % 2 == 1 ? DirtDuration : 0f);

        public void Initialize(ConvoyPrototype owner)
        {
            game = owner;
            asphalt = MakeMaterial("Highway asphalt", "#576069");
            shoulder = MakeMaterial("Highway shoulder", "#b0a987");
            paint = MakeMaterial("Highway markings", "#f2ec8b");
            dust = MakeMaterial("Dirt track earth", "#997f73");
            rut = MakeMaterial("Dirt track ruts", "#443846");
            foreach (Transform tile in game.road)
            {
                foreach (Transform part in tile)
                    if (part.name.StartsWith("Dirt lane ") || part.name == "Tire rut")
                    {
                        part.GetComponent<Renderer>().sharedMaterial = part.name == "Tire rut" ? rut : dust;
                        part.gameObject.SetActive(true);
                    }
                Transform existing = tile.Find("Highway surface");
                GameObject surface = existing != null ? existing.gameObject : new GameObject("Highway surface");
                surface.transform.SetParent(tile, false);
                var filter = surface.GetComponent<MeshFilter>();
                if (filter == null) filter = surface.AddComponent<MeshFilter>();
                var mesh = new Mesh { name = "Scrolling highway tile" };
                mesh.MarkDynamic();
                filter.sharedMesh = mesh;
                var renderer = surface.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = surface.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { asphalt, shoulder, paint };
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                surface.SetActive(false);
                tiles.Add(new Tile { Root = tile, Surface = surface, Mesh = mesh });
            }
            Apply();
        }

        public void Apply()
        {
            float time = game.SurvivalTime, distance = game.DistanceTravelled;
            if (game.WorldAuthority)
            {
                if (time < lastTime)
                {
                    History = Vector4.zero;
                    BoundarySequence = 0;
                }
                while (BoundaryTime(BoundarySequence + 1) <= time)
                {
                    float boundary = BoundaryTime(BoundarySequence + 1);
                    float previousNominal = CruiseDistanceAt(lastTime, game.roadSpeed);
                    float interval = CruiseDistanceAt(time, game.roadSpeed) - previousNominal;
                    float travel = Mathf.Lerp(lastDistance, distance,
                        interval > 0f ? Mathf.Clamp01((CruiseDistanceAt(boundary, game.roadSpeed) -
                            previousNominal) / interval) : 1f);
                    History = new Vector4(travel, History.x, History.y, History.z);
                    BoundarySequence++;
                }
            }
            lastTime = time;
            lastDistance = distance;
            highways.Clear();
            float nominal = CruiseDistanceAt(time, game.roadSpeed);
            float fraction = game.accelerator != null ? Mathf.Max(0.05f, game.accelerator.SpeedFraction) : 1f;
            int first = Mathf.Max(1, BoundarySequence - 3);
            float start = BoundarySequence >= 4 && first % 2 == 0 ? ConvoyPrototype.RoadRear - 24f : 0f;
            for (int sequence = first; ; sequence++)
            {
                float z = sequence <= BoundarySequence ? History[BoundarySequence - sequence] - distance :
                    (CruiseDistanceAt(BoundaryTime(sequence), game.roadSpeed) - nominal) * fraction;
                if (sequence % 2 == 1) start = z;
                else if (z > ConvoyPrototype.RoadRear - 24f)
                    highways.Add(new Vector2(start, z));
                if (z > ConvoyPrototype.ViewDistance + 48f)
                {
                    if (sequence % 2 == 1) highways.Add(new Vector2(start, z + ConvoyPrototype.ViewDistance));
                    break;
                }
            }
            foreach (Tile tile in tiles) ApplyTile(tile);
        }

        public void ApplyHistory(Vector4 history, int sequence)
        {
            History = history;
            BoundarySequence = sequence;
        }

        public bool HighwayAtRoadPosition(float z)
        {
            foreach (Vector2 range in highways) if (z >= range.x && z < range.y) return true;
            return false;
        }

        private void ApplyTile(Tile tile)
        {
            float center = tile.Root.localPosition.z;
            int count = 0;
            bool changed = false;
            foreach (Vector2 highway in highways)
            {
                var range = new Vector2(Mathf.Max(-12f, highway.x - center), Mathf.Min(12f, highway.y - center));
                if (range.x >= range.y) continue;
                if (count >= tile.Ranges.Count) { tile.Ranges.Add(range); changed = true; }
                else if ((tile.Ranges[count] - range).sqrMagnitude > 0.000001f)
                { tile.Ranges[count] = range; changed = true; }
                count++;
            }
            if (count != tile.Ranges.Count)
            {
                tile.Ranges.RemoveRange(count, tile.Ranges.Count - count);
                changed = true;
            }
            tile.Surface.SetActive(count > 0);
            if (!changed) return;
            vertices.Clear();
            foreach (List<int> indices in triangles) indices.Clear();
            foreach (Vector2 range in tile.Ranges)
            {
                AddQuad(vertices, triangles[0], -7.2f, 7.2f, range.x, range.y, 0.045f);
                AddQuad(vertices, triangles[1], -7.65f, -7.2f, range.x, range.y, 0.046f);
                AddQuad(vertices, triangles[1], 7.2f, 7.65f, range.x, range.y, 0.046f);
                foreach (float x in new[] { -6.95f, 6.95f })
                    AddQuad(vertices, triangles[2], x - 0.06f, x + 0.06f, range.x, range.y, 0.05f);
                foreach (float x in new[] { -2.4f, 2.4f })
                    for (float z = -12f; z < 12f; z += 6f)
                    {
                        float back = Mathf.Max(z, range.x), front = Mathf.Min(z + 3f, range.y);
                        if (back < front) AddQuad(vertices, triangles[2], x - 0.08f, x + 0.08f, back, front, 0.05f);
                    }
            }
            tile.Mesh.Clear();
            tile.Mesh.SetVertices(vertices);
            tile.Mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) tile.Mesh.SetTriangles(triangles[i], i);
            tile.Mesh.RecalculateNormals();
            tile.Mesh.RecalculateBounds();
        }

        private Material MakeMaterial(string name, string hex)
        {
            if (game.dirt == null) throw new System.InvalidOperationException("Missing road surface material.");
            ColorUtility.TryParseHtmlString(hex, out Color color);
            var material = new Material(game.dirt) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            return material;
        }

        private static void AddQuad(List<Vector3> vertices, List<int> indices,
            float left, float right, float back, float front, float y)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(left, y, back));
            vertices.Add(new Vector3(left, y, front));
            vertices.Add(new Vector3(right, y, front));
            vertices.Add(new Vector3(right, y, back));
            indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        private void OnDestroy()
        {
            foreach (Tile tile in tiles) Release(tile.Mesh);
            Release(asphalt);
            Release(shoulder);
            Release(paint);
            Release(dust);
            Release(rut);
        }

        private static void Release(Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }
    }
}
