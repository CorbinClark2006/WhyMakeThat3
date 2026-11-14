using Convoy;
using NUnit.Framework;
using UnityEngine;

public sealed class ConvoyMechanicsTests
{
    private GameObject root;
    private ConvoyPrototype game;
    private Material material;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Convoy test fixture");
        game = root.AddComponent<ConvoyPrototype>();
        game.showRoleSelection = false;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        game.metal = game.dark = game.dirt = game.grass = game.foliage =
            game.orange = game.cyan = game.glass = game.rubber = material;
            game.playerAvatarPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<ConvoySpriteAvatar>(
                "Assets/ConvoyPrototype/Player1SpriteAvatar.prefab");
            game.player2AvatarPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<ConvoySpriteAvatar>(
                "Assets/ConvoyPrototype/Player2SpriteAvatar.prefab");
            string[] natureNames = { "Tree1", "BIG_funky_rock", "funky_rock", "funky_rock_3",
                "funky_grass", "funky_grass_2", "funky_grasss_3", "funky_shroom_1", "funky_shroom_3" };
            game.natureSprites = new Sprite[natureNames.Length];
            game.natureBatchMaterials = new Material[natureNames.Length];
            for (int i = 0; i < game.natureSprites.Length; i++)
            {
                game.natureSprites[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Game Assets/nature/" + natureNames[i] + ".png");
                game.natureBatchMaterials[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/ConvoyPrototype/Materials/Nature " + natureNames[i] + ".mat");
            }
            game.natureMaterial = game.playerAvatarPrefab.spriteRenderer.sharedMaterial;
            game.mainVanModelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ConvoyPrototype/PlayerVanV008.prefab");
            game.pursuerVanModelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ConvoyPrototype/PursuerVanV001.prefab");
            PrototypeGeometry.Build(game);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(material);
    }

    [Test]
    public void BrandingUsesSuppliedPixelLogoAndAuthoredCargoHasNoUnattachedRoots()
    {
        Assert.That(ConvoyPrototype.GameTitle, Is.EqualTo("All Roads Lead To Gnome"));
        Texture2D logo = Resources.Load<Texture2D>("GnomeLogo");
        Assert.That(logo, Is.Not.Null);
        Assert.That(logo.width, Is.EqualTo(96));
        Assert.That(logo.height, Is.EqualTo(64));
        Assert.That(logo.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(logo.mipmapCount, Is.EqualTo(1));
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/Scenes/ConvoyPrototype.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            ConvoyPrototype authored = null;
            foreach (GameObject item in scene.GetRootGameObjects())
            {
                if (item.TryGetComponent(out ConvoyPrototype owner)) authored = owner;
                string source = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item);
                Assert.That(source, Is.Not.EqualTo("Assets/Game Assets/in_van_assets/CardboardBox1.fbx"));
                Assert.That(source, Is.Not.EqualTo("Assets/Game Assets/in_van_assets/GnomeKiller.fbx"));
                Assert.That(source, Is.Not.EqualTo("Assets/Game Assets/in_van_assets/trashbag_v001.fbx"));
                Assert.That(source, Is.Not.EqualTo("Assets/Game Assets/in_van_assets/beartrap_v001.fbx"));
            }
            Assert.That(authored, Is.Not.Null);
            Transform cargo = authored.van.Find("Secured authored cargo");
            Assert.That(cargo, Is.Not.Null);
            Assert.That(cargo.childCount, Is.EqualTo(16), "Keep all twelve boxes and four authored decorations.");
            foreach (Transform prop in cargo)
            {
                Assert.That(prop.IsChildOf(authored.van), Is.True);
                foreach (Transform child in prop.GetComponentsInChildren<Transform>(true))
                    Assert.That(UnityEditor.GameObjectUtility.GetStaticEditorFlags(child.gameObject),
                        Is.EqualTo((UnityEditor.StaticEditorFlags)0),
                        "Van-mounted decorations must not be statically batched into world space.");
            }
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void SecuringDecorativeCargoLocksPhysicsWithoutTouchingCarryableBattery()
    {
        var cargo = new GameObject("Secured authored cargo");
        cargo.transform.SetParent(game.van, false);
        var prop = new GameObject("Future authored cargo with physics");
        prop.transform.SetParent(cargo.transform, false);
        Rigidbody body = prop.AddComponent<Rigidbody>();
        Vector3 position = prop.transform.localPosition = new Vector3(0.4f, 0.6f, -1f);
        PrototypeGeometry.SecureAuthoredCargo(game);
        Assert.That(body.isKinematic, Is.True);
        Assert.That(body.useGravity, Is.False);
        Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints.FreezeAll));
        Assert.That(prop.transform.localPosition, Is.EqualTo(position));
        Assert.That(game.supplyPack.GetComponent<Rigidbody>().constraints, Is.EqualTo(RigidbodyConstraints.None),
            "Battery pickup/drop physics remain independent from fixed decorations.");
    }

    [Test]
    public void EscapePushesRammingEnemiesBackThenRestoresChaseWithoutLosingSpacing()
    {
        PursuerVan leader = game.SpawnPursuer(-1, -20f);
        leader.Step(10f);
        leader.Step(3f);
        Assert.That(leader.State, Is.EqualTo(PursuerVan.ChaseState.Ramming));
        PursuerVan follower = game.SpawnPursuer(-1, -30f);
        ReplacePedalRock();
        float initial = leader.transform.localPosition.z;
        leader.Step(1f);
        follower.Step(1f);
        Assert.That(leader.transform.localPosition.z, Is.LessThan(initial));
        Assert.That(leader.State, Is.EqualTo(PursuerVan.ChaseState.Approaching));
        Assert.That(game.Health, Is.EqualTo(5), "The escape must interrupt a pending ram.");
        game.accelerator.Step(7.99f);
        Assert.That(game.accelerator.Escaping, Is.True);
        initial = leader.transform.localPosition.z;
        leader.Step(0.1f);
        follower.Step(0.1f);
        Assert.That(leader.transform.localPosition.z, Is.LessThan(initial));
        Assert.That(leader.transform.localPosition.z - follower.transform.localPosition.z,
            Is.GreaterThanOrEqualTo(PursuerVan.FollowingDistance));
        game.accelerator.Step(0.02f);
        Assert.That(game.accelerator.Escaping, Is.False);
        initial = leader.transform.localPosition.z;
        leader.Step(0.1f);
        Assert.That(leader.transform.localPosition.z, Is.GreaterThan(initial));
        PursuerVan newcomer = game.SpawnPursuer(1, -100f);
        ReplacePedalRock();
        newcomer.Step(0.1f);
        Assert.That(newcomer.transform.localPosition.z, Is.LessThan(-100f),
            "New spawns must also respect an active escape.");
    }

    [Test]
    public void EscapeFreezesOnPauseTransfersOnlineAndClearsOnDislodgeAndReplay()
    {
        ReplacePedalRock();
        game.accelerator.Step(0.5f);
        float speed = game.CurrentSpeed;
        game.SetPaused(true);
        game.accelerator.Step(100f);
        Assert.That(game.accelerator.EscapeRemaining, Is.EqualTo(7.5f));
        Assert.That(game.CurrentSpeed, Is.EqualTo(speed));
        game.SetPaused(false);
        ConvoyAcceleratorState snapshot = game.accelerator.CaptureOnline();
        float remaining = game.accelerator.EscapeRemaining;
        game.accelerator.ResetForRound();
        game.network = root.AddComponent<ConvoyMultiplayer>();
        game.accelerator.ApplyOnline(snapshot);
        game.accelerator.ApplyEscapeOnline(remaining);
        game.accelerator.Step(0.5f);
        Assert.That(game.CurrentSpeed, Is.EqualTo(game.CruiseSpeed * ConvoyAccelerator.EscapeSpeed).Within(0.001f));
        for (int i = 0; i < 3; i++) game.accelerator.RegisterImpact();
        Assert.That(game.accelerator.EscapeRemaining, Is.Zero);
        game.accelerator.ResetForRound();
        Assert.That(game.accelerator.EscapeRemaining, Is.Zero);
        Assert.That(game.CurrentSpeed, Is.EqualTo(game.CruiseSpeed));
        ReplacePedalRock();
        game.accelerator.Step(20f);
        Assert.That(game.accelerator.EscapeRemaining, Is.Zero);
        Assert.That(game.CurrentSpeed, Is.EqualTo(game.CruiseSpeed), "A long frame must cross boost and settle boundaries.");
    }

    private void ReplacePedalRock()
    {
        for (int i = 0; i < 3; i++) game.accelerator.RegisterImpact();
        game.accelerator.Step(15f);
        game.player.SetSpawn(new Vector3(-0.9f, 0.214f, 2.95f), Quaternion.identity);
        Assert.That(game.player.TryInteract(), Is.True);
        game.player.SetSpawn(new Vector3(-0.9f, 0.214f, 3.1f), Quaternion.identity);
        Assert.That(game.player.TryInteract(), Is.True);
    }

