using System;
using System.Collections;
using System.Collections.Generic;
using Convoy;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

public sealed class ConvoyCoopTests
{
    private readonly List<ConvoyMultiplayer> sessions = new List<ConvoyMultiplayer>();
    private GameObject clone;

    [UnityTest]
    [Explicit("Exercises real two-peer in-room replay and requires internet access.")]
    public IEnumerator ReplayKeepsRoomSwapsRolesResetsEverythingAndSurvivesAuthorityHandoff()
    {
        yield return SceneManager.LoadSceneAsync("ConvoyPrototype");
        yield return null;
        ConvoyPrototype first = Object.FindAnyObjectByType<ConvoyPrototype>();
        clone = Object.Instantiate(first.gameObject);
        clone.transform.position = Vector3.right * 2000f;
        ConvoyPrototype second = clone.GetComponent<ConvoyPrototype>();
        first.player.enabled = second.player.enabled = false;
        second.player.view.GetComponent<AudioListener>().enabled = false;
        yield return null;
        sessions.Add(first.network);
        sessions.Add(second.network);
        string room = Guid.NewGuid().ToString("N").Substring(0, 8);
        first.network.Connect(room, true, ConvoyPrototype.PlayerRole.Gunner);
        yield return Wait(() => first.network.World != null, "Create the original room.");
        second.network.Connect(room, false, ConvoyPrototype.PlayerRole.Driver);
        yield return Wait(() => first.network.MatchReady && second.network.MatchReady, "Both initial roles must join.");
        var firstRunner = first.network.Runner;
        var secondRunner = second.network.Runner;
        for (int round = 1; round <= 3; round++)
        {
            ConvoyPrototype authority = first.WorldAuthority ? first : second;
            authority.AdvanceDifficulty(60f);
            first.Tutorial.Complete(ConvoyTutorial.Lesson.Jump);
            second.Tutorial.Complete(ConvoyTutorial.Lesson.Jump);
            for (int hit = 0; hit < 5; hit++) authority.RegisterRam(hit % 2 == 0 ? 1 : -1);
            yield return Wait(() => first.IsDead && second.IsDead, "Both browsers must see the same run end.");
            second.network.World.SendAction(ConvoyNetworkWorld.Action.Replay);
            yield return Wait(() => first.network.World.ReplayPhase == ConvoyNetworkWorld.ReplayState.ChoosingRole &&
                second.network.World.ReplayPhase == ConvoyNetworkWorld.ReplayState.ChoosingRole, "Replay opens role choice for both.");
            Assert.That(first.network.World.ReplayChooser, Is.EqualTo(secondRunner.LocalPlayer));
            first.network.World.SendAction(ConvoyNetworkWorld.Action.Replay);
            first.network.World.SendAction(ConvoyNetworkWorld.Action.ReplayDriver);
            yield return new WaitForSeconds(0.15f);
            Assert.That(first.network.World.ReplayChooser, Is.EqualTo(secondRunner.LocalPlayer),
                "A late replay click cannot steal the role picker.");
            Assert.That(first.network.MatchReady, Is.False);
            if (round == 2)
            {
                firstRunner.SetMasterClient(secondRunner.LocalPlayer);
                yield return Wait(() => second.WorldAuthority, "Authority can move during role selection.");
            }
            second.network.World.SendAction(round % 2 == 1 ?
                ConvoyNetworkWorld.Action.ReplayGunner : ConvoyNetworkWorld.Action.ReplayDriver);
            yield return Wait(() => first.network.World.Round == round && second.network.World.Round == round &&
                first.network.MatchReady && second.network.MatchReady && !first.Paused && !second.Paused,
                "Both clients must reset and acknowledge before the next round runs.");
            Assert.That(first.network.Runner, Is.SameAs(firstRunner));
            Assert.That(second.network.Runner, Is.SameAs(secondRunner));
            Assert.That(firstRunner.SessionInfo.Name, Is.EqualTo(room.ToUpperInvariant()));
            Assert.That(second.Role, Is.EqualTo(round % 2 == 1 ? ConvoyPrototype.PlayerRole.Gunner : ConvoyPrototype.PlayerRole.Driver));
            Assert.That(first.Role, Is.Not.EqualTo(second.Role));
            Assert.That(first.otherPlayer.Role, Is.EqualTo(second.Role));
            Assert.That(second.otherPlayer.Role, Is.EqualTo(first.Role));
            Assert.That(first.Health, Is.EqualTo(5));
            Assert.That(second.Health, Is.EqualTo(5));
            Assert.That(first.ShotsAvailable, Is.EqualTo(4));
            Assert.That(second.ShotsAvailable, Is.EqualTo(4));
            Assert.That(first.DestroyedVans, Is.Zero);
            Assert.That(first.DifficultyStage, Is.Zero);
            Assert.That(first.SurvivalTime, Is.LessThan(2f));
            Assert.That(second.accelerator.RockOnPedal, Is.True);
            Assert.That(second.accelerator.Impacts, Is.Zero);
            Assert.That(second.CurrentSpeed, Is.EqualTo(23f));
            Assert.That(first.player.CarriedAmmo, Is.Null);
            Assert.That(first.player.CarryingRock, Is.False);
            Assert.That(first.player.IsRespawning, Is.False);
            Assert.That(first.Tutorial.Knows(ConvoyTutorial.Lesson.Jump), Is.False);
            Assert.That(second.Tutorial.Knows(ConvoyTutorial.Lesson.Jump), Is.False);
            yield return Wait(() => first.Pursuers.Count == 1 && second.Pursuers.Count == 1 &&
                first.roadRocks.Rocks.Count == 1 && second.roadRocks.Rocks.Count == 1 &&
                first.supplyPack != null && second.supplyPack != null, "Fresh enemies, hazards, and one supply replicate once.");
        }
    }

