using Convoy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GnomeGameSetup
{
    public const string LogoPath = "Assets/Game Assets/all_road_s_lead_to_gnome.png";
    public const string LogoResourcePath = "Assets/ConvoyPrototype/Resources/GnomeLogo.png";

    [MenuItem("Tools/Convoy/Apply Gnome Branding And Secure Cargo")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit play mode before securing cargo.");
        if (Application.isBatchMode)
            EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
        var game = Object.FindAnyObjectByType<ConvoyPrototype>();
        if (game == null) throw new System.InvalidOperationException("Open the game scene.");
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(LogoResourcePath) == null &&
            !AssetDatabase.CopyAsset(LogoPath, LogoResourcePath))
            throw new System.InvalidOperationException("Cannot install the supplied game logo.");
        var importer = (TextureImporter)AssetImporter.GetAtPath(LogoResourcePath);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        Transform cargo = game.van.Find("Secured authored cargo");
        if (cargo == null)
        {
            cargo = new GameObject("Secured authored cargo").transform;
            cargo.SetParent(game.van, false);
        }
        foreach (GameObject root in game.gameObject.scene.GetRootGameObjects())
        {
            string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            if (source != "Assets/Game Assets/in_van_assets/CardboardBox1.fbx" &&
                source != "Assets/Game Assets/in_van_assets/GnomeKiller.fbx" &&
                source != "Assets/Game Assets/in_van_assets/trashbag_v001.fbx" &&
                source != "Assets/Game Assets/in_van_assets/beartrap_v001.fbx")
                continue;
            Undo.SetTransformParent(root.transform, cargo, "Secure van cargo");
        }
        foreach (Transform child in cargo.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
        PrototypeGeometry.SecureAuthoredCargo(game);
        PlayerSettings.productName = ConvoyPrototype.GameTitle;
        game.gameObject.name = ConvoyPrototype.GameTitle;
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        EditorSceneManager.SaveScene(game.gameObject.scene);
        AssetDatabase.SaveAssets();
    }
}