    [Test]
    public void NightAssetsUsePaletteFogAndNonCollidingHeadlampsWithOnlyLaneLeadersLit()
    {
        Material sky = Resources.Load<Material>("ConvoyNightSky");
        Assert.That(sky, Is.Not.Null);
        Assert.That(UnityEditor.ShaderUtil.ShaderHasError(sky.shader), Is.False);
        Assert.That(ConvoyNight.FogColor, Is.EqualTo((Color)new Color32(69, 41, 63, 255)));
        Assert.That(ConvoyNight.FogStart, Is.GreaterThan(50f), "Nearby enemies and hazards must remain clear.");
        Material enemyMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Game Assets/in_van_assets/textures/vann.mat");
        Assert.That(enemyMaterial.GetTexture("_BaseMap"), Is.SameAs(
            UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Game Assets/in_van_assets/textures/vanchaser_v001.png")));
        PursuerVan leader = game.SpawnPursuer(-1, -10f);
        PursuerVan follower = game.SpawnPursuer(-1, -30f);
        PursuerVan distant = game.SpawnPursuer(1, -65f);
        leader.Step(0f);
        follower.Step(0f);
        distant.Step(0f);
        Assert.That(leader.headlights.enabled, Is.True);
        Assert.That(follower.headlights.enabled, Is.False);
        Assert.That(distant.headlights.enabled, Is.False);
        Assert.That(leader.headlights.shadows, Is.EqualTo(LightShadows.None));
        foreach (Transform child in leader.visualRoot)
            if (child.name == "Enemy headlamp")
            {
                Assert.That(child.GetComponent<Collider>(), Is.Null);
                Assert.That(child.GetComponent<Renderer>().sharedMaterial, Is.SameAs(Resources.Load<Material>("ConvoyLamp")));
            }
    }

    [Test]
    public void SoundBankReferencesExactlyTheSixRequestedImportedClips()
    {
        var bank = Resources.Load<ConvoySoundBank>("ConvoySoundBank");
        Assert.That(bank, Is.Not.Null);
        bank.AssignTo(game);
        AudioClip[] clips = { game.engineSound, game.windSound, game.fireSound,
            game.impactSound, game.pickupSound, game.loadSound };
        string[] names = { "van_running.wav", "background_wind.mp3", "crossbow_fire.mp3",
            "van_hit.mp3", "pickup_soundeffect.wav", "insert_amo.mp3" };
        for (int i = 0; i < clips.Length; i++)
        {
            Assert.That(clips[i], Is.Not.Null);
            Assert.That(UnityEditor.AssetDatabase.GetAssetPath(clips[i]),
                Is.EqualTo("Assets/Game Assets/sound_effects/" + names[i]));
            Assert.That(clips[i].length, Is.GreaterThan(0f));
            var importer = (UnityEditor.AudioImporter)UnityEditor.AssetImporter.GetAtPath(
                UnityEditor.AssetDatabase.GetAssetPath(clips[i]));
            Assert.That(importer.defaultSampleSettings.preloadAudioData, Is.True);
        }
        Assert.Throws<System.ArgumentOutOfRangeException>(() => game.PlaySound((ConvoyPrototype.Sound)100));
    }

    [TestCase(0f, false, 30f)]
    [TestCase(29.99f, false, 0.01f)]
    [TestCase(30f, true, 15f)]
    [TestCase(44.99f, true, 0.01f)]
    [TestCase(45f, false, 30f)]
    [TestCase(75f, true, 15f)]
    [TestCase(90f, false, 30f)]
    public void RoadCycleSwitchesExactlyAtThirtyAndFortyFiveSeconds(float time, bool highway, float remaining)
    {
        Assert.That(ConvoyRoadCycle.HighwayAt(time), Is.EqualTo(highway));
        Assert.That(ConvoyRoadCycle.RemainingAt(time), Is.EqualTo(remaining).Within(0.001f));
    }

    [Test]
    public void HighwayClearsApproachingHazardsAndResumesDirtWithoutBurstWhileDifficultyContinues()
    {
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        game.EnsureRoadAndRepairs();
        Assert.That(game.roadRocks.Rocks.Count, Is.EqualTo(1));
        game.AdvanceDifficulty(30f);
        game.ScrollRoad(0f);
        Assert.That(game.IsHighway, Is.True);
        Assert.That(game.roadRocks.Rocks.Count, Is.Zero);
        Assert.That(game.DifficultyStage, Is.EqualTo(1));
        Transform tile = game.road.GetChild(9);
        Assert.That(tile.Find("Highway surface").gameObject.activeSelf, Is.True);
        Assert.That(tile.Find("Dirt lane 1").gameObject.activeSelf, Is.True,
            "Dirt remains underneath the clipped asphalt rather than switching the whole world.");
        for (int i = 0; i < 150; i++) game.roadRocks.Step(0.1f);
        Assert.That(game.roadRocks.Rocks.Count, Is.Zero);
        game.AdvanceDifficulty(15f);
        game.ScrollRoad(0f);
        Assert.That(game.IsHighway, Is.False);
        Assert.That(game.RoadCycle.HighwayAtRoadPosition(20f), Is.False);
        Assert.That(tile.Find("Dirt lane 1").gameObject.activeSelf, Is.True);
        game.roadRocks.Step(0.1f);
        Assert.That(game.roadRocks.Rocks.Count, Is.Zero, "Highway must not bank a spawn burst.");
        game.roadRocks.Step(game.roadRocks.CurrentSpawnSeconds);
        Assert.That(game.roadRocks.Rocks.Count, Is.EqualTo(1));
    }

    [Test]
    public void BothSurfaceBoundariesApproachAndPassTheVanWithoutChangingTheWholeRoad()
    {
        game.EnsureRoadAndRepairs();
        ApplyRoadTime(24f);
        Assert.That(game.RoadCycle.HighwayAtRoadPosition(0f), Is.False);
        Assert.That(game.RoadCycle.HighwayAtRoadPosition(100f), Is.False);
        Assert.That(game.RoadCycle.HighwayAtRoadPosition(150f), Is.True, "Highway appears ahead before its timer begins.");
        ApplyRoadTime(29.8f);
        Assert.That(game.RoadCycle.HighwayAtRoadPosition(0f), Is.False);
        Assert.That(game.RoadCycle.HighwayAtRoadPosition(6f), Is.True);
        Transform tile = game.road.GetChild(9).Find("Highway surface");
        Mesh mesh = tile.GetComponent<MeshFilter>().sharedMesh;
        Assert.That(mesh.bounds.min.z, Is.EqualTo(4.6f).Within(0.01f), "Boundary clips through the current tile.");
        ApplyRoadTime(30.2f);
        Assert.That(game.RoadCycle.HighwayAtRoadPosition(0f), Is.True);
        Assert.That(game.RoadCycle.HighwayAtRoadPosition(-10f), Is.False, "Passed dirt stays behind.");
        ApplyRoadTime(44.8f);
        Assert.That(game.RoadCycle.HighwayAtRoadPosition(0f), Is.True);
        Assert.That(game.RoadCycle.HighwayAtRoadPosition(8f), Is.False, "Dirt appears ahead before highway ends.");
        ApplyRoadTime(45.2f);
        Assert.That(game.RoadCycle.HighwayAtRoadPosition(0f), Is.False);
        Assert.That(game.RoadCycle.HighwayAtRoadPosition(-10f), Is.True, "Passed highway stays behind.");
        Assert.That(game.RoadCycle.BoundarySequence, Is.EqualTo(2));
        Assert.That(game.RoadCycle.History.x, Is.EqualTo(1069.5f).Within(0.1f));
        Assert.That(game.RoadCycle.History.y, Is.EqualTo(690f).Within(0.1f));
        Assert.That(game.road.GetChild(9).Find("Roadside nature sprites"), Is.Not.Null);
    }

    private void ApplyRoadTime(float time)
    {
        ConvoyWorldState state = game.CaptureWorld();
        state.SurvivalTime = time;
        state.Distance = ConvoyRoadCycle.CruiseDistanceAt(time, game.roadSpeed);
        game.ApplyWorld(state);
        foreach (Transform tile in game.road)
            tile.localPosition = new Vector3(0f, 0f, ConvoyPrototype.RoadRear + tile.GetSiblingIndex() * ConvoyPrototype.SegmentLength);
        game.RoadCycle.Apply();
    }

    [Test]
    public void FiresUseActualCargoAndHoodSurfacesAndRepairOnlyOnceAfterARealJumpRise()
    {
        game.EnsureRoadAndRepairs();
        game.RegisterRam(1);
        game.RegisterRam(-1);
        Assert.That(game.Health, Is.EqualTo(3));
        Assert.That(game.RepairFires.Count, Is.EqualTo(2));
        Transform cargo = game.RepairFires.Spot(0), hood = game.RepairFires.Spot(1);
        Assert.That(cargo.localPosition.z, Is.LessThan(0f));
        Assert.That(hood.localPosition.z, Is.GreaterThan(4f));
        Assert.That(hood.localPosition.y, Is.GreaterThan(cargo.localPosition.y));
        Vector3 feet = cargo.localPosition - Vector3.up * 0.012f;
        game.player.SetSpawn(feet, Quaternion.identity);
        Assert.That(game.RepairFires.TryStomp(game.player), Is.False, "Walking or teleporting onto fire cannot repair.");
        Assert.That(game.RepairFires.BeginJump(game.player), Is.True);
        game.RepairFires.ObserveJump(game.player, feet + Vector3.up * 0.05f);
        Assert.That(game.RepairFires.TryStomp(game.player), Is.False, "Small step-up is not a jump.");
        Assert.That(game.RepairFires.BeginJump(game.player), Is.True);
        game.RepairFires.ObserveJump(game.player, feet + Vector3.up * 0.3f);
        Assert.That(game.RepairFires.TryStomp(game.player), Is.True);
        Assert.That(game.Health, Is.EqualTo(4));
        Assert.That(game.RepairFires.Count, Is.EqualTo(1));
        Assert.That(game.RepairFires.TryStomp(game.player), Is.False);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.FireStomp), Is.True);
        Assert.That(cargo.gameObject.activeSelf, Is.False);
        for (int i = 0; i < 4; i++) game.RegisterRam(1);
        Assert.That(game.IsDead, Is.True);
        Assert.That(game.RepairFires.Count, Is.EqualTo(4), "Fatal damage must not add a fifth repair.");
        Assert.That(game.RepairFires.BeginJump(game.player), Is.False);
        Assert.That(game.RestoreHealth(), Is.False, "Fire cannot resurrect the van.");
        game.ResetRound(ConvoyPrototype.PlayerRole.Gunner, feet, Quaternion.identity);
        Assert.That(game.RepairFires.Mask, Is.Zero);
        Assert.That(game.Health, Is.EqualTo(5));
        Assert.That(game.IsHighway, Is.False);
    }

    [Test]
    public void ReplayResetClearsDamageSuppliesTimersMotionRespawnAndTutorialWithoutRebuildingScenery()
    {
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        Transform roadTile = game.road.GetChild(9);
        game.player.MountDriver();
        game.Steer(1f, 1f);
        game.AdvanceDifficulty(120f);
        game.ScrollRoad(1f);
        game.Tutorial.Complete(ConvoyTutorial.Lesson.Jump);
        game.player.DismountDriver();
        game.player.SetSpawn(new Vector3(0.6f, 0.82f, 2.5f), Quaternion.identity);
        Assert.That(game.player.TryInteract(), Is.True);
        for (int i = 0; i < 5; i++) game.RegisterRam(1);
        game.ResetRound(ConvoyPrototype.PlayerRole.Gunner, new Vector3(0.9f, 0.82f, -1.35f), Quaternion.Euler(0, 180, 0));
        Assert.That(game.Health, Is.EqualTo(5));
        Assert.That(game.ShotsAvailable, Is.EqualTo(4));
        Assert.That(game.SurvivalTime, Is.Zero);
        Assert.That(game.SteeringOffset, Is.Zero);
        Assert.That(game.Cooldown, Is.Zero);
        Assert.That(game.Pursuers.Count, Is.Zero);
        Assert.That(game.roadRocks.Rocks.Count, Is.Zero);
        Assert.That(game.accelerator.Impacts, Is.Zero);
        Assert.That(game.accelerator.RockOnPedal, Is.True);
        Assert.That(game.player.CarryingRock, Is.False);
        Assert.That(game.player.CarriedAmmo, Is.Null);
        Assert.That(game.player.Mounted, Is.False);
        Assert.That(game.player.Driving, Is.False);
        Assert.That(game.Tutorial.Learned, Is.EqualTo(ConvoyTutorial.Lesson.None));
        Assert.That(game.road.GetChild(9), Is.SameAs(roadTile), "Replay must not rebuild the optimized field scenery.");
    }

    [TestCase("Survived 01:23", "SURVIVED 01:23")]
    [TestCase("Partner's role", "PARTNER'S ROLE")]
    [TestCase("Connection\nclosed", "CONNECTION\nCLOSED")]
    public void PixelUIRetainsReadableAsciiLabels(string original, string expected)
    {
        Assert.That(ConvoyPixelUI.Normalize(original), Is.EqualTo(expected));
    }

