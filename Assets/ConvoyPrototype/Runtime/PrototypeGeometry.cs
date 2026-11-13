using System.Collections.Generic;
using UnityEngine;

namespace Convoy
{
    public static class PrototypeGeometry
    {
        public static GameObject Part(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool solid = true, PrimitiveType shape = PrimitiveType.Cube)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid)
            {
                Collider collider = part.GetComponent<Collider>();
                collider.enabled = false;
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }
            return part;
        }

        private static Transform Group(string name, Transform parent, Vector3 position)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = position;
            return group;
        }

        public static void Build(ConvoyPrototype game)
        {
            BuildRoad(game);
            BuildVan(game);
            BuildPlayer(game);
            BuildAmmoStation(game);
            BuildGunAmmoDisplay(game);
            BuildAccelerator(game);
            BuildRoadRocks(game);
        }

        public static void BuildGunAmmoDisplay(ConvoyPrototype g)
        {
            Transform display = Group("Railgun charge display", g.gun, new Vector3(0.065f, 0.15f, -0.42f));
            display.localScale = Vector3.one * 0.5f;
            Part("Recessed display housing", display, new Vector3(0f, 0f, 0.02f),
                new Vector3(0.16f, 0.09f, 0.025f), g.dark, false);
            Part("Display screen", display, Vector3.zero,
                new Vector3(0.14f, 0.07f, 0.005f), g.rubber, false);
            Transform digits = Group("Loaded charges", display, new Vector3(0f, 0f, -0.005f));
            g.gunAmmoDisplay = digits.gameObject.AddComponent<TextMesh>();
            g.gunAmmoDisplay.anchor = TextAnchor.MiddleCenter;
            g.gunAmmoDisplay.alignment = TextAlignment.Center;
            g.gunAmmoDisplay.fontSize = 64;
            g.gunAmmoDisplay.characterSize = 0.004f;
            g.gunAmmoDisplay.color = new Color(0.65f, 1f, 0.8f);
            g.gunAmmoDisplay.text = g.ShotsAvailable.ToString();
            display.gameObject.SetActive(false);
        }

        public static void BuildAmmoStation(ConvoyPrototype g)
        {
            g.ammoSupply = Group("Passenger ammo supply", g.van, new Vector3(0.9f, 0.9f, 2.5f));
            g.ammoReceiver = Group("Railgun ammo loading socket", g.van, new Vector3(0.55f, 0.35f, -1.35f));
            Part("Loading socket base", g.ammoReceiver, new Vector3(0f, -0.07f, 0f),
                new Vector3(0.34f, 0.04f, 0.34f), g.dark);
            for (int side = -1; side <= 1; side += 2)
                Part("Loading socket guide", g.ammoReceiver, new Vector3(side * 0.15f, 0f, 0f),
                    new Vector3(0.025f, 0.14f, 0.34f), g.cyan, false);
            Part("Ammo feed conduit", g.van, new Vector3(0.55f, 0.75f, -2f),
                new Vector3(0.08f, 0.08f, 1.3f), g.metal, false);
            g.supplyPack = CreateAmmoPack(g, g.ammoSupply);
        }

        public static ConvoyAmmoPack CreateAmmoPack(ConvoyPrototype g, Transform source)
        {
            Transform root = Group("Four-shot rail battery", g.van, g.van.InverseTransformPoint(source.position));
            root.localRotation = Quaternion.Inverse(g.van.rotation) * source.rotation;
            root.localScale = Vector3.one * ConvoyAmmoPack.Scale;
            root.gameObject.layer = 8;
            BoxCollider collider = root.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.16f, 0.14f, 0.2f);
            collider.isTrigger = true;
            Rigidbody body = root.gameObject.AddComponent<Rigidbody>();
            body.mass = 0.2f;
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Part("Battery casing", root, Vector3.zero, new Vector3(0.16f, 0.14f, 0.2f), g.dark, false);
            for (int cell = 0; cell < ConvoyPrototype.ShotsPerPack; cell++)
                Part("Charge cell", root, new Vector3(-0.06f + cell * 0.04f, 0.08f, 0f),
                    new Vector3(0.025f, 0.025f, 0.15f), g.cyan, false);
            ConvoyAmmoPack pack = root.gameObject.AddComponent<ConvoyAmmoPack>();
            pack.game = g;
            return pack;
        }

        private static void BuildRoad(ConvoyPrototype g)
        {
            g.road = Group("Scrolling world - moves backwards, van stays at origin", g.transform, Vector3.zero);
            for (int i = 0; i < ConvoyPrototype.SegmentCount; i++)
            {
                Transform segment = Group("Road tile " + i, g.road, new Vector3(0f, 0f,
                    ConvoyPrototype.RoadRear + i * ConvoyPrototype.SegmentLength));
                Part("Grassland", segment, new Vector3(0f, -0.22f, 0f), new Vector3(110f, 0.4f, 24f), g.grass, false);
                for (int lane = -1; lane <= 1; lane++)
                {
                    Part("Dirt lane " + (lane + 2), segment, new Vector3(lane * 4.8f, -0.015f, 0f),
                        new Vector3(3.8f, 0.05f, 24f), g.dirt, false);
                    for (int track = -1; track <= 1; track += 2)
                        Part("Tire rut", segment, new Vector3(lane * 4.8f + track * 0.95f, 0.015f, 0f),
                            new Vector3(0.13f, 0.015f, 24f), g.dark, false);
                }
                BuildRoadsideNature(g, segment, i);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Road marker", segment, new Vector3(side * 7.7f, 0.5f, 0f), new Vector3(0.15f, 1f, 0.15f), g.metal, false);
                    Part("Marker reflector", segment, new Vector3(side * 7.7f, 0.85f, -0.09f), new Vector3(0.18f, 0.18f, 0.03f), g.orange, false);
                }
            }
        }

        public static void ExtendRoad(ConvoyPrototype game)
        {
            int existing = game.road.childCount;
            if (existing == 0)
                throw new System.InvalidOperationException("Cannot extend an empty road.");
            for (int i = existing; i < ConvoyPrototype.SegmentCount; i++)
            {
                Transform tile = Object.Instantiate(game.road.GetChild(i % existing), game.road, false);
                tile.name = "Road tile " + i;
                tile.localPosition = new Vector3(0f, 0f,
                    ConvoyPrototype.RoadRear + i * ConvoyPrototype.SegmentLength);
            }

        }

        public static void ReplaceRoadsideNature(ConvoyPrototype game)
        {
            ValidateNatureSprites(game);
            int index = 0;
            foreach (Transform tile in game.road)
            {
                for (int i = tile.childCount - 1; i >= 0; i--)
                {
                    Transform child = tile.GetChild(i);
                    if (child.name != "Roadside scrub" && child.name != "Rock" && child.name != "Roadside nature sprites")
                        continue;
                    child.gameObject.SetActive(false);
                    if (Application.isPlaying) Object.Destroy(child.gameObject);
                    else Object.DestroyImmediate(child.gameObject);
                }
                BuildRoadsideNature(game, tile, index++);
            }
        }

        private static void ValidateNatureSprites(ConvoyPrototype game)
        {
            if (game.natureMaterial == null || game.natureSprites == null || game.natureSprites.Length != 9 ||
                game.natureBatchMaterials == null || game.natureBatchMaterials.Length != 9)
                throw new System.InvalidOperationException("Assign all nine roadside nature sprites and their unlit material.");
            foreach (Sprite sprite in game.natureSprites)
                if (sprite == null)
                    throw new System.InvalidOperationException("A roadside nature sprite is missing.");
            foreach (Material material in game.natureBatchMaterials)
                if (material == null)
                    throw new System.InvalidOperationException("A roadside batch material is missing.");
        }

        private static void BuildRoadsideNature(ConvoyPrototype game, Transform tile, int tileIndex)
        {
            ValidateNatureSprites(game);
            System.Random random = new System.Random(218 + tileIndex * 397);
            Transform group = Group("Roadside nature sprites", tile, Vector3.zero);
            var placements = new List<ConvoyNatureBatch.Placement>[9];
            for (int i = 0; i < placements.Length; i++)
                placements[i] = new List<ConvoyNatureBatch.Placement>();
            for (int i = 0; i < 448; i++)
            {
                int spriteIndex = i < 32 ? 0 : i < 44 ? 1 + (i - 32) % 3 :
                    i < 444 ? 4 + (i - 44) % 3 : 7 + (i - 444) % 2;
                float height = spriteIndex == 0 ? 12f + (float)random.NextDouble() * 8f :
                    spriteIndex <= 3 ? 1f + (float)random.NextDouble() * 1.5f :
                    spriteIndex <= 6 ? 0.8f + (float)random.NextDouble() * 0.7f :
                    0.4f + (float)random.NextDouble() * 0.5f;
                Sprite sprite = game.natureSprites[spriteIndex];
                float halfWidth = sprite.bounds.size.x * height / sprite.bounds.size.y * 0.5f;
                float x = 8.5f + halfWidth + (float)random.NextDouble() * (54f - 8.5f - halfWidth * 2f);
                placements[spriteIndex].Add(new ConvoyNatureBatch.Placement
                {
                    position = new Vector3(i % 2 == 0 ? -x : x, -0.02f,
                        -12f + (float)random.NextDouble() * 24f),
                    scale = height / sprite.bounds.size.y
                });
            }
            for (int i = 0; i < placements.Length; i++)
            {
                Transform item = Group(game.natureSprites[i].name + " batch", group, Vector3.zero);
                ConvoyNatureBatch batch = item.gameObject.AddComponent<ConvoyNatureBatch>();
                batch.sprite = game.natureSprites[i];
                batch.placements = placements[i].ToArray();
                batch.BuildMesh(game.natureBatchMaterials[i]);
            }
        }
        private static void BuildVan(ConvoyPrototype g)
        {
            g.van = Group("Player van - stationary suspension rig", g.transform, new Vector3(0f, 1.05f, 0f));
            BuildVanHitbox(g);
            for (int side = -1; side <= 1; side += 2)
                Part("Interior light strip", g.van, new Vector3(side * 1.4f, 2.28f, -0.5f),
                    new Vector3(0.08f, 0.05f, 4.6f), g.cyan, false);

            Part("Gunner stool - step up here", g.van, new Vector3(0f, 0.31f, -1.35f), new Vector3(0.8f, 0.36f, 0.8f), g.dark);
            Part("Stool tread", g.van, new Vector3(0f, 0.5f, -1.35f), new Vector3(0.85f, 0.04f, 0.85f), g.orange);
            Transform cargo = Group("Jumpable cargo boxes", g.van, Vector3.zero);
            for (int step = 0; step < 3; step++)
            {
                float height = 0.3f * (step + 1);
                float z = 0.95f - step * 0.95f;
                Part("Left cargo box " + (step + 1), cargo, new Vector3(-1.05f, 0.14f + height * 0.5f, z),
                    new Vector3(0.8f, height, 0.8f), step % 2 == 0 ? g.orange : g.metal);
                Part("Right cargo box " + (step + 1), cargo, new Vector3(1.05f, 0.14f + height * 0.5f, z + 0.7f),
                    new Vector3(0.8f, height, 0.8f), step % 2 == 0 ? g.metal : g.orange);
            }
            Part("Partition rear step", cargo, new Vector3(0.48f, 1.05f, 0.65f),
                new Vector3(0.8f, 0.6f, 0.8f), g.metal);
            Part("Partition front step", cargo, new Vector3(0f, 1.24f, 1.45f),
                new Vector3(0.64f, 0.3f, 0.56f), g.dark);
            Part("Gun pedestal", g.van, new Vector3(0f, 0.65f, -2.8f), new Vector3(0.2f, 0.55f, 0.2f), g.dark, true, PrimitiveType.Cylinder);
            Part("Gun mount", g.van, new Vector3(0f, 1.2f, -2.8f), new Vector3(0.6f, 0.13f, 0.6f), g.metal, true, PrimitiveType.Cylinder);
            g.gun = Group("Rear railgun - aim pivot", g.van, new Vector3(0f, 1.35f, -2.8f));
            g.gun.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Part("Breech", g.gun, new Vector3(0f, -0.06f, 0f), new Vector3(0.45f, 0.26f, 0.7f), g.dark);
            for (int side = -1; side <= 1; side += 2)
            {
                Part("Accelerator rail", g.gun, new Vector3(side * 0.16f, 0f, 0.9f), new Vector3(0.1f, 0.15f, 1.65f), g.metal);
                Part("Charged coil", g.gun, new Vector3(side * 0.22f, 0.02f, 0.85f), new Vector3(0.04f, 0.07f, 1.4f), g.cyan, false);
                Part("Rear grip", g.gun, new Vector3(side * 0.32f, -0.12f, -0.25f), new Vector3(0.13f, 0.26f, 0.12f), g.dark, false);
            }
            Part("Optic base", g.gun, new Vector3(0f, 0.08f, -0.15f), new Vector3(0.18f, 0.05f, 0.2f), g.orange, false);
            g.muzzle = Group("Muzzle", g.gun, new Vector3(0f, 0f, 1.75f));
            Transform optic = Group("Railgun optic camera", g.gun, new Vector3(0f, 0.2f, -0.65f));
            g.gunCamera = optic.gameObject.AddComponent<Camera>();
            ConfigureCamera(g.gunCamera, 52f);
            g.gunCamera.enabled = false;
            optic.gameObject.AddComponent<AudioListener>().enabled = false;
            InstallMainVanModel(g);
            SetLayer(g.van, 8);
        }

        public static bool IsLegacyVanShellPart(string name)
        {
            return name == "Steel floor" || name == "Floor walkway" || name == "Cargo wall" ||
                name == "Cab window sill" || name == "Interior wall rib" ||
                name == "Rear door pillar" || name == "Open rear door" || name == "Rear taillight" ||
                name == "Roof" || name == "Front nose" || name == "Front windshield";
        }

        public static void RemoveLegacyVanParts(Transform van)
        {
            List<GameObject> parts = new List<GameObject>();
            foreach (Transform child in van)
                if (IsLegacyVanShellPart(child.name) || child.name.Contains("frontseperation") ||
                    child.name.StartsWith("Driver seat") || child.name.StartsWith("Seat back") ||
                    child.name == "Dashboard" || child.name == "Autopilot display" ||
                    child.name == "Driverless steering column" || child.name == "Rear safety lip" ||
                    child.name == "Wheel" || child.name == "Passenger seat - ammo supply")
                    parts.Add(child.gameObject);
            foreach (GameObject part in parts)
            {
                if (Application.isPlaying) Object.Destroy(part);
#if UNITY_EDITOR
                else UnityEditor.Undo.DestroyObjectImmediate(part);
#else
                else Object.DestroyImmediate(part);
#endif
            }
        }

        public static void InstallMainVanModel(ConvoyPrototype game)
        {
            if (game.mainVanModelPrefab == null)
                throw new System.InvalidOperationException("Assign the intact v008 player van prefab.");
            RemoveLegacyVanParts(game.van);
            game.mainVanModel = Object.Instantiate(game.mainVanModelPrefab, game.van.transform, false).transform;
            game.mainVanModel.name = "Intact v008 player van";
            SetLayer(game.mainVanModel, 8);
        }

        public static void BuildVanHitbox(ConvoyPrototype g)
        {
            Transform hull = Group("Main van damage hitbox", g.van, new Vector3(0f, 0.9f, 0f));
            hull.gameObject.layer = 8;
            g.vanHitbox = hull.gameObject.AddComponent<BoxCollider>();
            g.vanHitbox.center = new Vector3(0f, 0.6f, 1.25f);
            g.vanHitbox.size = new Vector3(3.5f, 3.5f, 9.5f);
            g.vanHitbox.isTrigger = true;
        }

        private static void BuildPlayer(ConvoyPrototype g)
        {
            Transform player = Group("Third person gunner", g.van, new Vector3(-0.97f, 0.14f, -1.5f));
            player.localRotation = Quaternion.Euler(0f, 180f, 0f);
            player.gameObject.layer = 8;
            CharacterController capsule = player.gameObject.AddComponent<CharacterController>();
            capsule.height = ConvoyPlayer.BodyHeight;
            capsule.radius = 0.043f;
            capsule.center = new Vector3(0f, ConvoyPlayer.BodyHeight * 0.5f + 0.01f, 0f);
            capsule.stepOffset = 0.03f;
            capsule.skinWidth = 0.005f;
            capsule.minMoveDistance = 0f;
            g.player = player.gameObject.AddComponent<ConvoyPlayer>();
            g.player.game = g;
            Quaternion orbit = Quaternion.Euler(18f, 0f, 0f);
            Transform eye = Group("Player eye", player, Vector3.up * ConvoyPlayer.EyeHeight +
                orbit * Vector3.back * ConvoyPlayer.CameraDistance);
            eye.localRotation = orbit;
            g.player.view = eye.gameObject.AddComponent<Camera>();
            g.player.view.tag = "MainCamera";
            ConfigureCamera(g.player.view, 68f);
            g.player.view.nearClipPlane = 0.015f;
            eye.gameObject.AddComponent<AudioListener>();
            BuildAvatar(g.player);
        }

        public static void BuildAvatar(ConvoyPlayer player)
        {
            ConvoySpriteAvatar prefab = player.Role == ConvoyPrototype.PlayerRole.Driver
                ? player.game.player2AvatarPrefab : player.game.playerAvatarPrefab;
            if (prefab == null)
                throw new System.InvalidOperationException("Assign the selected role's sprite avatar prefab before building the player.");
            if (player.avatar != null)
            {
                player.avatar.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Object.Destroy(player.avatar.gameObject);
                else
                    Object.DestroyImmediate(player.avatar.gameObject);
            }
            player.spriteAvatar = Object.Instantiate(prefab, player.transform, false);
            player.spriteAvatar.name = player.Role == ConvoyPrototype.PlayerRole.Driver ? "Player 2 sprite avatar" : "Player 1 sprite avatar";
            player.avatar = player.spriteAvatar.transform;
            SetLayer(player.avatar, 8);
        }

        private static void ConfigureCamera(Camera camera, float fov)
        {
            camera.fieldOfView = fov;
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = ConvoyPrototype.ViewDistance;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.65f, 0.65f, 0.65f);
            camera.allowHDR = true;
        }

        public static void BuildDriverStation(ConvoyPrototype game)
        {
            if (game.van.Find("Driver steering wheel") != null)
                return;
            Transform wheel = Group("Driver steering wheel", game.van, ConvoyPrototype.SteeringWheelPosition);
            wheel.localRotation = Quaternion.Euler(20f, 0f, 0f);
            for (int segment = 0; segment < 24; segment++)
            {
                float angle = segment * Mathf.PI * 2f / 24f;
                GameObject rim = Part("Wheel rim", wheel,
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.22f,
                    new Vector3(0.065f, 0.025f, 0.035f), game.dark, false);
                rim.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
            }

            Part("Wheel hub", wheel, Vector3.zero, new Vector3(0.08f, 0.08f, 0.06f), game.metal, false);
            for (int spoke = 0; spoke < 3; spoke++)
            {
                float angle = (90f + spoke * 120f) * Mathf.Deg2Rad;
                GameObject bar = Part("Wheel spoke", wheel,
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.11f,
                    new Vector3(0.22f, 0.025f, 0.025f), game.metal, false);
                bar.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            }
        }

        public static void BuildRoadRocks(ConvoyPrototype game)
        {
            if (game.roadRocks != null)
                return;
            game.roadRocks = game.gameObject.AddComponent<ConvoyRoadRocks>();
            game.roadRocks.game = game;
        }

        public static void BuildAccelerator(ConvoyPrototype game)
        {
            if (game.accelerator != null)
                return;
            Transform station = Group("Driver pedals", game.van, Vector3.zero);
            game.accelerator = station.gameObject.AddComponent<ConvoyAccelerator>();
            game.accelerator.game = game;
            game.accelerator.pedal = Part("Accelerator pedal", station, ConvoyAccelerator.PedalPosition,
                new Vector3(0.22f, 0.035f, 0.28f), game.dark, false).transform;
            Part("Brake pedal - decorative", station, ConvoyAccelerator.PedalPosition + Vector3.left * 0.33f,
                new Vector3(0.25f, 0.035f, 0.22f), game.rubber, false);
            game.accelerator.rock = Part("Accelerator rock", game.van, Vector3.zero,
                new Vector3(0.18f, 0.12f, 0.2f), game.metal, false, PrimitiveType.Sphere).transform;
            game.accelerator.InitializeRock();
        }

        public static PursuerVan CreatePursuer(ConvoyPrototype g, int side, float z)
        {
            Transform root = Group("Chaser - two hits", g.transform, new Vector3(side * 4.8f, 0f, z));
            PursuerVan enemy = root.gameObject.AddComponent<PursuerVan>();
            enemy.Initialize(g, side);
            root = Group("Chaser body - hit recoil", root, Vector3.zero);
            enemy.visualRoot = root;
            if (g.pursuerVanModelPrefab == null)
                throw new System.InvalidOperationException("Assign the intact v001 pursuer van prefab.");
            Transform model = Object.Instantiate(g.pursuerVanModelPrefab, root, false).transform;
            model.name = "v001 pursuer van shell";
            Bounds bounds = GetModelBounds(model);
            BoxCollider hull = root.gameObject.AddComponent<BoxCollider>();
            hull.center = bounds.center;
            hull.size = bounds.size;
            enemy.damageIndicator = Part("Armor status - light intact, dark damaged", root,
                new Vector3(0f, bounds.max.y + 0.08f, 0.2f),
                new Vector3(1.2f, 0.13f, 0.2f), g.cyan, false).GetComponent<Renderer>();
            enemy.wheels = new Transform[0];
            return enemy;
        }

        public static Bounds GetModelBounds(Transform root)
        {
            bool found = false;
            Bounds bounds = default;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                Bounds local = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f,
                            (corner & 4) == 0 ? -1f : 1f));
                    point = root.InverseTransformPoint(renderer.transform.TransformPoint(point));
                    if (!found)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                    else
                        bounds.Encapsulate(point);
                }
            }
            if (!found)
                throw new System.InvalidOperationException("Van model has no rendered geometry: " + root.name);
            return bounds;
        }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
                SetLayer(child, layer);
        }
    }
}
