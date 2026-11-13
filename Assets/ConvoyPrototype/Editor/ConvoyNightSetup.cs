using Convoy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ConvoyNightSetup
{
    [MenuItem("Tools/Convoy/Apply Night Atmosphere")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit play mode before applying night atmosphere.");
        if (Application.isBatchMode)
            EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
        ConvoyPrototype game = Object.FindFirstObjectByType<ConvoyPrototype>();
        if (game == null) throw new System.InvalidOperationException("Open the Convoy scene.");
        Configure(game);
        VanModelSetup.ApplyPursuerToScene();
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        EditorSceneManager.SaveScene(game.gameObject.scene);
        AssetDatabase.SaveAssets();
    }

    public static void Configure(ConvoyPrototype game)
    {
        CreateMaterial("ConvoyNightSky", "Convoy/Pixel Night Sky");
        Material lamp = CreateMaterial("ConvoyLamp", "Universal Render Pipeline/Lit");
        lamp.SetColor("_BaseColor", ConvoyNight.WarmLight);
        lamp.SetColor("_EmissionColor", ConvoyNight.WarmLight * 2f);
        lamp.EnableKeyword("_EMISSION");
        lamp.SetFloat("_Smoothness", 0f);
        EditorUtility.SetDirty(lamp);
        SetColor(game.grass, "#576069");
        SetColor(game.foliage, "#443846");
        SetColor(game.orange, "#fbb954");
        SetColor(game.cyan, "#f2ec8b");
        game.cyan.SetColor("_EmissionColor", ConvoyNight.WarmLight * 1.5f);
        game.cyan.EnableKeyword("_EMISSION");
        foreach (Material material in game.natureBatchMaterials)
        {
            material.SetColor("_NightTint", new Color(0.62f, 0.66f, 0.72f));
            EditorUtility.SetDirty(material);
        }
        ConvoyNight.Apply(game);
    }

    private static Material CreateMaterial(string name, string shaderName)
    {
        string path = "Assets/ConvoyPrototype/Resources/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new System.InvalidOperationException("Missing shader: " + shaderName);
        material = new Material(shader) { name = name };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void SetColor(Material material, string hex)
    {
        if (!ColorUtility.TryParseHtmlString(hex, out Color color))
            throw new System.ArgumentException("Invalid palette color: " + hex);
        material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
    }
}