    [Test]
    public void TutorialBatteryRouteUsesRealReachAndRetiresOnlySuccessfulSteps()
    {
        game.player.SetSpawn(new Vector3(0.6f, 0.82f, 2.5f), Quaternion.identity);
        Assert.That(game.Tutorial.FindHint().Lesson, Is.EqualTo(ConvoyTutorial.Lesson.BatteryPickup));
        Assert.That(game.Tutorial.FindHint().Target, Is.SameAs(game.supplyPack.transform));
        Assert.That(game.player.TryInteract(), Is.True);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.BatteryPickup), Is.True);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.BatteryLoad), Is.False);
        Assert.That(game.Tutorial.FindHint().Key, Is.EqualTo("WASD"));
        Assert.That(game.Tutorial.FindHint().Target, Is.SameAs(game.gun));
        Assert.That(game.Tutorial.FindHint().TargetPosition,
            Is.EqualTo(game.gun.TransformPoint(ConvoyPrototype.GunLoadingPoint)));
        Assert.That(game.player.TryInteract(), Is.False, "Picking up is not the same as loading.");
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.BatteryLoad), Is.False);
        game.player.SetSpawn(new Vector3(0.8f, 0.35f, -1.35f), Quaternion.identity);
        Assert.That(game.Tutorial.FindHint().Key, Is.EqualTo("E"));
        Assert.That(game.player.TryInteract(), Is.True);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.BatteryLoad), Is.True);
        game.player.SetSpawn(new Vector3(0.6f, 0.82f, 2.5f), Quaternion.identity);
        Assert.That(game.Tutorial.FindHint().Lesson, Is.Not.EqualTo(ConvoyTutorial.Lesson.BatteryPickup));
        Assert.That(game.player.TryInteract(), Is.True);
        Assert.That(game.Tutorial.FindHint().Lesson, Is.EqualTo(ConvoyTutorial.Lesson.None));
    }

    [Test]
    public void TutorialObstructedBatteryHasNoPickupHintAndFailedActionsDoNotTeach()
    {
        game.player.SetSpawn(new Vector3(0.6f, 0.82f, 2.5f), Quaternion.identity);
        Vector3 midpoint = (game.player.transform.TransformPoint(Vector3.up * ConvoyPlayer.EyeHeight) +
            game.supplyPack.transform.position) * 0.5f;
        PrototypeGeometry.Part("Tutorial obstruction", game.van, game.van.InverseTransformPoint(midpoint),
            Vector3.one * 0.08f, material);
        Assert.That(game.player.FindReachableAmmo(), Is.Null);
        Assert.That(game.Tutorial.FindHint().Lesson, Is.Not.EqualTo(ConvoyTutorial.Lesson.BatteryPickup));
        Assert.That(game.player.TryInteract(), Is.False);
        Assert.That(game.TryFire(), Is.False);
        Assert.That(game.Tutorial.Learned, Is.EqualTo(ConvoyTutorial.Lesson.None));
    }

    [Test]
    public void TutorialDrivingNeedsSuccessfulMountAndActualSteeringAndKeepsLateUnlearnedRepairs()
    {
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        Assert.That(game.Tutorial.FindHint().Lesson, Is.EqualTo(ConvoyTutorial.Lesson.DriverMount));
        Assert.That(game.Tutorial.FindHint().TargetPosition,
            Is.EqualTo(game.van.TransformPoint(ConvoyPrototype.DriverBoardingPoint)));
        Assert.That(Vector3.Distance(game.Tutorial.FindHint().TargetPosition,
            game.van.TransformPoint(ConvoyPrototype.SteeringWheelPosition)), Is.GreaterThan(1f));
        Assert.That(game.player.MountDriver(), Is.True);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.DriverMount), Is.True);
        Assert.That(game.Tutorial.FindHint().Lesson, Is.EqualTo(ConvoyTutorial.Lesson.Steer));
        game.Steer(0f, 1f);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.Steer), Is.False);
        for (int frame = 0; frame < 100; frame++)
            game.Steer(1f, 0.0002f);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.Steer), Is.False);
        game.Steer(1f, 0.01f);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.Steer), Is.True);
        Assert.That(game.Tutorial.FindHint().Lesson, Is.EqualTo(ConvoyTutorial.Lesson.None));
        game.player.DismountDriver();
        for (int i = 0; i < 3; i++) game.accelerator.RegisterImpact();
        game.AdvanceDifficulty(120f);
        game.accelerator.Step(15f);
        game.player.SetSpawn(new Vector3(-0.9f, 0.214f, 2.95f), Quaternion.identity);
        Assert.That(game.Tutorial.FindHint().Lesson, Is.EqualTo(ConvoyTutorial.Lesson.RockPickup));
        Assert.That(game.player.TryInteract(), Is.True);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.RockPickup), Is.True);
        game.player.SetSpawn(new Vector3(-0.9f, 0.214f, 3.1f), Quaternion.identity);
        Assert.That(game.Tutorial.FindHint().Lesson, Is.EqualTo(ConvoyTutorial.Lesson.RockPlace));
        Assert.That(game.Tutorial.FindHint().Key, Is.EqualTo("E"));
        Assert.That(game.player.TryInteract(), Is.True);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.RockPlace), Is.True);
        for (int i = 0; i < 3; i++) game.accelerator.RegisterImpact();
        game.accelerator.Step(1f);
        Assert.That(game.accelerator.NeedsRock, Is.True);
        Assert.That(game.Tutorial.FindHint().Lesson, Is.Not.EqualTo(ConvoyTutorial.Lesson.RockPickup));
    }

    [Test]
    public void TutorialGunUseRetiresMountAndFireSeparately()
    {
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Gunner);
        game.player.SetSpawn(new Vector3(0.9f, 0.82f, -1.35f), Quaternion.identity);
        Assert.That(game.Tutorial.FindHint().Lesson, Is.EqualTo(ConvoyTutorial.Lesson.GunMount));
        Assert.That(game.player.Mount(), Is.True);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.GunMount), Is.True);
        Assert.That(game.Tutorial.FindHint().Lesson, Is.EqualTo(ConvoyTutorial.Lesson.GunFire));
        Assert.That(game.TryFire(), Is.True);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.GunFire), Is.True);
        Assert.That(game.Tutorial.FindHint().Lesson, Is.EqualTo(ConvoyTutorial.Lesson.None));
        game.player.Dismount();
        Assert.That(game.Tutorial.FindHint().Lesson, Is.Not.EqualTo(ConvoyTutorial.Lesson.GunMount));
    }

    [Test]
    public void FirstEmptyGunGuidesDismountPickupAndActualGunLoadingAfterFireLessonRetires()
    {
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Gunner);
        game.player.SetSpawn(new Vector3(0.9f, 0.82f, -1.35f), Quaternion.identity);
        Assert.That(game.player.Mount(), Is.True);
        for (int i = 0; i < 4; i++)
        {
            var state = game.CaptureWorld();
            state.Cooldown = 0f;
            game.ApplyWorld(state);
            Assert.That(game.TryFire(), Is.True);
        }
        Assert.That(game.ShotsAvailable, Is.Zero);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.GunFire), Is.True);
        var hint = game.Tutorial.FindHint();
        Assert.That(hint.Lesson, Is.EqualTo(ConvoyTutorial.Lesson.BatteryPickup));
        Assert.That(hint.Key, Is.EqualTo("RMB"));
        Assert.That(hint.Target, Is.SameAs(game.supplyPack.transform));
        game.SetPaused(true);
        game.SetPaused(false);
        Assert.That(game.Tutorial.FindHint().Key, Is.EqualTo("WASD"));
        game.player.SetSpawn(new Vector3(0.6f, 0.82f, 2.5f), Quaternion.identity);
        Assert.That(game.Tutorial.FindHint().Key, Is.EqualTo("E"));
        Assert.That(game.player.TryInteract(), Is.True);
        hint = game.Tutorial.FindHint();
        Assert.That(hint.Lesson, Is.EqualTo(ConvoyTutorial.Lesson.BatteryLoad));
        Assert.That(hint.Target, Is.SameAs(game.gun));
        Assert.That(hint.TargetPosition, Is.EqualTo(game.gun.TransformPoint(ConvoyPrototype.GunLoadingPoint)));
        Assert.That(hint.Key, Is.EqualTo("WASD"));
        Assert.That(game.player.TryInteract(), Is.False);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.BatteryLoad), Is.False);
        game.player.SetSpawn(new Vector3(0.8f, 0.35f, -1.35f), Quaternion.identity);
        Assert.That(game.Tutorial.FindHint().Key, Is.EqualTo("E"));
        Assert.That(game.player.TryInteract(), Is.True);
        Assert.That(game.ShotsAvailable, Is.EqualTo(4));
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.BatteryLoad), Is.True);
        game.ApplyAmmoCount(0);
        Assert.That(game.Tutorial.FindHint().Lesson, Is.Not.EqualTo(ConvoyTutorial.Lesson.BatteryPickup));
    }

    [Test]
    public void SteeringCrossesALaneInFourTenthsAndReversesImmediatelyWithoutOvershootingRoad()
    {
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        Assert.That(game.player.MountDriver(), Is.True);
        game.Steer(1f, 0.4f);
        Assert.That(game.SteeringOffset, Is.EqualTo(4.8f).Within(0.001f));
        game.Steer(-1f, 0.1f);
        Assert.That(game.SteeringOffset, Is.EqualTo(3.6f).Within(0.001f));
        game.Steer(-1f, 1f);
        Assert.That(game.SteeringOffset, Is.EqualTo(-ConvoyPrototype.RoadEdge));
    }

    [Test]
    public void RoadPressureDoublesEarlyAndReachesDenseBoundedTrafficByNinetySeconds()
    {
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        Assert.That(game.roadRocks.CurrentSpawnSeconds, Is.EqualTo(7f));
        game.AdvanceDifficulty(14.99f);
        Assert.That(game.roadRocks.PressureStage, Is.Zero);
        game.AdvanceDifficulty(0.01f);
        Assert.That(game.DifficultyStage, Is.Zero, "Road pressure rises independently of the 30-second cruise stage.");
        Assert.That(game.roadRocks.CurrentSpawnSeconds, Is.LessThan(3.12f));
        game.AdvanceDifficulty(15f);
        Assert.That(game.roadRocks.CurrentSpawnSeconds, Is.EqualTo(2f));
        game.AdvanceDifficulty(60f);
        Assert.That(game.roadRocks.CurrentSpawnSeconds, Is.EqualTo(0.85f));
        game.van.localPosition += Vector3.right * 20f;
        for (int i = 0; i < 100; i++)
        {
            game.roadRocks.Step(0.1f);
            Assert.That(game.roadRocks.Rocks.Count, Is.LessThanOrEqualTo(ConvoyRoadRocks.HazardLimit));
        }
        Assert.That(game.roadRocks.Rocks.Count, Is.GreaterThanOrEqualTo(6));
        Assert.That(game.RamCount, Is.Zero);
    }

    [Test]
    public void TutorialLearningSurvivesPauseButResetsForNewGameAndRequiresMeasuredMovement()
    {
        game.Tutorial.ObserveWalk(0.69f);
        game.Tutorial.ObserveLook(11f);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.Walk), Is.False);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.Look), Is.False);
        game.Tutorial.ObserveWalk(0.02f);
        game.Tutorial.ObserveLook(1f);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.Walk | ConvoyTutorial.Lesson.Look), Is.True);
        game.SetPaused(true);
        Assert.That(game.Tutorial.Visible, Is.False);
        Assert.That(game.Tutorial.FindHint().Lesson, Is.EqualTo(ConvoyTutorial.Lesson.None));
        game.SetPaused(false);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.Walk), Is.True);
        ConvoyPlayer remote = Object.Instantiate(game.player.gameObject, game.van).GetComponent<ConvoyPlayer>();
        remote.IsLocal = false;
        game.RecordLesson(remote, ConvoyTutorial.Lesson.BatteryLoad);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.BatteryLoad), Is.False);
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        Assert.That(game.Tutorial.Learned, Is.EqualTo(ConvoyTutorial.Lesson.None));
        for (int i = 0; i < 5; i++) game.RegisterRam(1);
        Assert.That(game.Tutorial.Visible, Is.False);
    }

    [Test]
    public void DifficultyChangesExactlyEveryThirtySecondsAndKeepsRisingWithObjectLimits()
    {
        Assert.That(ConvoyPrototype.DifficultyInterval, Is.EqualTo(30f));
        game.AdvanceDifficulty(ConvoyPrototype.DifficultyInterval - 1f);
        game.AdvanceDifficulty(0.99f);
        Assert.That(game.DifficultyStage, Is.Zero);
        Assert.That(game.CurrentSpeed, Is.EqualTo(23f));
        Assert.That(game.CurrentPursuerLimit, Is.EqualTo(2));
        Assert.That(game.CurrentSpawnInterval, Is.EqualTo(16f));
        game.AdvanceDifficulty(0.01f);
        Assert.That(game.DifficultyStage, Is.EqualTo(1));
        Assert.That(game.CruiseSpeed, Is.EqualTo(25.3f).Within(0.001f));
        Assert.That(game.CurrentPursuerLimit, Is.EqualTo(3));
        Assert.That(game.CurrentSpawnInterval, Is.EqualTo(12.8f).Within(0.001f));
        float speed = game.CruiseSpeed;
        for (int i = 0; i < 100; i++)
        {
            game.AdvanceDifficulty(ConvoyPrototype.DifficultyInterval);
            Assert.That(game.CruiseSpeed, Is.GreaterThan(speed));
            Assert.That(game.CurrentPursuerLimit, Is.LessThanOrEqualTo(8));
            Assert.That(game.CurrentSpawnInterval, Is.GreaterThanOrEqualTo(2.5f));
            Assert.That(game.roadRocks.CurrentSpacing / game.CruiseSpeed, Is.GreaterThanOrEqualTo(0.849f));
            Assert.That(game.roadRocks.CurrentSpawnDistance / game.CruiseSpeed, Is.GreaterThanOrEqualTo(6.499f));
            speed = game.CruiseSpeed;
        }
        Assert.Throws<System.ArgumentOutOfRangeException>(() => game.AdvanceDifficulty(float.NaN));
    }

    [Test]
    public void DifficultyFreezesForLocalPauseSelectionAndDeathAndRepairsRestoreNewCruiseSpeed()
    {
        game.showRoleSelection = true;
        game.AdvanceDifficulty(ConvoyPrototype.DifficultyInterval);
        Assert.That(game.SurvivalTime, Is.Zero);
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        game.SetPaused(true);
        game.AdvanceDifficulty(ConvoyPrototype.DifficultyInterval);
        Assert.That(game.SurvivalTime, Is.Zero);
        game.SetPaused(false);
        game.AdvanceDifficulty(ConvoyPrototype.DifficultyInterval);
        for (int i = 0; i < 3; i++) game.accelerator.RegisterImpact();
        game.accelerator.Step(15f);
        Assert.That(game.CurrentSpeed, Is.Zero);
        game.AdvanceDifficulty(ConvoyPrototype.DifficultyInterval);
        Assert.That(game.DifficultyStage, Is.EqualTo(2), "A broken accelerator must not suspend survival difficulty.");
        game.player.SetSpawn(new Vector3(-0.9f, 0.214f, 2.95f), Quaternion.identity);
        Assert.That(game.player.TryInteract(), Is.True);
        game.player.SetSpawn(new Vector3(-0.9f, 0.214f, 3.1f), Quaternion.identity);
        Assert.That(game.player.TryInteract(), Is.True);
        game.accelerator.Step(5f);
        Assert.That(game.CurrentSpeed, Is.EqualTo(game.CruiseSpeed * ConvoyAccelerator.EscapeSpeed).Within(0.001f));
        game.accelerator.Step(4f);
        Assert.That(game.CurrentSpeed, Is.EqualTo(game.roadSpeed * 1.2f).Within(0.001f));
        for (int i = 0; i < 5; i++) game.RegisterRam(1);
        float elapsed = game.SurvivalTime;
        game.AdvanceDifficulty(ConvoyPrototype.DifficultyInterval);
        Assert.That(game.SurvivalTime, Is.EqualTo(elapsed));
        Assert.That(game.CurrentSpeed, Is.Zero);
    }

    [Test]
    public void LaterRoadHazardsMixGroundedBushesAndRocksAndBushHitsDamageOnce()
    {
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        game.AdvanceDifficulty(ConvoyRoadCycle.CycleDuration);
        Assert.That(game.IsHighway, Is.False);
        Assert.That(game.roadRocks.CurrentSpawnSeconds, Is.EqualTo(7f / 4.75f).Within(0.001f));
        for (int index = 0; index < 2; index++)
        {
            game.roadRocks.ApplyProgress(new Vector3(game.roadRocks.CurrentSpacing - 1f, index, 1f));
            game.roadRocks.Step(2f / game.CurrentSpeed);
            Transform hazard = game.roadRocks.Rocks[game.roadRocks.Rocks.Count - 1];
            Assert.That(game.roadRocks.IsBush(hazard), Is.EqualTo(index == 1));
            Assert.That(hazard.localPosition.y, Is.EqualTo(-0.02f));
            Assert.That(hazard.localPosition.x, Is.EqualTo((index - 1) * 4.8f));
            Assert.That(hazard.localPosition.z / game.CruiseSpeed, Is.GreaterThan(5f));
        }
        Transform bush = game.roadRocks.Rocks[game.roadRocks.Rocks.Count - 1];
        Assert.That(bush.GetComponent<SpriteRenderer>().sprite.bounds.size.y * bush.localScale.y,
            Is.EqualTo(ConvoyRoadRocks.BushHeight).Within(0.001f));
        bush.localPosition = new Vector3(0f, -0.02f, 10f);
        game.roadRocks.Step(0.6f);
        Assert.That(game.RamCount, Is.EqualTo(1));
        Assert.That(game.accelerator.Impacts, Is.EqualTo(1));
        Assert.That(game.roadRocks.Rocks.Contains(bush), Is.False);
        game.roadRocks.Step(0.1f);
        Assert.That(game.RamCount, Is.EqualTo(1));
    }

    [Test]
    public void FasterRoadStillAllowsChasersToCatchUpAndHitchesDoNotSpawnHazardBursts()
    {
        PursuerVan enemy = game.SpawnPursuer(-1, -65f);
        float original = enemy.transform.localPosition.z;
        enemy.Step(1f);
        float baseClosing = enemy.transform.localPosition.z - original;
        game.AdvanceDifficulty(ConvoyPrototype.DifficultyInterval);
        original = enemy.transform.localPosition.z;
        enemy.Step(1f);
        Assert.That(enemy.transform.localPosition.z - original,
            Is.EqualTo(baseClosing * game.SpeedMultiplier * game.ChasePressure).Within(0.001f));
        game.Pursuers.Remove(enemy);
        Object.DestroyImmediate(enemy.gameObject);
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        game.AdvanceDifficulty(ConvoyRoadCycle.HighwayDuration);
        game.van.localPosition += Vector3.right * 4.95f;
        game.roadRocks.Step(10000f);
        Assert.That(game.roadRocks.Rocks.Count, Is.Zero, "Do not replay hours of missed spawns after a hitch.");
        for (int i = 0; i < ConvoyRoadRocks.HazardLimit; i++)
            game.roadRocks.SpawnRock(1, 500f);
        game.roadRocks.ApplyProgress(new Vector3(game.roadRocks.CurrentSpacing, 0f, 1f));
        game.roadRocks.Step(0f);
        Assert.That(game.roadRocks.Rocks.Count, Is.EqualTo(ConvoyRoadRocks.HazardLimit));
    }

    [Test]
    public void DriverRoadRocksAreGroundedAndDamageOnceIncludingAccelerator()
    {
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        Assert.That(game.Pursuers.Count, Is.Zero);
        Assert.That(game.roadRocks.Rocks.Count, Is.EqualTo(1));
        Transform rock = game.roadRocks.Rocks[0];
        Assert.That(rock.localPosition, Is.EqualTo(new Vector3(0f, -0.02f, 140f)));
        SpriteRenderer renderer = rock.GetComponent<SpriteRenderer>();
        Assert.That(renderer.sprite, Is.SameAs(game.natureSprites[1]));
        Assert.That(renderer.sprite.bounds.size.y * rock.localScale.y, Is.EqualTo(2.2f).Within(0.001f));
        game.roadRocks.Step(3f);
        Assert.That(game.RamCount, Is.Zero, "No distant or early hits.");
        game.roadRocks.Step(4f);
        Assert.That(game.RamCount, Is.EqualTo(1), "Swept collision must catch crossing the whole van in one frame.");
        Assert.That(game.accelerator.Impacts, Is.EqualTo(1));
        Assert.That(game.DamageFlash, Is.EqualTo(1f));
        Assert.That(game.player.ImpactVelocity.sqrMagnitude, Is.GreaterThan(0f));
        Assert.That(game.roadRocks.Rocks.Count, Is.EqualTo(1), "The next lane's obstacle arrives after seven seconds.");
        game.roadRocks.Step(1f);
        Assert.That(game.RamCount, Is.EqualTo(1), "The same rock cannot deal repeated damage.");
        for (int i = 0; i < 2; i++)
        {
            game.roadRocks.SpawnRock(0, 10f);
            game.roadRocks.Step(1f);
        }
        Assert.That(game.Health, Is.EqualTo(2));
        Assert.That(game.accelerator.NeedsRock, Is.True, "Road hits must also dislodge the accelerator rock.");
    }

    [Test]
    public void RoadRocksRespectModePauseStoppedSpeedAndLeaveOtherLanesClear()
    {
        Assert.Throws<System.InvalidOperationException>(() => game.roadRocks.SpawnRock(0, 10f));
        game.roadRocks.BeginPractice();
        Assert.That(game.roadRocks.Rocks.Count, Is.Zero);
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        Assert.That(game.player.MountDriver(), Is.True);
        game.Steer(1f, 1.6f);
        game.van.localPosition = new Vector3(game.SteeringOffset, 1.05f, 0f);
        game.roadRocks.Step(7f);
        Assert.That(game.RamCount, Is.Zero, "Steering into an open lane must avoid the center rock.");
        game.roadRocks.Step(1f);
        Assert.That(game.roadRocks.Rocks.Count, Is.EqualTo(1));
        game.roadRocks.Step(5.9f);
        Assert.That(game.roadRocks.Rocks.Count, Is.EqualTo(1), "Next spawn waits for seven more seconds of travel.");
        game.roadRocks.Step(0.2f);
        Assert.That(game.roadRocks.Rocks.Count, Is.EqualTo(2));
        Transform rock = game.roadRocks.Rocks[0];
        Assert.That(rock.localPosition.x, Is.EqualTo(-4.8f));
        Vector3 position = rock.localPosition;
        game.SetPaused(true);
        game.roadRocks.Step(20f);
        Assert.That(rock.localPosition, Is.EqualTo(position));
        game.SetPaused(false);
        for (int i = 0; i < 3; i++) game.accelerator.RegisterImpact();
        game.accelerator.Step(15f);
        game.roadRocks.Step(20f);
        Assert.That(rock.localPosition, Is.EqualTo(position));
        Assert.That(game.roadRocks.Rocks.Count, Is.EqualTo(2), "Stopped travel must not keep spawning rocks.");
    }

    [Test]
    public void NatureSpritesReplaceOldScrubStayOutsideRoadAndScrollWithTiles()
    {
        foreach (Transform tile in game.road)
        {
            Assert.That(tile.Find("Roadside scrub"), Is.Null);
            Assert.That(tile.Find("Rock"), Is.Null);
            ConvoyNatureBatch[] batches = tile.GetComponentsInChildren<ConvoyNatureBatch>();
            Assert.That(batches.Length, Is.EqualTo(9));
            Assert.That(tile.GetComponentsInChildren<ConvoyNatureSprite>().Length, Is.Zero);
            Assert.That(tile.GetComponentsInChildren<SpriteRenderer>().Length, Is.Zero);
            int grassCount = 0, treeCount = 0, rockCount = 0;
            var used = new System.Collections.Generic.HashSet<Sprite>();
            foreach (ConvoyNatureBatch batch in batches)
            {
                Sprite sprite = batch.sprite;
                used.Add(sprite);
                Assert.That(batch.GetComponent<MeshRenderer>().sharedMaterial.mainTexture, Is.SameAs(sprite.texture));
                Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(batch.GetComponent<Collider>(), Is.Null);
                Mesh mesh = batch.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount, Is.EqualTo(batch.placements.Length * 4));
                Assert.That(mesh.triangles.Length, Is.EqualTo(batch.placements.Length * 6));
                foreach (ConvoyNatureBatch.Placement placement in batch.placements)
                {
                    float height = sprite.bounds.size.y * placement.scale;
                    if (sprite == game.natureSprites[0])
                    {
                        treeCount++;
                        Assert.That(height, Is.InRange(12f, 20f));
                    }
                    else if (sprite == game.natureSprites[1] || sprite == game.natureSprites[2] ||
                        sprite == game.natureSprites[3])
                    {
                        rockCount++;
                        Assert.That(height, Is.InRange(1f, 2.5f));
                    }
                    else if (sprite == game.natureSprites[4] || sprite == game.natureSprites[5] ||
                        sprite == game.natureSprites[6])
                        grassCount++;
                    float halfWidth = sprite.bounds.size.x * placement.scale * 0.5f;
                    Assert.That(Mathf.Abs(placement.position.x) - halfWidth, Is.GreaterThanOrEqualTo(8.49f));
                    Assert.That(placement.position.y, Is.EqualTo(-0.02f));
                    Assert.That(Mathf.Abs(placement.position.z), Is.LessThanOrEqualTo(12f));
                    Assert.That(Mathf.Abs(placement.position.x) + halfWidth, Is.LessThanOrEqualTo(54.01f));
                    Assert.That(mesh.bounds.Contains(placement.position + Vector3.up * height), Is.True);
                }
            }
            Assert.That(used.Count, Is.EqualTo(9));
            Assert.That(grassCount, Is.EqualTo(400));
            Assert.That(treeCount, Is.EqualTo(32));
            Assert.That(rockCount, Is.EqualTo(12));
        }
        Transform firstTile = game.road.GetChild(9);
        Transform tree = firstTile.Find("Roadside nature sprites").GetChild(0);
        Vector3 local = tree.localPosition;
        float worldZ = tree.position.z;
        game.ScrollRoad(0.25f);
        Assert.That(tree.position.z - worldZ, Is.EqualTo(-game.CurrentSpeed * 0.25f).Within(0.001f));
        Assert.That(tree.localPosition, Is.EqualTo(local));
        PrototypeGeometry.ReplaceRoadsideNature(game);
        Assert.That(game.road.GetComponentsInChildren<ConvoyNatureBatch>().Length,
            Is.EqualTo(ConvoyPrototype.SegmentCount * 9), "Applying scenery twice must not duplicate it.");
    }

    [Test]
    public void EveryNatureSpriteVisibleBottomIsAnchoredToGround()
    {
        foreach (Sprite sprite in game.natureSprites)
        {
            Texture2D image = new Texture2D(2, 2);
            try
            {
                Assert.That(image.LoadImage(System.IO.File.ReadAllBytes(UnityEditor.AssetDatabase.GetAssetPath(sprite))), Is.True);
                Color32[] pixels = image.GetPixels32();
                int bottom = image.height;
                for (int y = 0; y < image.height && bottom == image.height; y++)
                    for (int x = 0; x < image.width; x++)
                        if (pixels[y * image.width + x].a > 0)
                        {
                            bottom = y;
                            break;
                        }
                Assert.That(sprite.pivot.y, Is.EqualTo(bottom).Within(0.001f),
                    sprite.name + " must anchor visible pixels, not transparent image padding.");
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
        }
    }

    [Test]
    public void Player2PrefabUsesAllGleifFramesWithSameSpriteSettingsAndSize()
    {
        ConvoySpriteAvatar avatar = game.player2AvatarPrefab;
        Assert.That(avatar, Is.Not.Null);
        Sprite[] frames = { avatar.idleFront, avatar.idleBack, avatar.walk1Front, avatar.walk2Front,
            avatar.walk1Back, avatar.walk2Back, avatar.jumpFront, avatar.jumpBack };
        string[] names = { "Idle_Front", "Idle_Back", "Walk1_Front", "Walk2_Front",
            "Walk1_Back", "Walk2_Back", "Jump_Front", "Jump_Back" };
        for (int i = 0; i < frames.Length; i++)
        {
            string path = "Assets/main_characters/player2/Gleif_" + names[i] + ".png";
            Assert.That(frames[i], Is.SameAs(UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path)));
            Assert.That(frames[i].pixelsPerUnit, Is.EqualTo(160f));
            Assert.That(frames[i].pivot, Is.EqualTo(new Vector2(frames[i].rect.width * 0.5f, 0f)));
            Assert.That(frames[i].bounds.size.y, Is.EqualTo(ConvoyPlayer.BodyHeight).Within(0.001f));
            Assert.That(frames[i].texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(frames[i].texture.mipmapCount, Is.EqualTo(1));
        }
        Assert.That(avatar.spriteRenderer.sharedMaterial,
            Is.SameAs(game.playerAvatarPrefab.spriteRenderer.sharedMaterial));
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        Assert.That(game.player.spriteAvatar.idleBack, Is.SameAs(avatar.idleBack));
        Assert.That(game.player.GetComponentsInChildren<ConvoySpriteAvatar>(true).Length, Is.EqualTo(1));
        ConvoySpriteAvatar instance = game.player.spriteAvatar;
        instance.Animate(Vector2.up, true, 0f);
        Assert.That(instance.spriteRenderer.sprite, Is.SameAs(avatar.walk1Back));
        instance.Animate(Vector2.up, true, ConvoySpriteAvatar.WalkFrameDuration);
        Assert.That(instance.spriteRenderer.sprite, Is.SameAs(avatar.walk2Back));
        instance.Animate(Vector2.zero, true, 0f);
        Assert.That(instance.spriteRenderer.sprite, Is.SameAs(avatar.idleBack));
        instance.Animate(Vector2.up, false, 0f);
        Assert.That(instance.spriteRenderer.sprite, Is.SameAs(avatar.jumpBack));
        instance.Animate(Vector2.down, true, 0f);
        Assert.That(instance.spriteRenderer.sprite, Is.SameAs(avatar.walk1Front));
        instance.Animate(Vector2.down, true, ConvoySpriteAvatar.WalkFrameDuration);
        Assert.That(instance.spriteRenderer.sprite, Is.SameAs(avatar.walk2Front));
        instance.Animate(Vector2.zero, true, 0f);
        Assert.That(instance.spriteRenderer.sprite, Is.SameAs(avatar.idleFront));
        instance.Animate(Vector2.down, false, 0f);
        Assert.That(instance.spriteRenderer.sprite, Is.SameAs(avatar.jumpFront));
    }

    [Test]
    public void ThreeRamsDislodgeRockAndStopRoadAfterExactlyFifteenSeconds()
    {
        ConvoyAccelerator accelerator = game.accelerator;
        Assert.That(accelerator.RockOnPedal, Is.True);
        Assert.That(game.CurrentSpeed, Is.EqualTo(game.roadSpeed));
        Assert.That(game.van.Find("Driver pedals/Brake pedal - decorative"), Is.Not.Null);
        game.RegisterRam(-1);
        accelerator.Step(0.45f);
        float firstZ = accelerator.rock.localPosition.z;
        game.RegisterRam(1);
        accelerator.Step(0.45f);
        Assert.That(accelerator.rock.localPosition.z, Is.LessThan(firstZ));
        Assert.That(game.CurrentSpeed, Is.EqualTo(game.roadSpeed));
        Assert.That(accelerator.NeedsRock, Is.False);
        game.RegisterRam(-1);
        Assert.That(game.Health, Is.EqualTo(2));
        Assert.That(accelerator.NeedsRock, Is.True);
        accelerator.Step(7.5f);
        Assert.That(game.CurrentSpeed, Is.EqualTo(game.roadSpeed * 0.5f).Within(0.001f));
        accelerator.RegisterImpact();
        accelerator.Step(7.49f);
        Assert.That(game.CurrentSpeed, Is.GreaterThan(0f));
        accelerator.Step(0.01f);
        Assert.That(game.CurrentSpeed, Is.EqualTo(0f).Within(0.001f));
        Assert.That(accelerator.rock.localPosition, Is.EqualTo(ConvoyAccelerator.FallenPosition));
        Vector3 roadPosition = game.road.GetChild(0).localPosition;
        game.ScrollRoad(10f);
        Assert.That(game.road.GetChild(0).localPosition, Is.EqualTo(roadPosition));
        Assert.That(game.SimulationRunning, Is.True, "Stopping must not disable walking or recovery.");
    }

    [TestCase(ConvoyPrototype.PlayerRole.Gunner)]
    [TestCase(ConvoyPrototype.PlayerRole.Driver)]
    public void EitherRoleCanCarryAndReplaceRockForAnEightSecondEscape(ConvoyPrototype.PlayerRole role)
    {
        game.showRoleSelection = true;
        game.SelectRole(role);
        ConvoyAccelerator accelerator = game.accelerator;
        for (int i = 0; i < 3; i++) game.RegisterRam(1);
        accelerator.Step(15f);
        game.player.SetSpawn(new Vector3(-0.9f, 0.214f, 2.95f), Quaternion.identity);
        int ammo = game.ShotsAvailable;
        Assert.That(game.player.TryInteract(), Is.True);
        Assert.That(game.player.CarryingRock, Is.True);
        Assert.That(game.player.TryPickUpAmmo(), Is.False);
        Assert.That(game.player.MountDriver(), Is.False);
        Assert.That(accelerator.NeedsRock, Is.True);
        game.player.SetSpawn(new Vector3(-0.9f, 0.214f, 3.1f), Quaternion.identity);
        Assert.That(game.player.TryInteract(), Is.True);
        Assert.That(game.player.CarryingRock, Is.False);
        Assert.That(accelerator.NeedsRock, Is.False);
        Assert.That(accelerator.Impacts, Is.Zero);
        Assert.That(game.CurrentSpeed, Is.Zero);
        accelerator.Step(0.5f);
        Assert.That(game.CurrentSpeed, Is.EqualTo(game.roadSpeed * ConvoyAccelerator.EscapeSpeed * 0.5f).Within(0.001f));
        accelerator.Step(0.5f);
        Assert.That(game.CurrentSpeed, Is.EqualTo(game.roadSpeed * ConvoyAccelerator.EscapeSpeed).Within(0.001f));
        accelerator.Step(6.99f);
        Assert.That(accelerator.Escaping, Is.True);
        accelerator.Step(0.02f);
        Assert.That(accelerator.Escaping, Is.False);
        accelerator.Step(1f);
        Assert.That(game.CurrentSpeed, Is.EqualTo(game.roadSpeed).Within(0.001f));
        Assert.That(game.Health, Is.EqualTo(2));
        Assert.That(game.ShotsAvailable, Is.EqualTo(ammo));
        accelerator.RegisterImpact();
        Assert.That(accelerator.Impacts, Is.EqualTo(1));
        Assert.That(accelerator.RockOnPedal, Is.True);
    }

    [Test]
    public void RockInteractionRequiresReachClearPathAndEmptyHandsAndPauseFreezesSpeed()
    {
        ConvoyAccelerator accelerator = game.accelerator;
        Assert.That(game.player.TryInteract(), Is.False, "The attached rock cannot be taken.");
        for (int i = 0; i < 3; i++) accelerator.RegisterImpact();
        accelerator.Step(1f);
        Assert.That(accelerator.TryPickUp(game.player), Is.False, "Cannot grab from the rear of the van.");
        game.SetPaused(true);
        float speed = game.CurrentSpeed;
        accelerator.Step(15f);
        Assert.That(game.CurrentSpeed, Is.EqualTo(speed));
        game.SetPaused(false);
        accelerator.Step(15f);
        game.player.SetSpawn(new Vector3(-0.9f, 0.214f, 2.95f), Quaternion.identity);
        GameObject blocker = PrototypeGeometry.Part("Interaction test wall", game.van,
            new Vector3(-0.81f, 0.36f, 2.95f), new Vector3(0.03f, 0.3f, 0.3f), material);
        Assert.That(accelerator.TryPickUp(game.player), Is.False, "No grabbing through solid geometry.");
        Object.DestroyImmediate(blocker);
        ConvoyAmmoPack pack = game.supplyPack;
        pack.transform.position = game.player.transform.TransformPoint(new Vector3(0f, 0.15f, 0.15f));
        Assert.That(game.player.TryPickUpAmmo(), Is.True);
        Assert.That(accelerator.TryPickUp(game.player), Is.False, "A battery and rock cannot be carried together.");
        game.player.ReleaseAmmo(false);
        pack.Consume();
        Assert.That(game.player.TryInteract(), Is.True);
        game.player.SetSpawn(new Vector3(-0.9f, 0.214f, 2f), Quaternion.identity);
        Assert.That(game.player.TryInteract(), Is.False, "No remote placement on the pedal.");
        Assert.That(game.player.CarryingRock, Is.True);
        game.SetPaused(true);
        Assert.That(game.player.CarryingRock, Is.False);
        Assert.That(accelerator.rock.localPosition, Is.EqualTo(ConvoyAccelerator.FallenPosition));
        Assert.That(accelerator.NeedsRock, Is.True);
    }

    [Test]
    public void SteeringScalesWithRoadSpeedAndCannotMoveStoppedVan()
    {
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        Assert.That(game.player.MountDriver(), Is.True);
        for (int i = 0; i < 3; i++) game.accelerator.RegisterImpact();
        game.accelerator.Step(7.5f);
        game.Steer(1f, 0.5f);
        Assert.That(game.SteeringOffset, Is.EqualTo(ConvoyPrototype.SteeringSpeed * 0.25f));
        game.accelerator.Step(7.5f);
        float offset = game.SteeringOffset;
        game.Steer(-1f, 5f);
        Assert.That(game.SteeringOffset, Is.EqualTo(offset));
    }

    [Test]
    public void ReplacementBodyMatchesOriginalAlignmentWithoutMovingGameplaySurfaces()
    {
        var original = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(VanModelSetupPath);
        MeshFilter oldBody = System.Array.Find(original.GetComponentsInChildren<MeshFilter>(), f => f.name == "pCube3");
        MeshFilter newBody = System.Array.Find(game.mainVanModel.GetComponentsInChildren<MeshFilter>(), f => f.name == "pCube3");
        Matrix4x4 alignment = Matrix4x4.TRS(new Vector3(0f, 0.0615f, -0.099f),
            Quaternion.Euler(0f, -90f, 0f), Vector3.one * 0.65f);
        var expected = new System.Collections.Generic.List<Vector3>();
        var actual = new System.Collections.Generic.List<Vector3>();
        foreach (Vector3 point in oldBody.sharedMesh.vertices)
            expected.Add(alignment.MultiplyPoint3x4(original.transform.InverseTransformPoint(oldBody.transform.TransformPoint(point))));
        foreach (Vector3 point in newBody.sharedMesh.vertices)
            actual.Add(game.mainVanModel.InverseTransformPoint(newBody.transform.TransformPoint(point)));
        float largest = 0f;
        foreach (Vector3 point in actual)
        {
            float nearest = float.PositiveInfinity;
            foreach (Vector3 target in expected) nearest = Mathf.Min(nearest, (point - target).sqrMagnitude);
            largest = Mathf.Max(largest, Mathf.Sqrt(nearest));
        }
        Assert.That(largest, Is.LessThan(0.005f),
            "New visible body must agree with preserved gameplay collision after export-origin correction.");
    }

    [Test]
    public void MainVanUsesV009WithOriginalGameplayCollisionAndChaserUsesV001()
    {
        Assert.That(game.mainVanModel, Is.Not.Null);
        Assert.That(game.mainVanModel.IsChildOf(game.van), Is.True);
        AssertIntactModel(game.mainVanModel, true);
        Assert.That(game.mainVanModel.gameObject.layer, Is.EqualTo(8));
        foreach (Transform child in game.van)
            Assert.That(PrototypeGeometry.IsLegacyVanShellPart(child.name), Is.False,
                "Old van geometry must be removed, not hidden: " + child.name);
        Assert.That(game.van.Find("Steel floor"), Is.Null);
        Assert.That(game.van.Find("Wheel"), Is.Null);
        Assert.That(game.gun.localPosition, Is.EqualTo(new Vector3(0f, 1.35f, -2.8f)));
        Assert.That(game.muzzle.position.z, Is.LessThan(game.van.position.z - 3.65f));
        Assert.That(game.ammoReceiver.localPosition, Is.EqualTo(new Vector3(0.55f, 0.35f, -1.35f)));
        PursuerVan enemy = PrototypeGeometry.CreatePursuer(game, 1, -10f);
        Transform chaser = enemy.visualRoot.Find("v001 pursuer van shell");
        AssertIntactModel(chaser, false);
        Bounds bounds = PrototypeGeometry.GetModelBounds(chaser);
        BoxCollider hull = enemy.visualRoot.GetComponent<BoxCollider>();
        Assert.That(Vector3.Distance(hull.center, bounds.center), Is.LessThan(0.001f));
        Assert.That(Vector3.Distance(hull.size, bounds.size), Is.LessThan(0.001f));
        Assert.That(bounds.size.x, Is.InRange(2.7f, 2.901f));
        Assert.That(bounds.size.y, Is.InRange(2.5f, 2.651f));
        Assert.That(bounds.size.z, Is.InRange(6f, 7.001f));
        Assert.That(bounds.min.y, Is.EqualTo(0.025f).Within(0.001f));
        Assert.That(enemy.damageIndicator.bounds.min.y, Is.GreaterThan(chaser.position.y + bounds.max.y));
        Assert.That(enemy.visualRoot.Find("Armored body"), Is.Null);
        Assert.That(enemy.visualRoot.GetComponent<BoxCollider>().enabled, Is.True);
        Assert.That(enemy.wheels.Length, Is.Zero);
        Assert.That(enemy.damageIndicator, Is.Not.Null);
    }

    private static void AssertIntactModel(Transform model, bool solid)
    {
        GameObject source = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(solid ? VanModelSetupPath :
            "Assets/Game Assets/truckchaser_v001.fbx");
        MeshFilter[] original = source.GetComponentsInChildren<MeshFilter>();
        MeshFilter[] actual = model.GetComponentsInChildren<MeshFilter>();
        Assert.That(actual.Length, Is.EqualTo(original.Length));
        for (int i = 0; i < actual.Length; i++)
        {
            if (solid)
            {
                if (actual[i].name == "pCube3")
                {
                    var replacement = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/Game Assets/in_van_assets/textures/Truck_v009.obj");
                    Assert.That(actual[i].sharedMesh, Is.SameAs(replacement.GetComponentInChildren<MeshFilter>().sharedMesh));
                    foreach (Material assigned in actual[i].GetComponent<Renderer>().sharedMaterials)
                        Assert.That(UnityEditor.AssetDatabase.GetAssetPath(assigned),
                            Is.EqualTo("Assets/Game Assets/in_van_assets/textures/in_van.mat"));
                }
                else
                {
                    MeshFilter pipe = System.Array.Find(original, f => f.name == actual[i].name);
                    Assert.That(pipe, Is.Not.Null);
                    Assert.That(actual[i].sharedMesh, Is.SameAs(pipe.sharedMesh));
                    Assert.That(actual[i].GetComponent<Renderer>().sharedMaterials,
                        Is.EqualTo(pipe.GetComponent<Renderer>().sharedMaterials));
                }
            }
            else
            {
                Assert.That(actual[i].sharedMesh, Is.SameAs(original[i].sharedMesh));
                Material appearance = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Game Assets/in_van_assets/textures/vann.mat");
                foreach (Material assigned in actual[i].GetComponent<Renderer>().sharedMaterials)
                    Assert.That(assigned, Is.SameAs(appearance));
            }
        }
        if (solid)
        {
            int triangleCount = 0;
            foreach (MeshFilter filter in original)
                triangleCount += filter.sharedMesh.triangles.Length;
            Assert.That(model.GetComponent<MeshCollider>().sharedMesh.triangles.Length,
                Is.EqualTo(triangleCount));
            Assert.That(model.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1),
                "Only original mesh collision is allowed; do not cover floor openings with a hull.");
            Mesh collision = model.GetComponent<MeshCollider>().sharedMesh;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            Matrix4x4 alignment = Matrix4x4.TRS(new Vector3(0f, 0.0615f, -0.099f),
                Quaternion.Euler(0f, -90f, 0f), Vector3.one * 0.65f);
            foreach (MeshFilter filter in original)
            {
                int start = vertices.Count;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                    vertices.Add(alignment.MultiplyPoint3x4(source.transform.InverseTransformPoint(
                        filter.transform.TransformPoint(vertex))));
                foreach (int index in filter.sharedMesh.triangles)
                    triangles.Add(start + index);
            }
            Assert.That(collision.triangles, Is.EqualTo(triangles),
                "Include every original body and pipe triangle without filling openings.");
            Vector3[] collisionVertices = collision.vertices;
            Assert.That(collisionVertices.Length, Is.EqualTo(vertices.Count));
            for (int i = 0; i < vertices.Count; i++)
                Assert.That(Vector3.Distance(collisionVertices[i], vertices[i]), Is.LessThan(0.0001f));
        }
        Vector3 scale = model.GetChild(0).localScale;
        Assert.That(scale.x, Is.EqualTo(scale.y));
        Assert.That(scale.y, Is.EqualTo(scale.z), "Only uniform model scaling is permitted.");
    }

    private const string VanModelSetupPath = "Assets/main_van/truckgreybox_v008.fbx";

    [Test]
    public void ModelFloorOpeningLetsCharacterFallButFloorAndBothPipesSupportIt()
    {
        GameObject isolated = Object.Instantiate(game.mainVanModelPrefab);
        isolated.transform.position = new Vector3(0f, 100f, 0f);
        GameObject probe = new GameObject("Floor collision probe");
        try
        {
            CharacterController controller = probe.AddComponent<CharacterController>();
            controller.height = ConvoyPlayer.BodyHeight;
            controller.radius = 0.043f;
            controller.center = new Vector3(0f, 0.11f, 0f);
            controller.skinWidth = 0.005f;
            controller.stepOffset = 0.03f;
            MeshCollider model = isolated.GetComponent<MeshCollider>();
            foreach (float x in new[] { -0.8f, -0.08f, 0.08f, 0.35f })
            {
                float z = x == -0.8f ? -1.5f : -0.75f;
                Vector3 start = new Vector3(x, 0.35f, z);
                controller.enabled = false;
                probe.transform.position = isolated.transform.TransformPoint(start);
                controller.enabled = true;
                Physics.SyncTransforms();
                RaycastHit hit;
                bool supported = model.Raycast(new Ray(probe.transform.position, Vector3.down), out hit, 1f);
                Assert.That(supported, Is.EqualTo(x != 0.35f),
                    "The floor and both pipes are solid, but the opening must remain empty at x=" + x);
                for (int step = 0; step < 100; step++)
                    controller.Move(Vector3.down * 0.01f);
                float y = isolated.transform.InverseTransformPoint(probe.transform.position).y;
                if (supported)
                {
                    float surface = isolated.transform.InverseTransformPoint(hit.point).y;
                    Assert.That(y, Is.EqualTo(surface - 0.01f).Within(0.025f),
                        "Character must rest on the visible floor or pipe at x=" + x);
                }
                else
                    Assert.That(y, Is.LessThan(-0.5f), "Character must fall through the opening.");
            }
        }
        finally
        {
            Object.DestroyImmediate(probe);
            Object.DestroyImmediate(isolated);
        }
    }

    [Test]
    public void SpriteAvatarSelectsAllEightFramesAndRemembersFacingWhenIdle()
    {
        ConvoySpriteAvatar avatar = game.player.spriteAvatar;
        Assert.That(avatar, Is.Not.Null);
        avatar.Animate(Vector2.zero, true, 0f);
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.idleBack));
        avatar.Animate(Vector2.up, true, 0f);
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.walk1Back));
        avatar.Animate(Vector2.up, true, ConvoySpriteAvatar.WalkFrameDuration + 0.001f);
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.walk2Back));
        avatar.Animate(Vector2.up, true, ConvoySpriteAvatar.WalkFrameDuration);
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.walk1Back));
        avatar.Animate(Vector2.zero, false, 0f);
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.jumpBack));
        avatar.Animate(Vector2.zero, true, 0f);
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.idleBack));
        avatar.Animate(Vector2.down, true, 0f);
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.walk1Front));
        avatar.Animate(Vector2.down, true, ConvoySpriteAvatar.WalkFrameDuration + 0.001f);
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.walk2Front));
        avatar.Animate(Vector2.down, false, 0f);
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.jumpFront));
        avatar.Animate(Vector2.zero, true, 0f);
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.idleFront));
        avatar.Animate(Vector2.right, true, 0f);
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.walk1Front),
            "Strafing retains the last front/back direction.");
    }

    [Test]
    public void SpriteAvatarFramesUseTinyScaleBottomPivotAndCrispTransparency()
    {
        ConvoySpriteAvatar avatar = game.player.spriteAvatar;
        Assert.That(avatar.GetComponentsInChildren<MeshRenderer>().Length, Is.Zero);
        Assert.That(avatar.GetComponentsInChildren<Collider>().Length, Is.Zero);
        foreach (Sprite sprite in new[] { avatar.idleFront, avatar.idleBack, avatar.walk1Front,
            avatar.walk2Front, avatar.walk1Back, avatar.walk2Back, avatar.jumpFront, avatar.jumpBack })
        {
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.rect.height / sprite.pixelsPerUnit, Is.EqualTo(ConvoyPlayer.BodyHeight).Within(0.001f));
            Assert.That(sprite.pivot.y, Is.Zero);
            Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(sprite.texture.mipmapCount, Is.EqualTo(1));
        }
        Assert.That(avatar.spriteRenderer.sharedMaterial.shader.name,
            Is.EqualTo("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
    }

    [Test]
    public void FifthRamShutsDownVanAndPreventsFurtherDamageMountingOrShooting()
    {
        game.player.transform.localPosition = new Vector3(0f, 0.6f, -1.35f);
        Assert.That(game.player.Mount(), Is.True);
        for (int i = 0; i < ConvoyPrototype.MaximumHealth; i++)
        {
            game.RegisterRam(i % 2 == 0 ? -1 : 1);
            Assert.That(game.Health, Is.EqualTo(4 - i));
            Assert.That(game.IsDead, Is.EqualTo(i == 4));
        }
        Assert.That(game.SimulationRunning, Is.False);
        Assert.That(game.Mounted, Is.False);
        Assert.That(game.TryFire(), Is.False);
        Assert.That(game.player.Mount(), Is.False);
        game.RegisterRam(1);
        Assert.That(game.RamCount, Is.EqualTo(5));
        game.SetPaused(false);
        Assert.That(game.SimulationRunning, Is.False);
    }

    [Test]
    public void RamMustTouchMainVanDamageHitbox()
    {
        PursuerVan enemy = game.SpawnPursuer(1, -30f);
        Assert.That(game.vanHitbox.isTrigger, Is.True);
        Assert.That(game.TryRegisterRam(enemy, enemy.transform.localPosition), Is.False);
        Assert.That(game.Health, Is.EqualTo(5));
        Assert.That(game.TryRegisterRam(enemy, new Vector3(PursuerVan.ContactX, 0f, -0.25f)), Is.True);
        Assert.That(game.Health, Is.EqualTo(4));
    }

    [Test]
    public void LaserHitSlidesVanBackWithoutShakeOrPassingFollower()
    {
        PursuerVan enemy = game.SpawnPursuer(1, -30f);
        PursuerVan follower = game.SpawnPursuer(1, -39f);
        float initialZ = enemy.transform.localPosition.z;
        enemy.TakeHit();
        Assert.That(enemy.transform.localPosition.z, Is.EqualTo(initialZ));
        enemy.Step(0.1f);
        Assert.That(enemy.transform.localPosition.z, Is.EqualTo(initialZ - 1f).Within(0.01f));
        Assert.That(enemy.KnockbackSpeed, Is.GreaterThan(0f));
        for (int i = 0; i < 70; i++)
        {
            enemy.Step(0.02f);
            follower.Step(0.02f);
            Assert.That(enemy.visualRoot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(enemy.visualRoot.localRotation, Quaternion.identity), Is.LessThan(0.01f));
            Assert.That(enemy.transform.localPosition.z - follower.transform.localPosition.z,
                Is.GreaterThanOrEqualTo(PursuerVan.FollowingDistance - 0.01f));
        }
        Assert.That(enemy.KnockbackSpeed, Is.Zero);
        Assert.That(enemy.visualRoot.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(Quaternion.Angle(enemy.visualRoot.localRotation, Quaternion.identity), Is.LessThan(0.01f));
    }

    [Test]
    public void LaserHitSlowsSurvivorForExactlyEightSecondsAndInterruptsRam()
    {
        PursuerVan enemy = game.SpawnPursuer(1, -0.25f);
        enemy.Step(0.02f);
        enemy.Step(PursuerVan.TelegraphDuration + 0.05f);
        Assert.That(enemy.State, Is.EqualTo(PursuerVan.ChaseState.Ramming));
        enemy.TakeHit();
        Assert.That(enemy.SlowRemaining, Is.EqualTo(8f));
        Assert.That(enemy.State, Is.EqualTo(PursuerVan.ChaseState.Approaching));
        for (int i = 0; i < 799; i++)
            enemy.Step(0.01f);
        Assert.That(enemy.SlowRemaining, Is.InRange(0.005f, 0.015f));
        Assert.That(game.RamCount, Is.Zero);
        enemy.Step(0.02f);
        Assert.That(enemy.SlowRemaining, Is.Zero);
    }

    [Test]
    public void ReturningFocusResumesWalkingButPreservesManualPause()
    {
        var focusCallback = typeof(ConvoyPrototype).GetMethod("OnApplicationFocus",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(focusCallback, Is.Not.Null);
        game.SetPaused(false);
        focusCallback.Invoke(game, new object[] { false });
        Assert.That(game.Paused, Is.True);
        Assert.That(game.SimulationRunning, Is.False);
        focusCallback.Invoke(game, new object[] { false });
        focusCallback.Invoke(game, new object[] { true });
        Assert.That(game.Paused, Is.False);
        Assert.That(game.SimulationRunning, Is.True);

        game.SetPaused(true);
        focusCallback.Invoke(game, new object[] { false });
        focusCallback.Invoke(game, new object[] { true });
        Assert.That(game.Paused, Is.True);
        Assert.That(game.SimulationRunning, Is.False);
    }

    [Test]
    public void FullSizeBatteryPickupKeepsReplacementsOnPassengerSeatAndLoadsFourShots()
    {
        Assert.That(ConvoyAmmoPack.Scale, Is.EqualTo(1f));
        Assert.That(ConvoyAmmoPack.CarryDistance - 0.1f,
            Is.GreaterThan(game.player.GetComponent<CharacterController>().radius),
            "Full-size battery back edge must clear the character's torso.");
        Vector3 supplyPosition = game.ammoSupply.position;
        Quaternion supplyRotation = game.ammoSupply.rotation;
        for (int pickup = 0; pickup < 3; pickup++)
        {
            CharacterController controller = game.player.GetComponent<CharacterController>();
            controller.enabled = false;
            game.player.transform.localPosition = new Vector3(0.6f, 0.82f, 2.5f);
            controller.enabled = true;
            ConvoyAmmoPack pack = game.supplyPack;
            Assert.That(game.player.TryPickUpAmmo(), Is.True, "Battery must be reachable on the passenger seat.");
            Assert.That(game.player.CarriedAmmo, Is.SameAs(pack));
            Assert.That(pack.transform.localScale, Is.EqualTo(Vector3.one * ConvoyAmmoPack.Scale));
            Assert.That(pack.transform.localPosition, Is.EqualTo(new Vector3(0f,
                ConvoyAmmoPack.CarryHeight, ConvoyAmmoPack.CarryDistance)));
            Assert.That(pack.GetComponent<Collider>().enabled, Is.False);
            Assert.That(game.supplyPack, Is.Not.SameAs(pack));
            Assert.That(game.supplyPack.transform.position, Is.EqualTo(supplyPosition));
            Assert.That(Quaternion.Angle(game.supplyPack.transform.rotation, supplyRotation), Is.LessThan(0.001f));
            Assert.That(game.supplyPack.transform.localScale, Is.EqualTo(pack.transform.localScale));
            Assert.That(game.supplyPack.GetComponent<Collider>().isTrigger, Is.True,
                "Replacement must not trap or shove the player at the pickup spot.");
            controller.enabled = false;
            game.player.transform.position = game.ammoReceiver.position -
                game.player.transform.TransformVector(new Vector3(0f,
                    ConvoyAmmoPack.CarryHeight, ConvoyAmmoPack.CarryDistance));
            controller.enabled = true;
            game.player.ReleaseAmmo(true);
            Assert.That(game.player.CarriedAmmo, Is.Null);
            Assert.That(pack.Consumed, Is.True);
            Assert.That(game.ShotsAvailable, Is.EqualTo(4 + (pickup + 1) * 4));
        }
    }

    [Test]
    public void AmmoPacksAddFourStackAndCannotBeConsumedTwice()
    {
        ConvoyAmmoPack pack = game.supplyPack;
        pack.transform.position = game.ammoReceiver.position;
        Assert.That(game.TryLoadAmmo(pack), Is.True);
        Assert.That(game.ShotsAvailable, Is.EqualTo(8));
        Assert.That(game.TryLoadAmmo(pack), Is.False);
        Assert.That(game.ShotsAvailable, Is.EqualTo(8));
        ConvoyAmmoPack second = PrototypeGeometry.CreateAmmoPack(game, game.ammoReceiver);
        Assert.That(game.TryLoadAmmo(second), Is.True);
        Assert.That(game.ShotsAvailable, Is.EqualTo(12));
    }

    [Test]
    public void AmmoCannotLoadFromPassengerSeatThroughWallOrWhilePaused()
    {
        ConvoyAmmoPack pack = game.supplyPack;
        Assert.That(game.TryLoadAmmo(pack), Is.False);
        pack.transform.position = game.ammoReceiver.position + Vector3.forward * 0.4f;
        PrototypeGeometry.Part("Blocked ammo loading", game.van,
            game.ammoReceiver.localPosition + Vector3.forward * 0.2f,
            new Vector3(0.5f, 0.5f, 0.05f), material);
        Assert.That(game.TryLoadAmmo(pack), Is.False);
        pack.transform.position = game.ammoReceiver.position;
        game.SetPaused(true);
        Assert.That(game.TryLoadAmmo(pack), Is.False);
        Assert.That(game.ShotsAvailable, Is.EqualTo(4));
        Assert.That(pack.Consumed, Is.False);
    }

    [Test]
    public void ScrollingMovesWorldBackwardsButNotVan()
    {
        Vector3 vanPosition = game.van.position;
        float tileZ = game.road.GetChild(5).localPosition.z;
        game.ScrollRoad(0.5f);
        Assert.That(game.road.GetChild(5).localPosition.z, Is.EqualTo(tileZ - game.roadSpeed * 0.5f).Within(0.001f));
        Assert.That(game.van.position, Is.EqualTo(vanPosition));
        Assert.That(game.road.childCount, Is.EqualTo(ConvoyPrototype.SegmentCount));
        Assert.That(game.road.GetChild(ConvoyPrototype.SegmentCount - 1).localPosition.z,
            Is.GreaterThanOrEqualTo(900f), "Actual road geometry must extend into the forward view.");
        Assert.That(game.player.view.farClipPlane, Is.GreaterThanOrEqualTo(1000f));
    }

    [TestCase(-217f, 935f)]
    [TestCase(-216f, -216f)]
    [TestCase(936f, -216f)]
    [TestCase(-1368f, -216f)]
    public void RoadWrapHasNoGapEvenAcrossSeveralLengths(float input, float expected)
    {
        Assert.That(ConvoyPrototype.WrapRoadPosition(input), Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void DriverSelectionGatesSimulationAndSteeringAndPreservesGunnerMode()
    {
        game.showRoleSelection = true;
        Assert.That(game.ChoosingRole, Is.True);
        Assert.That(game.SimulationRunning, Is.False);
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        Assert.That(game.DriverPractice, Is.True);
        Assert.That(game.Pursuers.Count, Is.Zero);
        Assert.That(game.player.transform.localPosition, Is.EqualTo(ConvoyPrototype.DriverSpawn));
        Assert.That(game.player.Mount(), Is.False);
        game.Steer(1f, 1f);
        Assert.That(game.SteeringOffset, Is.Zero);
        Assert.That(game.player.MountDriver(), Is.True);
        game.Steer(1f, 0.5f);
        Assert.That(game.SteeringOffset, Is.EqualTo(ConvoyPrototype.RoadEdge));
        game.Steer(1f, 20f);
        Assert.That(game.SteeringOffset, Is.EqualTo(ConvoyPrototype.RoadEdge));
        game.Steer(-1f, 20f);
        Assert.That(game.SteeringOffset, Is.EqualTo(-ConvoyPrototype.RoadEdge));
        game.SetPaused(true);
        Assert.That(game.player.Driving, Is.False);
        game.Steer(1f, 1f);
        Assert.That(game.SteeringOffset, Is.EqualTo(-ConvoyPrototype.RoadEdge));
        Assert.That(game.player.GetComponent<CharacterController>().enabled, Is.True);
    }

    [Test]
    public void FallRespawnsAtSavedTransformAfterExactlyThreeSecondsThenFadesIn()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var awake = typeof(ConvoyPlayer).GetMethod("Awake", flags);
        var begin = typeof(ConvoyPlayer).GetMethod("BeginRespawn", flags);
        var advance = typeof(ConvoyPlayer).GetMethod("AdvanceRespawn", flags);
        Vector3 spawn = game.player.transform.localPosition;
        Quaternion rotation = game.player.transform.localRotation;
        awake.Invoke(game.player, null);
        game.player.AddImpact(Vector3.right * 3f, 9f);
        game.player.transform.localPosition = new Vector3(0.35f, -0.6f, -0.75f);
        game.player.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        begin.Invoke(game.player, null);
        Assert.That(game.player.IsRespawning, Is.True);
        Assert.That(game.player.RespawnRemaining, Is.EqualTo(3f));
        Assert.That(game.player.RespawnFadeAlpha, Is.EqualTo(1f));
        Assert.That(game.player.GetComponent<CharacterController>().enabled, Is.False);
        Assert.That(game.SimulationRunning, Is.False);
        int health = game.Health;
        game.RegisterRam(1);
        Assert.That(game.Health, Is.EqualTo(health), "No damage while waiting to respawn.");
        Assert.That(game.player.Mount(), Is.False);
        advance.Invoke(game.player, new object[] { 2.99f });
        Assert.That(game.player.IsRespawning, Is.True);
        Assert.That(game.player.transform.localPosition.y, Is.LessThan(-0.5f));
        advance.Invoke(game.player, new object[] { 0.01f });
        advance.Invoke(game.player, new object[] { 0.00001f });
        Assert.That(game.player.IsRespawning, Is.False);
        Assert.That(game.player.transform.localPosition, Is.EqualTo(spawn));
        Assert.That(Quaternion.Angle(game.player.transform.localRotation, rotation), Is.LessThan(0.001f));
        Assert.That(game.player.GetComponent<CharacterController>().enabled, Is.True);
        Assert.That(game.player.ImpactVelocity, Is.EqualTo(Vector3.zero));
        Assert.That(game.SimulationRunning, Is.True);
        Assert.That(game.player.RespawnFadeAlpha, Is.EqualTo(1f).Within(0.001f));
        advance.Invoke(game.player, new object[] { 0.3f });
        Assert.That(game.player.RespawnFadeAlpha, Is.EqualTo(0.5f).Within(0.001f));
        advance.Invoke(game.player, new object[] { 0.3f });
        Assert.That(game.player.RespawnFadeAlpha, Is.Zero);
        begin.Invoke(game.player, null);
        Assert.That(game.player.RespawnRemaining, Is.EqualTo(3f), "Repeated falls must restart the countdown.");
    }

    [Test]
    public void EasierChaseUsesTwoVansSlowerApproachAndLongerWarnings()
    {
        Assert.That(game.maxPursuers, Is.EqualTo(2));
        Assert.That(game.spawnInterval, Is.EqualTo(16f));
        PursuerVan enemy = game.SpawnPursuer(1, -20f);
        float z = enemy.transform.localPosition.z;
        enemy.Step(1f);
        Assert.That(enemy.transform.localPosition.z - z, Is.EqualTo(4.5f).Within(0.001f));
        for (int i = 0; i < 200 && enemy.State == PursuerVan.ChaseState.Approaching; i++)
            enemy.Step(0.02f);
        Assert.That(enemy.State, Is.EqualTo(PursuerVan.ChaseState.Telegraphing));
        enemy.Step(2.49f);
        Assert.That(enemy.State, Is.EqualTo(PursuerVan.ChaseState.Telegraphing));
        enemy.Step(0.02f);
        Assert.That(enemy.State, Is.EqualTo(PursuerVan.ChaseState.Ramming));
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void SideImpactPushesOccupantAwayFromStruckSide(int side)
    {
        game.RegisterRam(side);
        Assert.That(game.player.ImpactVelocity.x * side, Is.LessThan(0f));
        Assert.That(game.RamCount, Is.EqualTo(1));
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void ChaserApproachesTelegraphsAndRamsOnlyOncePerAttack(int side)
    {
        PursuerVan enemy = game.SpawnPursuer(side, -20f);
        for (int i = 0; i < 600 && game.RamCount == 0; i++)
            enemy.Step(0.02f);
        Assert.That(game.RamCount, Is.EqualTo(1));
        Assert.That(enemy.State, Is.EqualTo(PursuerVan.ChaseState.Retreating));
        for (int i = 0; i < 100; i++)
            enemy.Step(0.02f);
        Assert.That(game.RamCount, Is.EqualTo(1));
        Assert.That(Mathf.Abs(enemy.transform.position.x), Is.GreaterThan(3.05f));
        Assert.That(enemy.transform.position.z, Is.LessThan(-5f));
    }

    [Test]
    public void ChaserSurvivesFirstHitExplodesOnSecondAndCannotScoreAgain()
    {
        PursuerVan enemy = game.SpawnPursuer(1, -30f);
        Assert.That(enemy.TakeHit(), Is.False);
        Assert.That(enemy.Hits, Is.EqualTo(1));
        Assert.That(enemy.IsDestroyed, Is.False);
        Assert.That(game.DestroyedVans, Is.Zero);
        Assert.That(enemy.TakeHit(), Is.True);
        Assert.That(enemy.IsDestroyed, Is.True);
        Assert.That(enemy.gameObject.activeSelf, Is.False);
        Assert.That(game.Pursuers.Contains(enemy), Is.False);
        Assert.That(game.DestroyedVans, Is.EqualTo(1));
        Assert.That(enemy.TakeHit(), Is.False);
        Assert.That(game.DestroyedVans, Is.EqualTo(1));
    }

    [Test]
    public void MountRequiresStoolAndDismountRestoresWalkingAndSingleCamera()
    {
        game.player.transform.localPosition = new Vector3(0f, 0.14f, 0.5f);
        Assert.That(game.player.Mount(), Is.False);
        game.player.transform.localPosition = new Vector3(0f, 0.6f, -1.35f);
        Assert.That(game.player.Mount(), Is.True);
        Assert.That(game.gunCamera.enabled, Is.True);
        Assert.That(game.player.view.enabled, Is.False);
        Assert.That(game.player.avatar.gameObject.activeSelf, Is.False);
        Assert.That(game.player.GetComponent<CharacterController>().enabled, Is.False);
        game.player.Dismount();
        Assert.That(game.gunCamera.enabled, Is.False);
        Assert.That(game.player.view.enabled, Is.True);
        Assert.That(game.player.avatar.gameObject.activeSelf, Is.True);
        Assert.That(game.player.GetComponent<CharacterController>().enabled, Is.True);
    }

    [Test]
    public void RailgunRayDamagesVanAndCooldownPreventsRepeatedShot()
    {
        game.player.transform.localPosition = new Vector3(0f, 0.6f, -1.35f);
        Assert.That(game.player.Mount(), Is.True);
        PursuerVan enemy = game.SpawnPursuer(1, -20f);
        enemy.transform.position = new Vector3(0f, 0f, -20f);
        game.gunCamera.transform.LookAt(enemy.transform.position + Vector3.up * 1.5f);
        Physics.SyncTransforms();
        Assert.That(game.TryFire(), Is.True);
        Assert.That(enemy.Hits, Is.EqualTo(1));
        Assert.That(game.TryFire(), Is.False);
        Assert.That(enemy.Hits, Is.EqualTo(1));
        Assert.That(game.Cooldown, Is.EqualTo(0.8f));
        Assert.That(game.HitMarker, Is.GreaterThan(0f));
    }

    [Test]
    public void PauseDismountsAndPreventsFiring()
    {
        game.player.transform.localPosition = new Vector3(0f, 0.6f, -1.35f);
        game.player.Mount();
        game.SetPaused(true);
        Assert.That(game.player.Mounted, Is.False);
        Assert.That(game.Paused, Is.True);
        Assert.That(game.TryFire(), Is.False);
    }

    [Test]
    public void SameLaneVansKeepTheirDistanceAcrossRepeatedRamAndRetreatCycles()
    {
        PursuerVan leader = game.SpawnPursuer(1, -20f);
        PursuerVan follower = game.SpawnPursuer(1, -40f);
        for (int i = 0; i < 2500; i++)
        {
            leader.Step(0.02f);
            follower.Step(0.02f);
            Assert.That(leader.transform.localPosition.z - follower.transform.localPosition.z,
                Is.GreaterThanOrEqualTo(PursuerVan.FollowingDistance - 0.5f));
        }
        Assert.That(game.RamCount, Is.GreaterThan(2));
    }
}