    [UnityTest]
    [Explicit("Exercises real two-peer Photon gameplay and requires internet access.")]
    public IEnumerator TwoPeersShareRolesSteeringAmmoDamageRockCombatAndAuthorityHandoff()
    {
        yield return SceneManager.LoadSceneAsync("ConvoyPrototype");
        yield return null;
        ConvoyPrototype first = Object.FindAnyObjectByType<ConvoyPrototype>();
        clone = Object.Instantiate(first.gameObject);
        clone.transform.position = Vector3.right * 2000f;
        ConvoyPrototype second = clone.GetComponent<ConvoyPrototype>();
        first.player.enabled = false;
        second.player.enabled = false;
        second.player.view.GetComponent<AudioListener>().enabled = false;
        yield return null;
        sessions.Add(first.network);
        sessions.Add(second.network);
        string code = Guid.NewGuid().ToString("N").Substring(0, 8);
        first.network.Connect(code, true, ConvoyPrototype.PlayerRole.Gunner);
        yield return Wait(() => first.network.World != null, "The first peer must create the shared world.");
        second.network.Connect(code, false, ConvoyPrototype.PlayerRole.Driver);
        yield return Wait(() => first.network.MatchReady && second.network.MatchReady &&
            first.otherPlayer != null && second.otherPlayer != null, "Both peers must receive exclusive roles.");
        Assert.That(first.Role, Is.EqualTo(ConvoyPrototype.PlayerRole.Gunner));
        Assert.That(second.Role, Is.EqualTo(ConvoyPrototype.PlayerRole.Driver));
        Assert.That(first.network.World.Gunner, Is.EqualTo(first.network.Runner.LocalPlayer));
        Assert.That(first.network.World.Driver, Is.EqualTo(second.network.Runner.LocalPlayer));
        yield return Wait(() => first.Pursuers.Count > 0 && second.Pursuers.Count > 0 &&
            first.roadRocks.Rocks.Count > 0 && second.roadRocks.Rocks.Count > 0,
            "Chasers and road hazards must exist in the same combined match.");

        Assert.That(second.player.MountDriver(), Is.True);
        second.network.SteeringInput = 1f;
        yield return Wait(() => first.SteeringOffset > 4.8f && second.SteeringOffset > 4.8f,
            "The driver's input must steer the van seen by the gunner.");
        yield return Wait(() => second.Tutorial.Knows(ConvoyTutorial.Lesson.Steer),
            "Only confirmed remote steering should teach the driver.");
        Assert.That(first.Tutorial.Knows(ConvoyTutorial.Lesson.Steer), Is.False);
        second.player.DismountDriver();
        second.network.SteeringInput = 0f;
        float distance = first.DistanceTravelled;
        first.SetPaused(true);
        yield return new WaitForSeconds(0.2f);
        Assert.That(first.DistanceTravelled, Is.GreaterThan(distance), "A local pause must not pause the shared world.");
        first.SetPaused(false);

        first.player.SetSpawn(new Vector3(0.6f, 0.82f, 2.5f), Quaternion.identity);
        second.player.SetSpawn(new Vector3(0.6f, 0.82f, 2.5f), Quaternion.identity);
        int beforePickup = first.network.World.EventSequence;
        first.network.World.SendAction(ConvoyNetworkWorld.Action.Interact);
        second.network.World.SendAction(ConvoyNetworkWorld.Action.Interact);
        yield return Wait(() => first.player.CarriedAmmo != null && second.player.CarriedAmmo != null &&
            first.otherPlayer.CarriedAmmo != null, "Both players must receive distinct shared batteries.");
        Assert.That(first.player.CarriedAmmo, Is.Not.SameAs(first.otherPlayer.CarriedAmmo));
        yield return Wait(() => first.Tutorial.Knows(ConvoyTutorial.Lesson.BatteryPickup) &&
            second.Tutorial.Knows(ConvoyTutorial.Lesson.BatteryPickup),
            "Each player should learn from their own confirmed battery pickup.");
        yield return Wait(() => CountSound(second.network.World, beforePickup, ConvoyPrototype.Sound.Pickup) == 2,
            "Both confirmed pickups replicate exactly one sound each to the remote browser.");
        first.player.SetSpawn(new Vector3(0.8f, 0.35f, -1.35f), Quaternion.identity);
        second.player.SetSpawn(new Vector3(0.8f, 0.35f, -1.35f), Quaternion.identity);
        int beforeLoad = first.network.World.EventSequence;
        first.network.World.SendAction(ConvoyNetworkWorld.Action.Interact);
        second.network.World.SendAction(ConvoyNetworkWorld.Action.Interact);
        yield return Wait(() => first.ShotsAvailable == 12 && second.ShotsAvailable == 12 &&
            first.player.CarriedAmmo == null && second.player.CarriedAmmo == null,
            "Either role must reload the same authoritative gun without duplicating ammo.");
        yield return Wait(() => first.Tutorial.Knows(ConvoyTutorial.Lesson.BatteryLoad) &&
            second.Tutorial.Knows(ConvoyTutorial.Lesson.BatteryLoad),
            "Both players independently completed the reload route.");
        yield return Wait(() => CountSound(second.network.World, beforeLoad, ConvoyPrototype.Sound.Load) == 2,
            "Both successful insertions replicate exactly one insert sound each.");
        Assert.That(first.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(3));
        Assert.That(second.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(3),
            "A cloned peer must reuse its audio sources rather than double the loops.");

        first.RegisterRam(1);
        first.RegisterRam(-1);
        first.RegisterRam(1);
        first.accelerator.Step(15f);
        yield return Wait(() => second.Health == 2 && second.accelerator.NeedsRock &&
            second.CurrentSpeed < 0.001f, "Shared damage must dislodge the same accelerator rock and stop both worlds.");
        second.player.SetSpawn(new Vector3(-0.9f, 0.214f, 2.95f), Quaternion.identity);
        second.network.World.SendAction(ConvoyNetworkWorld.Action.Interact);
        yield return Wait(() => second.player.CarryingRock && first.otherPlayer.CarryingRock,
            "The remote driver must acquire the single shared rock.");
        first.player.SetSpawn(new Vector3(-0.9f, 0.214f, 2.95f), Quaternion.identity);
        first.network.World.SendAction(ConvoyNetworkWorld.Action.Interact);
        yield return new WaitForSeconds(0.15f);
        Assert.That(first.player.CarryingRock, Is.False, "A carried rock cannot be acquired by the other player.");
        second.player.SetSpawn(new Vector3(-0.9f, 0.214f, 3.1f), Quaternion.identity);
        second.network.World.SendAction(ConvoyNetworkWorld.Action.Interact);
        yield return Wait(() => !first.accelerator.NeedsRock && !second.accelerator.NeedsRock,
            "The driver must place the shared rock back on the pedal.");
        yield return Wait(() => second.Tutorial.Knows(ConvoyTutorial.Lesson.RockPickup | ConvoyTutorial.Lesson.RockPlace),
            "Confirmed repair steps must teach the remote player who performed them.");
        Assert.That(first.Tutorial.Knows(ConvoyTutorial.Lesson.RockPickup), Is.False);
        Assert.That(first.Tutorial.Knows(ConvoyTutorial.Lesson.RockPlace), Is.False);
        first.accelerator.Step(5f);
        yield return Wait(() => Mathf.Abs(second.CurrentSpeed - second.roadSpeed) < 0.001f,
            "Both worlds must recover normal speed.");

        first.player.SetSpawn(new Vector3(0.9f, 0.82f, -1.35f), Quaternion.identity);
        Assert.That(first.player.Mount(), Is.True);
        PursuerVan target = first.Pursuers[0];
        Aim(first, target);
        first.network.World.SendAction(ConvoyNetworkWorld.Action.Fire);
        yield return Wait(() => target.Hits == 1 && second.Pursuers[0].Hits == 1,
            "The first railgun hit must damage the same enemy on both peers.");
        yield return new WaitForSeconds(0.85f);
        Aim(first, target);
        first.network.World.SendAction(ConvoyNetworkWorld.Action.Fire);
        yield return Wait(() => first.DestroyedVans == 1 && second.DestroyedVans == 1 &&
            first.ShotsAvailable == 10 && second.ShotsAvailable == 10,
            "Two authoritative shots must destroy the enemy and consume the same ammo.");
        yield return Wait(() => first.Tutorial.Knows(ConvoyTutorial.Lesson.GunFire),
            "Confirmed firing must complete only the gunner's lesson.");
        Assert.That(second.Tutorial.Knows(ConvoyTutorial.Lesson.GunFire), Is.False);

        first.network.Runner.SetMasterClient(second.network.Runner.LocalPlayer);
        yield return Wait(() => !first.network.World.HasStateAuthority && second.network.World.HasStateAuthority,
            "World authority must hand off to the other peer when needed.");
        yield return new WaitForSeconds(0.3f);
        Assert.That(second.Health, Is.EqualTo(2));
        Assert.That(second.ShotsAvailable, Is.EqualTo(10));
        Assert.That(second.accelerator.NeedsRock, Is.False);
        Assert.That(second.Tutorial.Knows(ConvoyTutorial.Lesson.BatteryLoad | ConvoyTutorial.Lesson.RockPlace), Is.True);
        Assert.That(first.Tutorial.Knows(ConvoyTutorial.Lesson.RockPlace), Is.False);
        Assert.That(first.Pursuers.Count, Is.EqualTo(second.Pursuers.Count), "Authority handoff must not duplicate entities.");
        second.AdvanceDifficulty(ConvoyPrototype.DifficultyInterval);
        yield return Wait(() => first.DifficultyStage == 1 && second.DifficultyStage == 1,
            "Difficulty elapsed time must replicate after authority handoff.");
        Assert.That(first.CruiseSpeed, Is.EqualTo(second.CruiseSpeed).Within(0.001f));
        Assert.That(second.CurrentPursuerLimit, Is.EqualTo(3));
        float sharedTime = first.SurvivalTime;
        first.SetPaused(true);
        first.AdvanceDifficulty(600f);
        yield return new WaitForSeconds(0.2f);
        Assert.That(first.SurvivalTime, Is.GreaterThan(sharedTime));
        Assert.That(first.DifficultyStage, Is.EqualTo(1), "A replica must not advance the shared clock itself.");
        first.SetPaused(false);
        second.roadRocks.SpawnBush(-1, 120f);
        yield return Wait(() => first.roadRocks.Rocks.Exists(rock => first.roadRocks.IsBush(rock)),
            "Road bushes must replicate with their own sprite, not turn into rocks.");
    }

