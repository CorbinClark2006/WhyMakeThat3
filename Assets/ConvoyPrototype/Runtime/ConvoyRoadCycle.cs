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
        private readonly List<GameObject> dirtParts = new List<GameObject>();
        private readonly List<GameObject> highwayParts = new List<GameObject>();
        private Material asphalt, shoulder, paint, dust, rut;
        private Mesh mesh;
        private bool highway;

        public void Initialize(ConvoyPrototype owner)
        {
            game = owner;
            asphalt = MakeMaterial("Highway asphalt", "#576069");
            shoulder = MakeMaterial("Highway shoulder", "#b0a987");
            paint = MakeMaterial("Highway markings", "#f2ec8b");
            dust = MakeMaterial("Dirt track earth", "#997f73");
            rut = MakeMaterial("Dirt track ruts", "#443846");
            mesh = new Mesh { name = "Pixel highway tile" };
            var vertices = new List<Vector3>();
            var triangles = new List<int>[] { new List<int>(), new List<int>(), new List<int>() };
            AddQuad(vertices, triangles[0], -7.2f, 7.2f, -12f, 12f, 0.045f);
            AddQuad(vertices, triangles[1], -7.65f, -7.2f, -12f, 12f, 0.046f);
            AddQuad(vertices, triangles[1], 7.2f, 7.65f, -12f, 12f, 0.046f);
            foreach (float x in new[] { -6.95f, 6.95f })
                AddQuad(vertices, triangles[2], x - 0.06f, x + 0.06f, -12f, 12f, 0.05f);
            foreach (float x in new[] { -2.4f, 2.4f })
                for (float z = -12f; z < 12f; z += 6f)
                    AddQuad(vertices, triangles[2], x - 0.08f, x + 0.08f, z, z + 3f, 0.05f);
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            foreach (Transform tile in game.road)
            {
                foreach (Transform part in tile)
                    if (part.name.StartsWith("Dirt lane ") || part.name == "Tire rut")
                    {
                        dirtParts.Add(part.gameObject);
                        part.GetComponent<Renderer>().sharedMaterial = part.name == "Tire rut" ? rut : dust;
                        part.gameObject.SetActive(true);
                    }
                Transform existing = tile.Find("Highway surface");
                GameObject surface = existing != null ? existing.gameObject : new GameObject("Highway surface");
                surface.transform.SetParent(tile, false);
                var filter = surface.GetComponent<MeshFilter>();
                if (filter == null) filter = surface.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var renderer = surface.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = surface.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { asphalt, shoulder, paint };
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                surface.SetActive(false);
                highwayParts.Add(surface);
            }
            Apply();
        }

        public void Apply()
        {
            bool next = game.IsHighway;
            if (next == highway) return;
            highway = next;
            foreach (GameObject part in dirtParts) part.SetActive(!highway);
            foreach (GameObject part in highwayParts) part.SetActive(highway);
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
            Release(mesh);
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
