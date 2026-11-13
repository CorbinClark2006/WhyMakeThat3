using UnityEngine;

namespace Convoy
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ConvoyNatureBatch : MonoBehaviour
    {
        [System.Serializable]
        public struct Placement
        {
            public Vector3 position;
            public float scale;
        }

        public Sprite sprite;
        public Placement[] placements;
        private Mesh ownedMesh;

        public void BuildMesh(Material material)
        {
            int count = placements.Length;
            Vector3[] vertices = new Vector3[count * 4];
            Vector2[] uv = new Vector2[count * 4];
            Vector2[] offsets = new Vector2[count * 4];
            int[] triangles = new int[count * 6];
            Rect rect = sprite.rect;
            Vector2 pivot = sprite.pivot;
            float ppu = sprite.pixelsPerUnit;
            Vector2[] corners = { Vector2.zero, new Vector2(rect.width, 0f),
                new Vector2(rect.width, rect.height), new Vector2(0f, rect.height) };
            Bounds bounds = new Bounds(placements[0].position, Vector3.zero);
            for (int i = 0; i < count; i++)
            {
                Placement placement = placements[i];
                float radius = rect.width / ppu * placement.scale;
                float height = rect.height / ppu * placement.scale;
                bounds.Encapsulate(placement.position + new Vector3(radius, height, radius));
                bounds.Encapsulate(placement.position - new Vector3(radius, height, radius));
                for (int corner = 0; corner < 4; corner++)
                {
                    int vertex = i * 4 + corner;
                    vertices[vertex] = placement.position;
                    offsets[vertex] = (corners[corner] - pivot) / ppu * placement.scale;
                    uv[vertex] = new Vector2((rect.x + corners[corner].x) / sprite.texture.width,
                        (rect.y + corners[corner].y) / sprite.texture.height);
                }
                int t = i * 6, v = i * 4;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }
            bounds.Expand(0.02f);
            ownedMesh = new Mesh { name = sprite.name + " batched scenery" };
            ownedMesh.vertices = vertices;
            ownedMesh.uv = uv;
            ownedMesh.uv2 = offsets;
            ownedMesh.triangles = triangles;
            ownedMesh.bounds = bounds;
            GetComponent<MeshFilter>().sharedMesh = ownedMesh;
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public void MarkMeshSaved()
        {
            ownedMesh = null;
        }

        private void OnDestroy()
        {
            if (ownedMesh == null)
                return;
            if (Application.isPlaying) Destroy(ownedMesh);
            else DestroyImmediate(ownedMesh);
        }
    }
}