    private static void Aim(ConvoyPrototype game, PursuerVan target)
    {
        Vector3 direction = target.GetComponentInChildren<Collider>().bounds.center - game.gunCamera.transform.position;
        direction = game.van.InverseTransformDirection(direction.normalized);
        game.SetGunAim(Mathf.DeltaAngle(180f, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg),
            -Mathf.Asin(direction.y) * Mathf.Rad2Deg);
    }

    private static IEnumerator Wait(Func<bool> condition, string message)
    {
        float deadline = Time.realtimeSinceStartup + 25f;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (!condition())
        {
            foreach (ConvoyPrototype game in Object.FindObjectsByType<ConvoyPrototype>(FindObjectsSortMode.None))
            {
                Debug.Log("Co-op diagnostic: " + message + " role=" + game.Role +
                    " health=" + game.Health + " ammo=" + game.ShotsAvailable +
                    " position=" + game.player.transform.localPosition +
                    " carrying=" + (game.player.CarriedAmmo != null) +
                    " remoteCarrying=" + (game.otherPlayer != null && game.otherPlayer.CarriedAmmo != null));
                foreach (ConvoyAmmoPack pack in game.van.GetComponentsInChildren<ConvoyAmmoPack>())
                    Debug.Log("Pack: " + pack.transform.position + " carried=" + pack.IsCarried);
            }
        }
        Assert.That(condition(), Is.True, message);
    }

    private static int CountSound(ConvoyNetworkWorld world, int after, ConvoyPrototype.Sound sound)
    {
        int count = 0;
        for (int sequence = after + 1; sequence <= world.EventSequence; sequence++)
        {
            ConvoyEvent item = world.Events[sequence % ConvoyNetworkWorld.EventCapacity];
            if (item.Sequence == sequence && item.Kind == 6 && item.Value == (int)sound)
                count++;
        }
        return count;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (ConvoyMultiplayer session in sessions)
        {
            if (session != null && session.Runner != null)
            {
                var task = session.Runner.Shutdown();
                while (!task.IsCompleted) yield return null;
                task.GetAwaiter().GetResult();
            }
        }
        sessions.Clear();
        if (clone != null) Object.Destroy(clone);
        yield return null;
        SceneManager.LoadScene("ConvoyPrototype");
        yield return null;
    }
}
