using System.Collections;
using System.Collections.Generic;
using Convoy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class ConvoyPlayModeTests
{
    private ConvoyPrototype game;
    private ConvoyPrototype authoredGame;
    private Keyboard keyboard;
    private Mouse mouse;
    private InputTestFixture inputFixture;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return SceneManager.LoadSceneAsync("ConvoyPrototype");
        yield return null;
        inputFixture = new InputTestFixture();
        inputFixture.Setup();
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        ConvoyPrototype savedGame = Object.FindFirstObjectByType<ConvoyPrototype>();
        if (savedGame.ChoosingRole)
            savedGame.SelectRole(ConvoyPrototype.PlayerRole.Gunner);
        authoredGame = savedGame;
        Assert.That(savedGame, Is.Not.Null);
        Assert.That(savedGame.player.moveSpeed, Is.GreaterThan(0f));
        Assert.That(savedGame.player.GetComponent<CharacterController>().height,
            Is.EqualTo(ConvoyPlayer.BodyHeight).Within(0.001f));
        Assert.That(savedGame.player.avatar, Is.Not.Null);
        AssertNoSolidPenetration(savedGame.player.GetComponent<CharacterController>(), savedGame);
        savedGame.gameObject.SetActive(false);
        // Keep mechanics tests independent of collaborators rearranging the saved cargo layout.
        game = new GameObject("Movement test fixture").AddComponent<ConvoyPrototype>();
        game.showRoleSelection = false;
        game.metal = savedGame.metal;
        game.dark = savedGame.dark;
        game.dirt = savedGame.dirt;
        game.grass = savedGame.grass;
        game.foliage = savedGame.foliage;
        game.orange = savedGame.orange;
        game.cyan = savedGame.cyan;
        game.glass = savedGame.glass;
        game.rubber = savedGame.rubber;
        game.playerAvatarPrefab = savedGame.playerAvatarPrefab;
        game.player2AvatarPrefab = savedGame.player2AvatarPrefab;
        game.natureSprites = savedGame.natureSprites;
        game.natureMaterial = savedGame.natureMaterial;
        game.natureBatchMaterials = savedGame.natureBatchMaterials;
        game.mainVanModelPrefab = savedGame.mainVanModelPrefab;
        game.pursuerVanModelPrefab = savedGame.pursuerVanModelPrefab;
        PrototypeGeometry.Build(game);
        keyboard = InputSystem.AddDevice<Keyboard>("ConvoyTestKeyboard");
        mouse = InputSystem.AddDevice<Mouse>("ConvoyTestMouse");
        yield return null;
        game.SetPaused(false);
        Assert.That(mouse.enabled, Is.True);
        Assert.That(keyboard.enabled, Is.True);
        Assert.That(Mouse.current, Is.SameAs(mouse));
        Assert.That(Keyboard.current, Is.SameAs(keyboard));
    }

    [UnityTearDown]
    public IEnumerator TearDownScene()
    {
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        if (game != null)
            game.SetPaused(true);
        yield return null;
        if (inputFixture != null)
            inputFixture.TearDown();
    }

    [UnityTest]
    public IEnumerator FirstEmptyGunActualClicksGuideBatteryPickupAndEAtRailgun()
    {
        game.player.SetSpawn(new Vector3(0.9f, 0.82f, -1.35f), Quaternion.identity);
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
        yield return new WaitForSeconds(0.1f);
        Assert.That(game.player.Mounted, Is.True);
        for (int shot = 0; shot < 4; shot++)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right).WithButton(MouseButton.Left));
            yield return new WaitForSeconds(0.1f);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return new WaitForSeconds(0.85f);
        }
        Assert.That(game.ShotsAvailable, Is.Zero);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.GunFire), Is.True);
        Assert.That(game.Tutorial.Current.Lesson, Is.EqualTo(ConvoyTutorial.Lesson.BatteryPickup));
        Assert.That(game.Tutorial.Current.Key, Is.EqualTo("RMB"));
        Assert.That(game.Tutorial.Current.Target, Is.SameAs(game.supplyPack.transform));
        InputSystem.QueueStateEvent(mouse, new MouseState());
        yield return new WaitForSeconds(0.2f);
        Assert.That(game.Tutorial.Current.Key, Is.EqualTo("WASD"));
        game.player.SetSpawn(new Vector3(0.6f, 0.82f, 2.5f), Quaternion.identity);
        yield return new WaitForSeconds(0.2f);
        Assert.That(game.Tutorial.Current.Key, Is.EqualTo("E"));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
        yield return new WaitForSeconds(0.1f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.2f);
        Assert.That(game.player.CarriedAmmo, Is.Not.Null);
        Assert.That(game.Tutorial.Current.Target, Is.SameAs(game.gun));
        Assert.That(Vector3.Distance(game.Tutorial.Current.TargetPosition,
            game.gun.TransformPoint(ConvoyPrototype.GunLoadingPoint)), Is.LessThan(0.001f));
        game.player.SetSpawn(new Vector3(-0.25f, 1f, -2f), Quaternion.identity);
        yield return null;
        Assert.That(game.Tutorial.FindHint().Key, Is.EqualTo("E"));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
        yield return null;
        yield return null;
        Assert.That(game.ShotsAvailable, Is.EqualTo(4));
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.BatteryLoad), Is.True);
    }

    [UnityTest]
    public IEnumerator TutorialActualControlsTeachJumpDrivingAndHideDuringRespawn()
    {
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        yield return new WaitForSeconds(0.2f);
        Assert.That(game.Tutorial.Current.Lesson, Is.EqualTo(ConvoyTutorial.Lesson.DriverMount));
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
        yield return new WaitForSeconds(0.1f);
        Assert.That(game.player.Driving, Is.True);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.DriverMount), Is.True);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.Steer), Is.False);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(0.3f);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.Steer), Is.True);
        Assert.That(game.Tutorial.Current.Lesson, Is.EqualTo(ConvoyTutorial.Lesson.None));
        InputState.Change(mouse, new MouseState());
        InputState.Change(keyboard, new KeyboardState());
        game.player.SetSpawn(new Vector3(-0.9f, 0.214f, 2f), Quaternion.identity);
        yield return new WaitForSeconds(0.2f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return new WaitForSeconds(0.1f);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.Jump), Is.True);
        InputState.Change(keyboard, new KeyboardState());
        game.player.SetSpawn(new Vector3(0f, -0.6f, 0f), Quaternion.identity);
        yield return null;
        yield return null;
        Assert.That(game.player.IsRespawning, Is.True);
        Assert.That(game.Tutorial.Visible, Is.False);
        Assert.That(game.Tutorial.Knows(ConvoyTutorial.Lesson.Jump), Is.True);
    }

    [UnityTest]
    public IEnumerator DifficultyActualUpdateSpawnsExtraChasersAtThirtySecondsAndRespectsCap()
    {
        game.player.enabled = false;
        game.AdvanceDifficulty(ConvoyPrototype.DifficultyInterval - 1f);
        Assert.That(game.DifficultyStage, Is.Zero);
        yield return new WaitForSeconds(1.2f);
        Assert.That(game.DifficultyStage, Is.EqualTo(1));
        Assert.That(game.CurrentSpeed, Is.EqualTo(game.roadSpeed * 1.1f).Within(0.001f));
        for (int i = 0; i < 4; i++)
        {
            ConvoyWorldState state = game.CaptureWorld();
            state.SpawnTimer = 0f;
            game.ApplyWorld(state);
            yield return null;
            yield return null;
        }
        Assert.That(game.Pursuers.Count, Is.EqualTo(3), "Actual Update must add the third van at difficulty two.");
        game.AdvanceDifficulty(600f);
        for (int i = 0; i < 10; i++)
        {
            ConvoyWorldState state = game.CaptureWorld();
            state.SpawnTimer = 0f;
            game.ApplyWorld(state);
            yield return null;
            yield return null;
        }
        Assert.That(game.Pursuers.Count, Is.EqualTo(ConvoyPrototype.PursuerLimit));
    }

    [UnityTest]
    public IEnumerator DriverRoadRockActualUpdateHitsOnceAndSteeringAvoidsNextRock()
    {
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        Assert.That(game.roadRocks.Rocks.Count, Is.EqualTo(1));
        game.roadRocks.Rocks[0].localPosition = new Vector3(0f, -0.02f, 22f);
        yield return new WaitForSeconds(1.2f);
        Assert.That(game.RamCount, Is.EqualTo(1));
        Assert.That(game.roadRocks.Rocks.Count, Is.Zero);
        Assert.That(game.accelerator.Impacts, Is.EqualTo(1));
        game.player.SetSpawn(ConvoyPrototype.DriverSpawn, Quaternion.identity);
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(1.6f);
        Assert.That(game.player.Driving, Is.True);
        Assert.That(game.SteeringOffset, Is.GreaterThan(4f));
        InputState.Change(keyboard, new KeyboardState());
        game.roadRocks.SpawnRock(0, 22f);
        yield return new WaitForSeconds(2.2f);
        Assert.That(game.RamCount, Is.EqualTo(1), "Actual A/D steering should avoid the next center-lane rock.");
        Assert.That(game.roadRocks.Rocks.Count, Is.Zero);
        Assert.That(game.Pursuers.Count, Is.Zero);
        InputState.Change(mouse, new MouseState());
    }

    [UnityTest]
    public IEnumerator RoadsideSpritesFaceBothForwardAndRearCamerasWithoutTilting()
    {
        ConvoyNatureBatch tree = game.road.GetChild(9).GetComponentInChildren<ConvoyNatureBatch>();
        Camera camera = game.player.view;
        RenderTexture target = new RenderTexture(128, 128, 16);
        Texture2D pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        int mask = camera.cullingMask;
        Color background = camera.backgroundColor;
        CameraClearFlags clearFlags = camera.clearFlags;
        RenderTexture previous = RenderTexture.active;
        tree.gameObject.layer = 31;
        camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.magenta;
        target.Create();
        camera.targetTexture = target;
        try
        {
            Vector3 center = tree.transform.TransformPoint(tree.placements[0].position) + Vector3.up * 8f;
            foreach (Vector3 offset in new[] { new Vector3(3f, 2f, -16f), new Vector3(-3f, 2f, 16f) })
            {
                camera.transform.position = center + offset;
                camera.transform.LookAt(center);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                pixels.Apply();
                int visible = 0;
                foreach (Color32 pixel in pixels.GetPixels32())
                    if (pixel.g > pixel.r && pixel.g > pixel.b)
                        visible++;
                Assert.That(visible, Is.GreaterThan(100), "GPU billboards must render tree pixels from both directions.");
            }
        }
        finally
        {
            tree.gameObject.layer = 0;
            camera.cullingMask = mask;
            camera.backgroundColor = background;
            camera.clearFlags = clearFlags;
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(pixels);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator FallingWhileCarryingRockReturnsItToFootwellAndFreezesSlowdownDuringRespawn()
    {
        for (int i = 0; i < 3; i++) game.accelerator.RegisterImpact();
        game.accelerator.Step(7.5f);
        game.player.SetSpawn(new Vector3(-0.9f, 0.214f, 2.95f), Quaternion.identity);
        Assert.That(game.player.TryInteract(), Is.True);
        CharacterController controller = game.player.GetComponent<CharacterController>();
        controller.enabled = false;
        game.player.transform.localPosition = new Vector3(-0.4f, -0.6f, -0.75f);
        controller.enabled = true;
        yield return null;
        yield return null;
        Assert.That(game.player.IsRespawning, Is.True);
        Assert.That(game.player.CarryingRock, Is.False);
        Assert.That(game.accelerator.rock.localPosition, Is.EqualTo(ConvoyAccelerator.FallenPosition));
        float speed = game.CurrentSpeed;
        yield return new WaitForSeconds(2f);
        Assert.That(game.CurrentSpeed, Is.EqualTo(speed));
        Assert.That(game.accelerator.NeedsRock, Is.True);
        yield return new WaitForSeconds(1.2f);
        Assert.That(game.player.IsRespawning, Is.False);
        Assert.That(game.player.TryInteract(), Is.True, "The same rock must remain recoverable after falling.");
    }

    [UnityTest]
    public IEnumerator AuthoredAcceleratorRockCanBeRecoveredWithEAndRestoresSpeed()
    {
        game.gameObject.SetActive(false);
        game = authoredGame;
        game.gameObject.SetActive(true);
        game.SetPaused(false);
        foreach (PursuerVan enemy in game.Pursuers)
            enemy.gameObject.SetActive(false);
        game.spawnInterval = 1000f;
        for (int i = 0; i < 3; i++) game.RegisterRam(1);
        Assert.That(game.accelerator.NeedsRock, Is.True);
        game.accelerator.Step(15f);
        game.player.SetSpawn(new Vector3(-0.9f, 0.214f, 2.95f), Quaternion.identity);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
        yield return null;
        yield return null;
        Assert.That(game.player.CarryingRock, Is.True, "E must pick up the visible footwell rock.");
        InputState.Change(keyboard, new KeyboardState());
        yield return null;
        game.player.SetSpawn(new Vector3(-0.9f, 0.214f, 3.1f), Quaternion.identity);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
        yield return null;
        yield return null;
        Assert.That(game.player.CarryingRock, Is.False);
        Assert.That(game.accelerator.RockOnPedal, Is.True);
        Assert.That(game.accelerator.NeedsRock, Is.False);
        InputState.Change(keyboard, new KeyboardState());
        game.accelerator.Step(5f);
        Assert.That(game.CurrentSpeed, Is.EqualTo(game.roadSpeed));
        float distance = game.DistanceTravelled;
        yield return new WaitForSeconds(0.1f);
        Assert.That(game.DistanceTravelled, Is.GreaterThan(distance));
        Assert.That(game.Health, Is.EqualTo(2));
        Assert.That(game.ShotsAvailable, Is.EqualTo(4));
    }

    [UnityTest]
    public IEnumerator DriverPracticeHoldSteersVanAndReleaseRestoresWalkingWithoutChasers()
    {
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        Assert.That(game.player.spriteAvatar.idleBack, Is.SameAs(game.player2AvatarPrefab.idleBack));
        Assert.That(game.player.spriteAvatar.idleBack.name, Is.EqualTo("Gleif_Idle_Back"));
        Assert.That(game.Pursuers.Count, Is.Zero);
        float localZ = game.player.transform.localPosition.z;
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(0.4f);
        Assert.That(game.player.Driving, Is.True);
        Assert.That(game.player.GetComponent<CharacterController>().enabled, Is.False);
        Assert.That(game.SteeringOffset, Is.InRange(4f, ConvoyPrototype.RoadEdge));
        Assert.That(game.van.localPosition.x, Is.EqualTo(game.SteeringOffset).Within(0.1f));
        Assert.That(game.player.transform.localPosition.z, Is.EqualTo(localZ).Within(0.001f));
        Assert.That(Vector3.Distance(game.player.view.transform.position,
            game.van.TransformPoint(ConvoyPrototype.DriverCameraPosition)), Is.LessThan(0.01f));
        Assert.That(game.player.view.enabled, Is.True);
        Assert.That(game.gunCamera.enabled, Is.False);
        Assert.That(game.player.view.GetComponent<AudioListener>().enabled, Is.True);
        MeshCollider truckCollider = game.mainVanModel.GetComponent<MeshCollider>();
        Physics.SyncTransforms();
        Assert.That(truckCollider.Raycast(game.player.view.ViewportPointToRay(new Vector3(0.5f, 0.1f, 0f)),
            out RaycastHit hoodHit, 10f), Is.True, "The hood must be visible in the lower driving view.");
        Assert.That(game.van.InverseTransformPoint(hoodHit.point).z, Is.GreaterThan(4.35f));
        Assert.That(truckCollider.Raycast(game.player.view.ViewportPointToRay(new Vector3(0.5f, 0.55f, 0f)),
            out _, 10f), Is.False, "The truck must not block the forward road view.");
        InputState.Change(keyboard, new KeyboardState());
        InputState.Change(mouse, new MouseState());
        yield return null;
        Assert.That(game.player.Driving, Is.False);
        Assert.That(game.player.GetComponentsInChildren<ConvoySpriteAvatar>(true).Length, Is.EqualTo(1));
        Assert.That(game.player.avatar.gameObject.activeSelf, Is.True);
        Assert.That(game.player.GetComponent<CharacterController>().enabled, Is.True);
        float offset = game.SteeringOffset;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
        yield return new WaitForSeconds(0.1f);
        Assert.That(game.SteeringOffset, Is.EqualTo(offset));
        Assert.That(game.player.transform.localPosition.z, Is.GreaterThan(localZ + 0.1f));
        InputState.Change(keyboard, new KeyboardState());
        game.spawnInterval = 2f;
        yield return new WaitForSeconds(2.2f);
        Assert.That(game.Pursuers.Count, Is.Zero, "Driver practice must never spawn chasing vans.");
        Assert.That(game.DistanceTravelled, Is.GreaterThan(30f), "Automatic forward travel continues.");
    }

    [UnityTest]
    public IEnumerator AuthoredFloorFallShowsThreeSecondRecoveryRestoresSpawnAndFadesIn()
    {
        game.gameObject.SetActive(false);
        game = authoredGame;
        game.gameObject.SetActive(true);
        game.SetPaused(false);
        Vector3 spawn = game.player.transform.localPosition;
        Quaternion spawnRotation = game.player.transform.localRotation;
        CharacterController controller = game.player.GetComponent<CharacterController>();
        controller.enabled = false;
        game.player.transform.localPosition = new Vector3(-0.4f, 0.14f, -0.75f);
        controller.enabled = true;
        float fallDeadline = Time.time + 2f;
        while (!game.player.IsRespawning && Time.time < fallDeadline)
            yield return null;
        Assert.That(game.player.IsRespawning, Is.True, "Falling through the actual floor opening must trigger recovery.");
        Assert.That(game.player.RespawnRemaining, Is.EqualTo(3f).Within(0.1f));
        Assert.That(game.player.RespawnFadeAlpha, Is.EqualTo(1f));
        Assert.That(controller.enabled, Is.False);
        Vector3 enemyPosition = game.Pursuers[0].transform.localPosition;
        int health = game.Health;
        float started = Time.time;
        yield return new WaitForSeconds(2.85f);
        Assert.That(game.player.IsRespawning, Is.True, "Do not restore the player before three seconds.");
        Assert.That(game.Health, Is.EqualTo(health));
        Assert.That(game.Pursuers[0].transform.localPosition, Is.EqualTo(enemyPosition));
        while (game.player.IsRespawning && Time.time - started < 3.3f)
            yield return null;
        Assert.That(game.player.IsRespawning, Is.False);
        Assert.That(Time.time - started, Is.InRange(2.95f, 3.3f));
        Vector3 restored = game.player.transform.localPosition;
        Assert.That(new Vector2(restored.x - spawn.x, restored.z - spawn.z).magnitude, Is.LessThan(0.01f));
        Assert.That(Quaternion.Angle(game.player.transform.localRotation, spawnRotation), Is.LessThan(0.1f));
        Assert.That(controller.enabled, Is.True);
        Assert.That(game.player.RespawnFadeAlpha, Is.GreaterThan(0.8f));
        Assert.That(game.player.ImpactVelocity, Is.EqualTo(Vector3.zero));
        yield return new WaitForSeconds(0.65f);
        Assert.That(game.player.RespawnFadeAlpha, Is.Zero);
        Assert.That(game.player.transform.localPosition.y, Is.GreaterThan(-0.1f));
        AssertNoSolidPenetration(controller);
    }

    [UnityTest]
    public IEnumerator AuthoredSpawnAllowsFourDirectionMovementAndFullCameraOrbit()
    {
        game.gameObject.SetActive(false);
        game = authoredGame;
        game.gameObject.SetActive(true);
        game.SetPaused(false);
        foreach (PursuerVan enemy in game.Pursuers)
            enemy.enabled = false;
        CharacterController controller = game.player.GetComponent<CharacterController>();
        Vector3 spawn = game.player.transform.localPosition;
        yield return new WaitForSeconds(0.15f);
        foreach (Key key in new[] { Key.W, Key.S, Key.A, Key.D })
        {
            controller.enabled = false;
            game.player.transform.localPosition = spawn;
            controller.enabled = true;
            InputState.Change(keyboard, new KeyboardState());
            yield return null;
            Vector3 start = game.player.transform.localPosition;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return new WaitForSeconds(0.15f);
            InputState.Change(keyboard, new KeyboardState());
            Vector3 delta = game.player.transform.localPosition - start;
            Assert.That(new Vector2(delta.x, delta.z).magnitude,
                Is.GreaterThan(game.player.moveSpeed * 0.1f), "Blocked at spawn walking " + key + ": " + delta);
            AssertNoSolidPenetration(controller);
        }
        controller.enabled = false;
        game.player.transform.localPosition = spawn;
        controller.enabled = true;
        for (int angle = 0; angle < 12; angle++)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(30f / game.player.mouseSensitivity, 0f) });
            float yaw = game.player.transform.localEulerAngles.y;
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.That(Mathf.DeltaAngle(yaw, game.player.transform.localEulerAngles.y), Is.EqualTo(30f).Within(0.2f));
            Vector3 eye = game.player.view.transform.position;
            foreach (Collider collider in Physics.OverlapSphere(eye, 0.03f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                Assert.That(collider.transform.IsChildOf(game.player.transform), Is.True,
                    "Camera overlaps " + collider.name + " at orbit step " + angle);
            Vector3 pivot = game.player.transform.TransformPoint(Vector3.up * ConvoyPlayer.EyeHeight);
            Assert.That(Vector3.Distance(pivot, eye), Is.GreaterThan(0.15f),
                "Camera collapsed at spawn orbit step " + angle);
        }
    }

    [UnityTest]
    public IEnumerator IntactModelPartitionBlocksCameraFromBothSides()
    {
        game.gameObject.SetActive(false);
        game = authoredGame;
        game.gameObject.SetActive(true);
        game.SetPaused(true);
        game.player.GetComponent<CharacterController>().enabled = false;
        Collider wall = game.mainVanModel.GetComponentInChildren<MeshCollider>();
        Assert.That(wall, Is.Not.Null);
        foreach (float x in new[] { -1f, 0f, 1f })
        {
            foreach (int side in new[] { -1, 1 })
            {
                game.player.transform.localPosition = new Vector3(x, 2.3f, 1.11f + side * 0.45f);
                InputSystem.QueueStateEvent(mouse, new MouseState
                {
                    delta = new Vector2(Mathf.DeltaAngle(game.player.transform.localEulerAngles.y,
                        side < 0 ? 180f : 0f) / game.player.mouseSensitivity, 0f)
                });
                game.SetPaused(false);
                game.player.GetComponent<CharacterController>().enabled = true;
                yield return new WaitForEndOfFrame();
                game.SetPaused(true);
                game.player.GetComponent<CharacterController>().enabled = false;
                Vector3 pivot = game.player.transform.TransformPoint(Vector3.up * ConvoyPlayer.EyeHeight);
                Vector3 eye = game.player.view.transform.position;
                Assert.That(Vector3.Distance(pivot, eye), Is.LessThan(ConvoyPlayer.CameraDistance));
                Assert.That(Physics.OverlapSphere(eye, 0.03f, Physics.AllLayers,
                    QueryTriggerInteraction.Ignore), Has.No.Member(wall),
                    "x=" + x + "; side=" + side + "; eye=" + game.van.InverseTransformPoint(eye) +
                    "; pivot=" + game.van.InverseTransformPoint(pivot));
                Assert.That((game.van.InverseTransformPoint(eye).z - 1.11f) * side, Is.GreaterThan(0f),
                    "Camera crossed the intact model partition.");
            }
        }
    }

    [UnityTest]
    public IEnumerator LaserHitSlidesBackwardWithoutShakeAndCleansUpSparks()
    {
        game.player.enabled = false;
        game.player.transform.localPosition = new Vector3(0f, 0.6f, -1.35f);
        game.player.Mount();
        PursuerVan enemy = game.Pursuers[0];
        enemy.enabled = false;
        enemy.transform.position = new Vector3(0f, 0f, -25f);
        game.gunCamera.transform.LookAt(enemy.transform.position + Vector3.up * 1.8f);
        float initialZ = enemy.transform.position.z;
        Assert.That(game.TryFire(), Is.True);
        Assert.That(enemy.transform.position.z, Is.EqualTo(initialZ));
        Assert.That(game.transform.Find("Rail impact flash"), Is.Not.Null);
        Assert.That(game.transform.Find("Rail impact spark"), Is.Not.Null);
        enemy.enabled = true;
        yield return new WaitForSeconds(0.1f);
        Assert.That(enemy.transform.position.z, Is.LessThan(initialZ - 0.15f));
        Assert.That(enemy.KnockbackSpeed, Is.GreaterThan(0f));
        Assert.That(enemy.visualRoot.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(Quaternion.Angle(enemy.visualRoot.localRotation, Quaternion.identity), Is.LessThan(0.01f));
        yield return new WaitForSeconds(1.3f);
        Assert.That(enemy.KnockbackSpeed, Is.Zero);
        Assert.That(enemy.visualRoot.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(game.transform.Find("Rail impact flash"), Is.Null);
        Assert.That(game.transform.Find("Rail impact spark"), Is.Null);
    }

    [UnityTest]
    public IEnumerator EPressPicksUpCarriesAndLoadsAmmoWithoutMouseHold()
    {
        foreach (PursuerVan enemy in game.Pursuers)
            enemy.enabled = false;
        CharacterController controller = game.player.GetComponent<CharacterController>();
        controller.enabled = false;
        game.player.transform.position = game.ammoSupply.position + Vector3.back * 0.25f - Vector3.up * 0.05f;
        controller.enabled = true;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
        yield return null;
        yield return null;
        Assert.That(game.SimulationRunning, Is.True);
        Assert.That(mouse.rightButton.isPressed, Is.False);
        Assert.That(Mouse.current, Is.SameAs(mouse));
        ConvoyAmmoPack pack = game.player.CarriedAmmo;
        Assert.That(pack, Is.Not.Null);
        Assert.That(game.supplyPack, Is.Not.SameAs(pack));
        Assert.That(game.supplyPack.gameObject.activeSelf, Is.True);
        Assert.That(game.player.Mounted, Is.False);
        controller.enabled = false;
        game.player.transform.localPosition = new Vector3(0f, 0.8f, 0.3f);
        controller.enabled = true;
        float carryStart = game.player.transform.localPosition.z;
        InputState.Change(keyboard, new KeyboardState(Key.W));
        yield return new WaitForSeconds(0.1f);
        InputState.Change(keyboard, new KeyboardState());
        Assert.That(game.player.transform.localPosition.z, Is.LessThan(carryStart - 0.15f));
        Assert.That(game.player.CarriedAmmo, Is.SameAs(pack));
        Assert.That(game.player.Mounted, Is.False);
        InputSystem.QueueStateEvent(mouse, new MouseState());
        yield return null;
        Assert.That(game.player.CarriedAmmo, Is.SameAs(pack));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
        yield return null;
        Assert.That(game.player.CarriedAmmo, Is.SameAs(pack), "E away from the gun must keep carrying");
        game.SetPaused(true);
        Assert.That(game.player.CarriedAmmo, Is.Null);
        Assert.That(pack.IsCarried, Is.False);
        Assert.That(pack.GetComponent<Collider>().enabled, Is.True);
        Assert.That(game.ShotsAvailable, Is.EqualTo(4));
        game.SetPaused(false);
        InputState.Change(keyboard, new KeyboardState());
        game.player.transform.position = pack.transform.position - Vector3.up * ConvoyPlayer.EyeHeight;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
        yield return null;
        yield return null;
        Assert.That(game.player.CarriedAmmo, Is.SameAs(pack));
        game.player.transform.position = game.ammoReceiver.position - game.player.transform.TransformVector(
            new Vector3(0f, ConvoyAmmoPack.CarryHeight, ConvoyAmmoPack.CarryDistance));
        InputState.Change(keyboard, new KeyboardState());
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
        yield return null;
        yield return null;
        Assert.That(game.player.CarriedAmmo, Is.Null);
        Assert.That(game.ShotsAvailable, Is.EqualTo(8));
    }

    [UnityTest]
    public IEnumerator EBehindRailgunConsumesCarriedBatteryAndAddsExactlyFourShots()
    {
        foreach (ConvoyPrototype target in new[] { game, authoredGame })
        {
            game.gameObject.SetActive(false);
            game = target;
            game.gameObject.SetActive(true);
            game.SetPaused(false);
            foreach (PursuerVan enemy in game.Pursuers)
                enemy.enabled = false;
            CharacterController controller = game.player.GetComponent<CharacterController>();
            foreach (Vector3 position in new[]
            {
                new Vector3(0.75f, 0.12f, -2f),
                new Vector3(-0.25f, 1f, -2f),
                new Vector3(0f, 1f, -1.94f)
            })
            {
                controller.enabled = false;
                game.player.transform.position = game.ammoSupply.position - Vector3.up * ConvoyPlayer.EyeHeight;
                controller.enabled = true;
                InputState.Change(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null;
                yield return null;
                ConvoyAmmoPack pack = game.player.CarriedAmmo;
                Assert.That(pack, Is.Not.Null);
                controller.enabled = false;
                game.player.transform.localPosition = position;
                controller.enabled = true;
                InputState.Change(keyboard, new KeyboardState());
                yield return null;
                int before = game.ShotsAvailable;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null;
                yield return null;
                Vector3 breech = game.gun.TransformPoint(new Vector3(0f, -0.1f, -0.5f));
                string blockers = "";
                if (pack != null)
                    foreach (RaycastHit hit in Physics.RaycastAll(pack.transform.position,
                        (breech - pack.transform.position).normalized, Vector3.Distance(pack.transform.position, breech),
                        Physics.AllLayers, QueryTriggerInteraction.Ignore))
                        blockers += hit.collider.name + "; ";
                Assert.That(game.player.CarriedAmmo, Is.Null, "Reload failed behind gun at " + position +
                    "; blockers=" + blockers + "; player=" + game.player.transform.localPosition +
                    "; pack=" + (pack == null ? "consumed" : pack.transform.position.ToString()) + "; breech=" + breech);
                Assert.That(pack == null || !pack.gameObject.activeSelf, Is.True,
                    "Loaded battery must disappear.");
                Assert.That(game.ShotsAvailable, Is.EqualTo(before + 4));
                yield return null;
                Assert.That(game.ShotsAvailable, Is.EqualTo(before + 4), "Holding E must not reload twice.");
            }
        }
    }

    [UnityTest]
    public IEnumerator AuthoredCabinAllowsPassengerAmmoTripThroughPartitionAndBack()
    {
        game.gameObject.SetActive(false);
        game = authoredGame;
        game.gameObject.SetActive(true);
        game.SetPaused(false);
        game.maxPursuers = 1;
        foreach (PursuerVan enemy in game.Pursuers)
            enemy.enabled = false;
        CharacterController controller = game.player.GetComponent<CharacterController>();
        controller.enabled = false;
        game.player.transform.localPosition = new Vector3(0.09f, 0.76f, 0.57f);
        controller.enabled = true;
        yield return new WaitForSeconds(0.2f);
        yield return WalkCabinRoute(new Vector2(0.48f, 0.65f), true);
        yield return WalkCabinRoute(new Vector2(0f, 1.45f), true, 0.4f);
        yield return WalkCabinRoute(new Vector2(0.9f, 2.5f), true);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
        yield return null;
        yield return null;
        Assert.That(game.player.CarriedAmmo, Is.Not.Null);
        yield return WalkCabinRoute(new Vector2(0f, 1.45f), true);
        yield return WalkCabinRoute(new Vector2(0f, 0.9f), true);
        yield return WalkCabinRoute(new Vector2(0f, -0.5f), false);
        yield return WalkCabinRoute(new Vector2(0.55f, -1.35f), true);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
        yield return null;
        yield return null;
        Assert.That(game.ShotsAvailable, Is.EqualTo(8),
            "Pack=" + (game.player.CarriedAmmo == null ? "none" : game.player.CarriedAmmo.transform.position.ToString()) +
            "; socket=" + game.ammoReceiver.position + "; paused=" + game.Paused);
        Assert.That(game.player.CarriedAmmo, Is.Null);
    }

    private IEnumerator WalkCabinRoute(Vector2 target, bool jump, float jumpDelay = 0.12f)
    {
        float deadline = Time.time + 5f;
        float nextJump = Time.time + 1f;
        if (jump)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return new WaitForSeconds(jumpDelay);
        }
        InputState.Change(keyboard, new KeyboardState(Key.W));
        while (Time.time < deadline)
        {
            if (game.Paused)
                game.SetPaused(false);
            Vector3 position = game.player.transform.localPosition;
            Vector2 difference = target - new Vector2(position.x, position.z);
            if (difference.magnitude < 0.07f)
                break;
            float desiredYaw = Mathf.Atan2(difference.x, difference.y) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(game.player.transform.localEulerAngles.y, desiredYaw);
            MouseState state = new MouseState { delta = new Vector2(delta / game.player.mouseSensitivity, 0f) };
            InputSystem.QueueStateEvent(mouse, state);
            if (jump && Time.time >= nextJump && game.player.GetComponent<CharacterController>().isGrounded)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
                nextJump = Time.time + 1f;
            }
            yield return null;
            InputState.Change(keyboard, new KeyboardState(Key.W));
            AssertNoSolidPenetration(game.player.GetComponent<CharacterController>());
        }
        InputState.Change(keyboard, new KeyboardState());
        Vector3 arrived = game.player.transform.localPosition;
        Assert.That(Vector2.Distance(target, new Vector2(arrived.x, arrived.z)), Is.LessThan(0.12f),
            "Could not reach cabin route waypoint " + target + " from " + arrived +
            "; running=" + game.SimulationRunning + "; W=" + keyboard.wKey.isPressed +
            "; mouse=" + (Mouse.current == mouse) + "; grounded=" +
            game.player.GetComponent<CharacterController>().isGrounded);
        yield return new WaitForSeconds(0.7f);
        game.SetPaused(false);
    }

    [UnityTest]
    public IEnumerator AmmoDisplayExistsOnGunOnlyWhileInOpticAndUpdatesAfterFiring()
    {
        Assert.That(game.gunAmmoDisplay, Is.Not.Null);
        Assert.That(game.gunAmmoDisplay.transform.IsChildOf(game.gun), Is.True);
        yield return new WaitForEndOfFrame();
        Assert.That(game.gunAmmoDisplay.gameObject.activeInHierarchy, Is.False);
        game.player.enabled = false;
        game.player.transform.localPosition = new Vector3(0f, 0.6f, -1.35f);
        Assert.That(game.player.Mount(), Is.True);
        yield return new WaitForEndOfFrame();
        Assert.That(game.gunAmmoDisplay.gameObject.activeInHierarchy, Is.True);
        Assert.That(game.gunAmmoDisplay.text, Is.EqualTo("4"));
        Vector3 screen = game.gunCamera.WorldToViewportPoint(game.gunAmmoDisplay.transform.position);
        Assert.That(screen.x, Is.InRange(0.05f, 0.95f));
        Assert.That(screen.y, Is.InRange(0.05f, 0.95f));
        Assert.That(screen.z, Is.GreaterThan(game.gunCamera.nearClipPlane));
        AssertAmmoDisplayGeometry(game);
        AssertAmmoDisplayGeometry(authoredGame);
        Assert.That(game.TryFire(), Is.True);
        yield return new WaitForEndOfFrame();
        Assert.That(game.gunAmmoDisplay.text, Is.EqualTo("3"));
        game.player.Dismount();
        yield return new WaitForEndOfFrame();
        Assert.That(game.gunAmmoDisplay.gameObject.activeInHierarchy, Is.False);
    }

    private static void AssertAmmoDisplayGeometry(ConvoyPrototype target)
    {
        TextMesh digits = target.gunAmmoDisplay;
        Transform display = digits.transform.parent;
        Transform housing = display.Find("Recessed display housing");
        Transform screen = display.Find("Display screen");
        float housingFront = housing.localPosition.z - housing.localScale.z * 0.5f;
        float screenBack = screen.localPosition.z + screen.localScale.z * 0.5f;
        Assert.That(housingFront - screenBack, Is.GreaterThanOrEqualTo(0.004f),
            "Screen must sit in front of the housing, not share its surface and flicker.");
        Assert.That(digits.transform.localPosition.z,
            Is.LessThan(screen.localPosition.z - screen.localScale.z * 0.5f));
        Assert.That(display.localScale.x, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(digits.characterSize, Is.EqualTo(0.004f).Within(0.0001f));

        if (!digits.gameObject.activeInHierarchy)
            return;
        Bounds bounds = digits.GetComponent<Renderer>().localBounds;
        float bottom = float.PositiveInfinity;
        float top = float.NegativeInfinity;
        foreach (int x in new[] { -1, 1 })
            foreach (int y in new[] { -1, 1 })
                foreach (int z in new[] { -1, 1 })
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                    float height = target.gunCamera.WorldToViewportPoint(digits.transform.TransformPoint(corner)).y;
                    bottom = Mathf.Min(bottom, height);
                    top = Mathf.Max(top, height);
                }
        Assert.That(top - bottom, Is.InRange(0.015f, 0.065f),
            "Digits should remain readable without dominating the aiming view.");
    }

    [UnityTest]
    public IEnumerator CannonConsumesExactlyFourShotsThenRefusesUntilReloaded()
    {
        game.player.enabled = false;
        game.player.transform.localPosition = new Vector3(0f, 0.6f, -1.35f);
        Assert.That(game.player.Mount(), Is.True);
        for (int shot = 0; shot < 4; shot++)
        {
            Assert.That(game.TryFire(), Is.True);
            Assert.That(game.ShotsAvailable, Is.EqualTo(3 - shot));
            Assert.That(game.TryFire(), Is.False);
            yield return new WaitForSeconds(0.85f);
        }
        Assert.That(game.TryFire(), Is.False);
        ConvoyAmmoPack pack = game.supplyPack;
        pack.transform.position = game.ammoReceiver.position;
        Assert.That(game.TryLoadAmmo(pack), Is.True);
        Assert.That(game.ShotsAvailable, Is.EqualTo(4));
        Assert.That(game.TryFire(), Is.True);
        Assert.That(game.ShotsAvailable, Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator HealthDisplayDropsInOnHitResetsAndFadesAfterFiveQuietSeconds()
    {
        game.maxPursuers = 1;
        foreach (PursuerVan enemy in game.Pursuers)
            enemy.enabled = false;
        Assert.That(game.HealthDisplayAlpha, Is.Zero);
        game.RegisterRam(1);
        Assert.That(game.HealthDisplayOffset, Is.EqualTo(-60f));
        yield return new WaitForSeconds(0.25f);
        Assert.That(game.HealthDisplayAlpha, Is.EqualTo(1f));
        Assert.That(game.HealthDisplayOffset, Is.Zero);
        yield return new WaitForSeconds(4.5f);
        Assert.That(game.HealthDisplayAlpha, Is.EqualTo(1f));
        game.RegisterRam(-1);
        yield return new WaitForSeconds(0.4f);
        Assert.That(game.HealthDisplayAlpha, Is.EqualTo(1f));
        yield return new WaitForSeconds(4.4f);
        Assert.That(game.HealthDisplayAlpha, Is.EqualTo(1f));
        yield return new WaitForSeconds(0.5f);
        Assert.That(game.HealthDisplayAlpha, Is.InRange(0.05f, 0.95f));
        yield return new WaitForSeconds(0.4f);
        Assert.That(game.HealthDisplayAlpha, Is.Zero);
        Assert.That(game.Health, Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator FiveRamsStopWorldAndCombatFlashFadesAndRRestarts()
    {
        game.player.transform.localPosition = new Vector3(0f, 0.6f, -1.35f);
        game.player.Mount();
        for (int i = 0; i < ConvoyPrototype.MaximumHealth; i++)
            game.RegisterRam(1);
        Assert.That(game.DamageFlash, Is.EqualTo(1f));
        Assert.That(game.IsDead, Is.True);
        Assert.That(game.Mounted, Is.False);
        Assert.That(game.TryFire(), Is.False);
        Vector3 road = game.road.GetChild(5).position;
        Vector3 enemy = game.Pursuers[0].transform.position;
        float distance = game.DistanceTravelled;
        int enemyCount = game.Pursuers.Count;
        yield return new WaitForSeconds(0.8f);
        Assert.That(game.DamageFlash, Is.Zero);
        Assert.That(game.DistanceTravelled, Is.EqualTo(distance));
        Assert.That(game.road.GetChild(5).position, Is.EqualTo(road));
        Assert.That(game.Pursuers[0].transform.position, Is.EqualTo(enemy));
        Assert.That(game.Pursuers.Count, Is.EqualTo(enemyCount));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
        float deadline = Time.realtimeSinceStartup + 5f;
        while (game != null && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(game == null, Is.True, "R must reload the scene");
        yield return null;
        game = Object.FindFirstObjectByType<ConvoyPrototype>();
        Assert.That(game, Is.Not.Null);
        Assert.That(game.Health, Is.EqualTo(5));
        Assert.That(game.IsDead, Is.False);
        game.SetPaused(false);
        Assert.That(game.SimulationRunning, Is.True);
    }

    [UnityTest]
    public IEnumerator MouseLookRespondsAtFasterSensitivityWithoutLingeringMotion()
    {
        float initialYaw = game.player.transform.localEulerAngles.y;
        InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100f, 0f) });
        yield return null;
        yield return new WaitForEndOfFrame();
        Assert.That(Mathf.DeltaAngle(initialYaw, game.player.transform.localEulerAngles.y),
            Is.EqualTo(20f).Within(0.1f));
        float yaw = game.player.transform.localEulerAngles.y;
        yield return null;
        yield return new WaitForEndOfFrame();
        Assert.That(Mathf.DeltaAngle(yaw, game.player.transform.localEulerAngles.y), Is.EqualTo(0f).Within(0.1f));

        game.player.transform.localPosition = new Vector3(0f, 0.6f, -1.35f);
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
        yield return null;
        yield return new WaitForEndOfFrame();
        Assert.That(game.Mounted, Is.True);
        float gunYaw = game.gun.localEulerAngles.y;
        InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100f, 0f) }.WithButton(MouseButton.Right));
        yield return null;
        yield return new WaitForEndOfFrame();
        Assert.That(Mathf.DeltaAngle(gunYaw, game.gun.localEulerAngles.y), Is.EqualTo(16f).Within(0.1f));
    }

    [UnityTest]
    public IEnumerator WorldScrollsAndEnemiesDeliverRealUpdateRams()
    {
        float startDistance = game.DistanceTravelled;
        Vector3 position = game.road.GetChild(5).localPosition;
        yield return new WaitForSeconds(1f);
        Assert.That(game.DistanceTravelled - startDistance, Is.GreaterThan(15f));
        Assert.That(game.road.GetChild(5).localPosition.z, Is.LessThan(position.z - 15f));
        yield return new WaitForSeconds(17f);
        Assert.That(game.RamCount, Is.EqualTo(1), "Easier chase should deliver only the first van's attack.");
        Assert.That(game.van.localPosition.z, Is.EqualTo(0f));
        Assert.That(Mathf.Abs(game.van.localPosition.x), Is.LessThan(1f));
        Assert.That(game.player.transform.localPosition.y, Is.GreaterThan(0f),
            "Player must remain on the original model floor, not the removed support-floor height.");
        AssertNoSolidPenetration(game.player.GetComponent<CharacterController>());
        Assert.That(game.Pursuers.Count, Is.LessThanOrEqualTo(game.maxPursuers));
    }

    [UnityTest]
    public IEnumerator KeyboardMovementAndHoldReleaseMouseMountWorkInPlayMode()
    {
        CharacterController controller = game.player.GetComponent<CharacterController>();
        controller.enabled = false;
        game.player.transform.localPosition = new Vector3(-1.1f, 0.14f, -0.7f);
        controller.enabled = true;
        float start = game.player.transform.localPosition.z;
        InputState.Change(keyboard, new KeyboardState(Key.W));
        yield return new WaitForSeconds(0.7f);
        InputState.Change(keyboard, new KeyboardState());
        Assert.That(game.player.transform.localPosition.z, Is.LessThan(start - 0.8f));
        controller.enabled = false;
        game.player.transform.localPosition = new Vector3(-0.8f, 0.14f, -1.6f);
        controller.enabled = true;
        Assert.That(game.player.CanMount, Is.True);
        InputState.Change(mouse, new MouseState().WithButton(MouseButton.Right));
        yield return null;
        yield return null;
        Assert.That(game.Mounted, Is.True);
        Assert.That(game.gunCamera.enabled, Is.True);
        InputState.Change(mouse, new MouseState());
        yield return null;
        yield return null;
        Assert.That(game.Mounted, Is.False);
        Assert.That(game.player.GetComponent<CharacterController>().enabled, Is.True);
        InputState.Change(keyboard, new KeyboardState(Key.W));
        yield return new WaitForSeconds(2f);
        InputState.Change(keyboard, new KeyboardState());
        Assert.That(game.player.transform.localPosition.z, Is.GreaterThanOrEqualTo(-3.11f));
        Assert.That(game.player.transform.localPosition.y, Is.InRange(0f, 0.7f));
    }

    [UnityTest]
    public IEnumerator TwoAimedShotsDestroyActualChaserAcrossRecharge()
    {
        game.player.enabled = false;
        game.player.transform.localPosition = new Vector3(0f, 0.6f, -1.35f);
        Assert.That(game.player.Mount(), Is.True);
        PursuerVan enemy = game.Pursuers[0];
        enemy.enabled = false;
        enemy.transform.position = new Vector3(0f, 0f, -25f);
        game.gunCamera.transform.LookAt(enemy.transform.position + Vector3.up * 1.8f);
        Physics.SyncTransforms();
        int previousScore = game.DestroyedVans;
        Assert.That(game.TryFire(), Is.True);
        Assert.That(enemy.Hits, Is.EqualTo(1));
        Assert.That(game.TryFire(), Is.False);
        yield return new WaitForSeconds(0.85f);
        game.gunCamera.transform.LookAt(enemy.transform.position + Vector3.up * 1.8f);
        Assert.That(game.TryFire(), Is.True);
        yield return null;
        Assert.That(game.DestroyedVans, Is.EqualTo(previousScore + 1));
        Assert.That(enemy == null, Is.True);
    }

    [UnityTest]
    public IEnumerator TinyPlayerJumpsOntoStoolAndImpactDisplacesWalkingPlayer()
    {
        CharacterController controller = game.player.GetComponent<CharacterController>();
        controller.enabled = false;
        game.player.transform.localPosition = new Vector3(0f, 0.16f, -0.45f);
        controller.enabled = true;
        yield return new WaitForSeconds(0.15f);
        InputState.Change(keyboard, new KeyboardState(Key.W));
        yield return new WaitForSeconds(0.25f / game.player.moveSpeed);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
        yield return new WaitForSeconds(0.63f / game.player.moveSpeed);
        InputState.Change(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.65f);
        Assert.That(game.player.transform.localPosition.y, Is.GreaterThan(0.45f));
        Assert.That(game.player.CanMount, Is.True);
        float stoolHeight = game.player.transform.localPosition.y;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return new WaitForSeconds(0.08f);
        Assert.That(game.player.transform.localPosition.y, Is.GreaterThan(stoolHeight + 0.04f));
        InputState.Change(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.5f);
        float x = game.player.transform.localPosition.x;
        game.RegisterRam(1);
        yield return new WaitForSeconds(0.15f);
        Assert.That(game.player.transform.localPosition.x, Is.LessThan(x - 0.1f));
        Assert.That(game.player.transform.localPosition.x, Is.GreaterThanOrEqualTo(-1.43f));
    }

    [UnityTest]
    public IEnumerator BothSideImpactsDisplacePlayerAwayFromAttackingVan()
    {
        CharacterController controller = game.player.GetComponent<CharacterController>();
        foreach (int side in new[] { -1, 1 })
        {
            controller.enabled = false;
            game.player.transform.localPosition = new Vector3(0f, 0.16f, -0.5f);
            controller.enabled = true;
            yield return new WaitForSeconds(0.9f);
            float initialX = game.player.transform.localPosition.x;
            game.RegisterRam(side);
            yield return new WaitForSeconds(0.15f);
            float displacement = game.player.transform.localPosition.x - initialX;
            Assert.That(displacement * side, Is.LessThan(-0.1f),
                "The occupant must move away from the attacking side.");
        }
    }

    [UnityTest]
    public IEnumerator PlayerSpritesRespondToWalkingJumpingLandingAndCameraOrbit()
    {
        yield return CheckSpriteAnimation();
    }

    [UnityTest]
    public IEnumerator Player2SpritesRespondToWalkingJumpingLandingAndCameraOrbit()
    {
        game.showRoleSelection = true;
        game.SelectRole(ConvoyPrototype.PlayerRole.Driver);
        Assert.That(game.player.spriteAvatar.idleBack.name, Is.EqualTo("Gleif_Idle_Back"));
        yield return CheckSpriteAnimation();
    }

    private IEnumerator CheckSpriteAnimation()
    {
        foreach (PursuerVan enemy in game.Pursuers)
            enemy.enabled = false;
        ConvoySpriteAvatar avatar = game.player.spriteAvatar;
        Assert.That(authoredGame.player.spriteAvatar, Is.Not.Null);
        CharacterController controller = game.player.GetComponent<CharacterController>();
        game.player.SetSpawn(new Vector3(-0.9f, 0.214f, 3.3f), Quaternion.Euler(0f, 180f, 0f));
        yield return new WaitForSeconds(0.15f);
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.idleBack));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
        yield return null;
        yield return new WaitForEndOfFrame();
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.walk1Back));
        yield return new WaitForSeconds(ConvoySpriteAvatar.WalkFrameDuration);
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.walk2Back));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        yield return new WaitForEndOfFrame();
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.idleBack));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.Space));
        yield return null;
        yield return new WaitForEndOfFrame();
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.jumpFront));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.9f);
        Assert.That(controller.isGrounded, Is.True);
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.idleFront));
        InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100f, 30f) });
        yield return null;
        yield return new WaitForEndOfFrame();
        Vector3 cameraForward = Vector3.ProjectOnPlane(game.player.view.transform.forward, game.van.up).normalized;
        Assert.That(Vector3.Dot(avatar.transform.forward, cameraForward), Is.GreaterThan(0.999f));
        Assert.That(Vector3.Dot(avatar.transform.up, game.van.up), Is.GreaterThan(0.999f));
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(avatar.idleFront));
        Sprite beforePause = avatar.spriteRenderer.sprite;
        game.SetPaused(true);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
        yield return new WaitForSeconds(0.2f);
        Assert.That(avatar.spriteRenderer.sprite, Is.SameAs(beforePause));
    }

    [UnityTest]
    public IEnumerator TinyPlayerCanJumpUpAllThreeCargoBoxHeights()
    {
        CharacterController controller = game.player.GetComponent<CharacterController>();
        Assert.That(controller.height, Is.EqualTo(0.2f).Within(0.001f));
        Assert.That(controller.radius, Is.EqualTo(0.043f).Within(0.001f));
        Assert.That(game.player.avatar, Is.Not.Null);
        Bounds avatarBounds = new Bounds(game.player.avatar.position, Vector3.zero);
        foreach (Renderer part in game.player.avatar.GetComponentsInChildren<Renderer>(true))
            avatarBounds.Encapsulate(part.bounds);
        Assert.That(avatarBounds.size.y, Is.EqualTo(0.2f).Within(0.005f));
        Assert.That(game.player.view.transform.localPosition.z, Is.LessThan(-0.2f));
        Assert.That(game.van.Find("Jumpable cargo boxes").childCount, Is.GreaterThanOrEqualTo(6));
        controller.enabled = false;
        game.player.transform.localPosition = new Vector3(-0.5f, 0.16f, 0.95f);
        controller.enabled = true;
        yield return new WaitForSeconds(0.15f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.Space));
        yield return new WaitForSeconds(0.55f / game.player.moveSpeed);
        InputState.Change(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.65f);
        Assert.That(game.player.transform.localPosition.y, Is.InRange(0.4f, 0.48f));
        for (int step = 1; step <= 2; step++)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
            yield return new WaitForSeconds(0.95f / game.player.moveSpeed);
            InputState.Change(keyboard, new KeyboardState());
            yield return new WaitForSeconds(0.55f);
            Assert.That(game.player.transform.localPosition.y,
                Is.InRange(0.1f + 0.3f * (step + 1), 0.18f + 0.3f * (step + 1)));
        }
        Assert.That(game.player.GetComponent<CharacterController>().isGrounded, Is.True);
    }

    [UnityTest]
    public IEnumerator SavedLayoutDoesNotPenetrateSolidsDuringWalkingAndRams()
    {
        game.gameObject.SetActive(false);
        game = authoredGame;
        game.gameObject.SetActive(true);
        game.SetPaused(false);
        CharacterController controller = game.player.GetComponent<CharacterController>();
        InputState.Change(keyboard, new KeyboardState(Key.W));
        float elapsed = 0f;
        float nextImpact = 0.3f;
        int side = 1;
        while (elapsed < 2f)
        {
            yield return new WaitForEndOfFrame();
            elapsed += Time.deltaTime;
            if (elapsed >= nextImpact)
            {
                game.RegisterRam(side);
                side = -side;
                nextImpact += 0.3f;
            }
            AssertNoSolidPenetration(controller);
        }
        InputState.Change(keyboard, new KeyboardState());
    }

    [UnityTest]
    public IEnumerator ThirdPersonCameraPullsInBeforeWallsAndReturnsAfterLeaving()
    {
        PrototypeGeometry.Part("Camera obstruction test wall", game.van,
            new Vector3(-0.55f, 0.8f, -0.1f), new Vector3(1f, 1.5f, 0.1f), game.metal);
        CharacterController controller = game.player.GetComponent<CharacterController>();
        controller.enabled = false;
        game.player.transform.localPosition = new Vector3(-0.55f, 0.16f, -0.5f);
        controller.enabled = true;
        yield return new WaitForEndOfFrame();
        Vector3 pivot = game.player.transform.TransformPoint(Vector3.up * ConvoyPlayer.EyeHeight);
        float wallDistance = Vector3.Distance(pivot, game.player.view.transform.position);
        Assert.That(wallDistance, Is.LessThan(ConvoyPlayer.CameraDistance - 0.1f));
        foreach (Collider obstacle in game.van.GetComponentsInChildren<Collider>())
        {
            if (obstacle == controller || !obstacle.enabled || obstacle.isTrigger)
                continue;
            Vector3 eye = game.player.view.transform.position;
            Assert.That(Physics.OverlapSphere(eye, 0.03f, Physics.AllLayers,
                QueryTriggerInteraction.Ignore), Has.No.Member(obstacle),
                "Camera clipped into " + obstacle.name);
        }
        controller.enabled = false;
        game.player.transform.localPosition = new Vector3(-0.55f, 0.16f, -0.9f);
        controller.enabled = true;
        yield return new WaitForEndOfFrame();
        pivot = game.player.transform.TransformPoint(Vector3.up * ConvoyPlayer.EyeHeight);
        Assert.That(Vector3.Distance(pivot, game.player.view.transform.position),
            Is.EqualTo(ConvoyPlayer.CameraDistance).Within(0.01f));
        Assert.That(ConvoyPlayer.CameraDistance, Is.EqualTo(0.6f).Within(0.001f));
        Assert.That(game.player.avatar.gameObject.activeSelf, Is.True);
    }

    [UnityTest]
    public IEnumerator SmallerCharacterCanJumpOntoSixTenthsMeterBox()
    {
        GameObject box = PrototypeGeometry.Part("Tall cargo jump test", game.van,
            new Vector3(0f, 0.44f, 0.6f), new Vector3(0.7f, 0.6f, 0.6f), game.metal);
        box.layer = 8;
        CharacterController controller = game.player.GetComponent<CharacterController>();
        controller.enabled = false;
        game.player.transform.localPosition = new Vector3(0f, 0.16f, 1.15f);
        controller.enabled = true;
        yield return new WaitForSeconds(0.15f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return new WaitForSeconds(0.23f);
        InputState.Change(keyboard, new KeyboardState(Key.W));
        yield return new WaitForSeconds(0.55f / game.player.moveSpeed);
        InputState.Change(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.7f);
        Assert.That(game.player.transform.localPosition.y, Is.InRange(0.7f, 0.78f));
        AssertNoSolidPenetration(controller);
    }

    [UnityTest]
    public IEnumerator WalkingIsImmediatelyFasterAndStopsWithoutDrifting()
    {
        game.maxPursuers = 1;
        Vector3 start = game.player.transform.localPosition;
        InputState.Change(keyboard, new KeyboardState(Key.S));
        yield return new WaitForSeconds(0.25f);
        InputState.Change(keyboard, new KeyboardState());
        float distance = game.player.transform.localPosition.z - start.z;
        Assert.That(game.player.moveSpeed, Is.EqualTo(3.2f).Within(0.001f));
        Assert.That(distance, Is.InRange(0.65f, 1.05f));
        Vector3 stopped = game.player.transform.localPosition;
        yield return new WaitForSeconds(0.2f);
        Assert.That(game.player.transform.localPosition.z, Is.EqualTo(stopped.z).Within(0.02f));
    }

    [UnityTest]
    public IEnumerator WalkingAndRamCannotPushPlayerThroughCargoBox()
    {
        CharacterController controller = game.player.GetComponent<CharacterController>();
        BoxCollider cargo = game.van.Find("Jumpable cargo boxes/Left cargo box 1").GetComponent<BoxCollider>();
        float cargoEdge = game.van.InverseTransformPoint(cargo.transform.TransformPoint(
            cargo.center + Vector3.right * cargo.size.x * 0.5f)).x;
        controller.enabled = false;
        game.player.transform.localPosition = new Vector3(0f, 0.16f, 0.95f);
        controller.enabled = true;
        yield return new WaitForSeconds(0.15f);
        InputState.Change(keyboard, new KeyboardState(Key.D));
        for (int i = 0; i < 10; i++)
        {
            game.RegisterRam(1);
            yield return new WaitForSeconds(0.08f);
            Assert.That(game.player.transform.localPosition.x,
                Is.GreaterThan(cargoEdge + controller.radius - controller.skinWidth - 0.005f));
            AssertNoSolidPenetration(controller);
        }
        InputState.Change(keyboard, new KeyboardState());
        Assert.That(game.player.transform.localPosition.y, Is.LessThan(0.25f));
        AssertNoSolidPenetration(controller);
    }

    [UnityTest]
    public IEnumerator SustainedMovementCannotCrossCabinWallsOrGunPedestal()
    {
        CharacterController controller = game.player.GetComponent<CharacterController>();
        controller.enabled = false;
        game.player.transform.localPosition = new Vector3(0f, 0.16f, -3.1f);
        controller.enabled = true;
        yield return new WaitForSeconds(0.15f);
        InputState.Change(keyboard, new KeyboardState(Key.S));
        yield return new WaitForSeconds(0.6f);
        Vector3 relativeToPedestal = game.player.transform.localPosition - new Vector3(0f, 0f, -2.8f);
        Assert.That(new Vector2(relativeToPedestal.x, relativeToPedestal.z).magnitude, Is.GreaterThan(0.19f));
        AssertNoSolidPenetration(controller);
        InputState.Change(keyboard, new KeyboardState(Key.A));
        yield return new WaitForSeconds(0.8f);
        Assert.That(game.player.transform.localPosition.x, Is.InRange(1.3f, 1.44f));
        AssertNoSolidPenetration(controller);
        InputState.Change(keyboard, new KeyboardState(Key.S));
        yield return new WaitForSeconds(1.8f);
        Assert.That(game.player.transform.localPosition.z, Is.LessThanOrEqualTo(5.41f));
        AssertNoSolidPenetration(controller);
        InputState.Change(keyboard, new KeyboardState());
    }

    private void AssertNoSolidPenetration(CharacterController controller, ConvoyPrototype owner = null)
    {
        Physics.SyncTransforms();
        foreach (Collider obstacle in (owner == null ? game : owner).van.GetComponentsInChildren<Collider>())
        {
            if (obstacle == controller || !obstacle.enabled || obstacle.isTrigger)
                continue;
            bool overlaps = Physics.ComputePenetration(controller, controller.transform.position,
                controller.transform.rotation, obstacle, obstacle.transform.position,
                obstacle.transform.rotation, out _, out float distance);
            Assert.That(overlaps && distance > controller.skinWidth + 0.005f, Is.False,
                "Player penetrated " + obstacle.name + " by " + distance
                + " at local position " + controller.transform.localPosition.ToString("F4"));
        }
    }
}
