using System.Collections.Generic;
using UnityEngine;

namespace Convoy
{
    public sealed class ConvoyRepairFires : MonoBehaviour
    {
        public const int FireLimit = ConvoyPrototype.MaximumHealth - 1;
        public const float StompRadius = 0.24f;
        public const float MinimumJumpRise = 0.12f;
        private ConvoyPrototype game;
        private Transform cargo;
        private readonly Transform[] spots = new Transform[FireLimit];
        private readonly SpriteRenderer[] flames = new SpriteRenderer[FireLimit];
        private readonly Dictionary<ConvoyPlayer, Jump> jumps = new Dictionary<ConvoyPlayer, Jump>();
        private readonly Sprite[] frames = new Sprite[4];
        private Texture2D texture;
        private Sprite scorchSprite;
        private Texture2D scorchTexture;
        public int Mask { get; private set; }
        public int Count
        {
            get
            {
                int count = 0;
                for (int i = 0; i < FireLimit; i++) if ((Mask & (1 << i)) != 0) count++;
                return count;
            }
        }

        private struct Jump
        {
            public float LaunchY, PeakY, Started;
        }

        public Transform Spot(int index) => spots[index];

        public void Initialize(ConvoyPrototype owner)
        {
            game = owner;
            cargo = game.van.Find("Jumpable cargo boxes");
            for (int i = game.van.childCount - 1; i >= 0; i--)
            {
                Transform child = game.van.GetChild(i);
                if (child.name != "Cargo repair fire" && child.name != "Hood repair fire") continue;
                child.gameObject.SetActive(false);
                Release(child.gameObject);
            }
            BakeSprites();
            Physics.SyncTransforms();
            Vector2[] positions = { new Vector2(-0.8f, -2f), new Vector2(-0.55f, 4.7f),
                new Vector2(0.8f, -2f), new Vector2(0.55f, 4.7f) };
            for (int i = 0; i < FireLimit; i++)
            {
                Vector2 p = positions[i];
                float ceiling = p.y < 0f ? 2f : 3f;
                Vector3 origin = game.van.TransformPoint(new Vector3(p.x, ceiling, p.y));
                float nearest = float.PositiveInfinity;
                Vector3 point = Vector3.zero;
                foreach (RaycastHit hit in Physics.RaycastAll(origin, -game.van.up, ceiling + 1f,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore))
                {
                    if (!IsRepairSurface(hit.collider) ||
                        Vector3.Dot(hit.normal, game.van.up) < 0.7f || hit.distance >= nearest) continue;
                    nearest = hit.distance;
                    point = game.van.InverseTransformPoint(hit.point);
                }
                if (float.IsPositiveInfinity(nearest))
                    throw new System.InvalidOperationException("No walkable van surface for repair fire " + i + ".");
                var root = new GameObject(i % 2 == 0 ? "Cargo repair fire" : "Hood repair fire");
                root.transform.SetParent(game.van, false);
                root.transform.localPosition = point + Vector3.up * 0.012f;
                spots[i] = root.transform;
                var scorch = new GameObject("Pixel scorch");
                scorch.transform.SetParent(root.transform, false);
                scorch.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var baseRenderer = scorch.AddComponent<SpriteRenderer>();
                baseRenderer.sprite = scorchSprite;
                baseRenderer.sharedMaterial = game.natureMaterial;
                var flame = new GameObject("Pixel flame");
                flame.transform.SetParent(root.transform, false);
                flames[i] = flame.AddComponent<SpriteRenderer>();
                flames[i].sharedMaterial = game.natureMaterial;
                flames[i].sprite = frames[i % frames.Length];
                root.SetActive(false);
            }
        }

        public void AddDamageFire()
        {
            if (!game.WorldAuthority) throw new System.InvalidOperationException("Only world authority can create repair fires.");
            for (int i = 0; i < FireLimit; i++)
                if ((Mask & (1 << i)) == 0)
                {
                    ApplyMask(Mask | (1 << i));
                    return;
                }
            throw new System.InvalidOperationException("Repair fires exceeded recoverable van damage.");
        }

        public void ApplyMask(int mask)
        {
            if (mask < 0 || mask >= (1 << FireLimit))
                throw new System.ArgumentOutOfRangeException(nameof(mask));
            Mask = mask;
            for (int i = 0; i < FireLimit; i++)
                spots[i].gameObject.SetActive((mask & (1 << i)) != 0);
        }

        public bool BeginJump(ConvoyPlayer actor) => BeginJump(actor, actor.transform.localPosition);

        public bool BeginJump(ConvoyPlayer actor, Vector3 launch)
        {
            if (!game.WorldAuthority || !game.SimulationRunning || !game.ContainsPlayer(actor) ||
                actor.Mounted || actor.Driving || actor.IsRespawning || !OnSurface(launch))
                return false;
            float y = launch.y;
            jumps[actor] = new Jump { LaunchY = y, PeakY = y, Started = Time.time };
            return true;
        }

        public void ObserveJump(ConvoyPlayer actor, Vector3 position)
        {
            if (!jumps.TryGetValue(actor, out Jump jump)) return;
            if (actor.Mounted || actor.Driving || actor.IsRespawning || Time.time - jump.Started > 2f)
            {
                jumps.Remove(actor);
                return;
            }
            jump.PeakY = Mathf.Max(jump.PeakY, position.y);
            jumps[actor] = jump;
        }

        public bool TryStomp(ConvoyPlayer actor) => TryStomp(actor, actor.transform.localPosition);

