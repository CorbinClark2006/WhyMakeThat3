using System.IO;
using System.Collections.Generic;
using Convoy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PrototypeSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/ConvoyPrototype.unity";
    private const string MaterialFolder = "Assets/ConvoyPrototype/Materials";

    [MenuItem("Tools/Convoy/Create Prototype Scene")]
    public static void CreateScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Exit play mode before building the Convoy prototype scene.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Directory.CreateDirectory(MaterialFolder);
        AssetDatabase.Refresh();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ConvoyPrototype game = new GameObject("DUST - Rearguard Prototype").AddComponent<ConvoyPrototype>();
        game.metal = Material("Van steel", Color.gray);
        game.dark = Material("Charcoal", new Color(0.19f, 0.19f, 0.19f));
        game.dirt = Material("Dust road", new Color(0.42f, 0.42f, 0.42f));
        game.grass = Material("Grass", new Color(0.58f, 0.58f, 0.58f));
        game.foliage = Material("Scrub canopy", new Color(0.35f, 0.35f, 0.35f));
        game.orange = Material("Hazard orange", new Color(0.32f, 0.32f, 0.32f));
        game.cyan = Material("Rail energy", new Color(0.85f, 0.85f, 0.85f), emission: true);
        game.glass = Material("Tinted glass", new Color(0.25f, 0.25f, 0.25f));
        game.rubber = Material("Rubber", new Color(0.08f, 0.08f, 0.08f));
        game.playerAvatarPrefab = PlayerSpriteSetup.CreateAvatarPrefab();
        game.player2AvatarPrefab = PlayerSpriteSetup.CreatePlayer2AvatarPrefab();
        NatureSpriteSetup.AssignSprites(game);
        VanModelSetup.AssignPrefabs(game);
        BrowserMultiplayerSetup.AssignSounds(game);
        PrototypeGeometry.Build(game);
        NatureSpriteSetup.SaveBatchMeshes(game);

        Light sun = new GameObject("Afternoon sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 2.2f;
        sun.color = Color.white;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        Light cabin = new GameObject("Cabin fill").AddComponent<Light>();
        cabin.transform.SetParent(game.van, false);
        cabin.transform.localPosition = new Vector3(0f, 1.9f, 0f);
        cabin.type = LightType.Point;
        cabin.color = Color.white;
        cabin.intensity = 3f;
        cabin.range = 7f;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.58f, 0.58f, 0.58f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.65f, 0.65f, 0.65f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 75f;
        RenderSettings.fogEndDistance = 175f;
        ConvoyNightSetup.Configure(game);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            if (existing.path != ScenePath)
                scenes.Add(existing);
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = game.gameObject;
        SceneView.lastActiveSceneView?.LookAt(new Vector3(0f, 2f, -1f), Quaternion.Euler(18f, 22f, 0f), 12f);
        Debug.Log("Convoy prototype created. Open the Game view and press Play.");
    }

    private static Material Material(string name, Color color, float metallic = 0f, bool emission = false)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new System.InvalidOperationException("The prototype requires the installed Universal Render Pipeline/Lit shader.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", metallic > 0f ? 0.45f : 0.12f);
        if (emission)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2f);
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(material);
        return material;
    }
}