        public bool TryStomp(ConvoyPlayer actor, Vector3 feet)
        {
            if (!game.WorldAuthority || !game.ContainsPlayer(actor) || !jumps.TryGetValue(actor, out Jump jump)) return false;
            jumps.Remove(actor);
            if (!game.SimulationRunning || actor.Mounted || actor.Driving || actor.IsRespawning ||
                Time.time - jump.Started > 2f || jump.PeakY - jump.LaunchY < MinimumJumpRise ||
                !OnSurface(feet)) return false;
            for (int i = 0; i < FireLimit; i++)
            {
                Vector3 offset = feet - spots[i].localPosition;
                if ((Mask & (1 << i)) == 0 || Mathf.Abs(offset.y) > 0.09f ||
                    new Vector2(offset.x, offset.z).sqrMagnitude > StompRadius * StompRadius) continue;
                if (!game.RestoreHealth()) return false;
                ApplyMask(Mask & ~(1 << i));
                game.RecordLesson(actor, ConvoyTutorial.Lesson.FireStomp);
                return true;
            }
            return false;
        }

        private bool OnSurface(Vector3 feet)
        {
            Physics.SyncTransforms();
            Vector3 origin = game.van.TransformPoint(feet + Vector3.up * 0.065f);
            foreach (RaycastHit hit in Physics.RaycastAll(origin, -game.van.up, 0.14f,
                Physics.AllLayers, QueryTriggerInteraction.Ignore))
                if (IsRepairSurface(hit.collider) &&
                    Vector3.Dot(hit.normal, game.van.up) > 0.7f) return true;
            return false;
        }

        private bool IsRepairSurface(Collider collider) => collider.transform.IsChildOf(game.mainVanModel) ||
            (cargo != null && collider.transform.IsChildOf(cargo));

        public Transform NearestFire(ConvoyPlayer actor)
        {
            Transform nearest = null;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < FireLimit; i++)
            {
                if ((Mask & (1 << i)) == 0) continue;
                float candidate = (spots[i].position - actor.transform.position).sqrMagnitude;
                if (candidate < distance) { distance = candidate; nearest = spots[i]; }
            }
            return nearest;
        }

        public void ResetJumps() => jumps.Clear();

        private void LateUpdate()
        {
            if (game == null || game.player == null) return;
            Camera camera = game.player.Mounted ? game.gunCamera : game.player.view;
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, game.van.up);
            int frame = Mathf.FloorToInt(Time.time * 8f);
            for (int i = 0; i < FireLimit; i++)
            {
                if ((Mask & (1 << i)) == 0) continue;
                flames[i].sprite = frames[(frame + i) % frames.Length];
                if (forward.sqrMagnitude > 0.001f)
                    flames[i].transform.rotation = Quaternion.LookRotation(forward, game.van.up);
            }
        }

        private void BakeSprites()
        {
            const int width = 18, height = 26;
            texture = new Texture2D(width * frames.Length, height, TextureFormat.RGBA32, false)
                { name = "Convoy pixel fire atlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * frames.Length * height];
            Color32[] palette = { ConvoyPixelUI.Ink, ConvoyPixelUI.Rust, ConvoyPixelUI.Copper,
                ConvoyPixelUI.Gold, ConvoyPixelUI.Paper };
            for (int f = 0; f < frames.Length; f++)
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int top = 18 + (f % 2) * 3;
                        int half = Mathf.Max(1, 7 - y / 3);
                        int center = 8 + (y > 9 ? (f % 3) - 1 : 0);
                        bool body = y < top && Mathf.Abs(x - center) <= half;
                        bool tongue = y > 7 && y < 16 + (3 - f) && Mathf.Abs(x - (4 + f)) <= Mathf.Max(0, 2 - (y - 7) / 4);
                        bool spark = y == 23 - f && (x == 12 - f || x == 13 - f);
                        if (!body && !tongue && !spark) continue;
                        int color = spark ? 3 : y < 2 ? 0 :
                            Mathf.Abs(x - center) >= half || y > top - 3 ? 1 :
                            y > 12 || Mathf.Abs(x - center) > half - 2 ? 2 :
                            y > 7 || Mathf.Abs(x - 8) > 2 ? 3 : 4;
                        pixels[y * width * frames.Length + f * width + x] = palette[color];
                    }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            for (int f = 0; f < frames.Length; f++)
                frames[f] = Sprite.Create(texture, new Rect(f * width, 0, width, height),
                    new Vector2(0.5f, 0f), 60f, 0, SpriteMeshType.FullRect);
            scorchTexture = new Texture2D(18, 12, TextureFormat.RGBA32, false)
                { name = "Pixel scorch", filterMode = FilterMode.Point };
            var scorchPixels = new Color32[18 * 12];
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 18; x++)
                    if ((x - 8) * (x - 8) + (y - 5) * (y - 5) * 2 < 65)
                        scorchPixels[y * 18 + x] = (x + y) % 4 == 0 ? ConvoyPixelUI.Rust : ConvoyPixelUI.Ink;
            scorchTexture.SetPixels32(scorchPixels);
            scorchTexture.Apply(false, true);
            scorchSprite = Sprite.Create(scorchTexture, new Rect(0, 0, 18, 12), Vector2.one * 0.5f, 45f);
        }

        private void OnDestroy()
        {
            foreach (Transform spot in spots) if (spot != null) Release(spot.gameObject);
            foreach (Sprite frame in frames) Release(frame);
            Release(scorchSprite);
            Release(texture);
            Release(scorchTexture);
        }

        private static void Release(Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }
    }
}
