using System;
using Convoy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerSpriteSetup
{
    public const string PrefabPath = "Assets/ConvoyPrototype/Player1SpriteAvatar.prefab";
    public const string Player2PrefabPath = "Assets/ConvoyPrototype/Player2SpriteAvatar.prefab";
    private const string MaterialPath = "Assets/ConvoyPrototype/Materials/Player sprite.mat";

    public static ConvoySpriteAvatar CreateAvatarPrefab()
    {
        return CreateAvatarPrefab("Assets/main_characters/player1/", "Beaddy", PrefabPath, "Player 1 sprite avatar");
    }

    public static ConvoySpriteAvatar CreatePlayer2AvatarPrefab()
    {
        return CreateAvatarPrefab("Assets/main_characters/player2/", "Gleif", Player2PrefabPath, "Player 2 sprite avatar");
    }

    private static ConvoySpriteAvatar CreateAvatarPrefab(string folder, string prefix, string prefabPath, string name)
    {
        Sprite idleFront = Import(folder, prefix + "_Idle_Front");
        Sprite idleBack = Import(folder, prefix + "_Idle_Back");
        Sprite walk1Front = Import(folder, prefix + "_Walk1_Front");
        Sprite walk2Front = Import(folder, prefix + "_Walk2_Front");
        Sprite walk1Back = Import(folder, prefix + "_Walk1_Back");
        Sprite walk2Back = Import(folder, prefix + "_Walk2_Back");
        Sprite jumpFront = Import(folder, prefix + "_Jump_Front");
        Sprite jumpBack = Import(folder, prefix + "_Jump_Back");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                throw new InvalidOperationException("The player avatar requires the URP unlit sprite shader.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        GameObject root = new GameObject(name);
        try
        {
            root.layer = 8;
            ConvoySpriteAvatar avatar = root.AddComponent<ConvoySpriteAvatar>();
            avatar.spriteRenderer = root.GetComponent<SpriteRenderer>();
            avatar.spriteRenderer.sharedMaterial = material;
            avatar.spriteRenderer.sprite = idleBack;
            avatar.spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            avatar.spriteRenderer.receiveShadows = false;
            avatar.idleFront = idleFront;
            avatar.idleBack = idleBack;
            avatar.walk1Front = walk1Front;
            avatar.walk2Front = walk2Front;
            avatar.walk1Back = walk1Back;
            avatar.walk2Back = walk2Back;
            avatar.jumpFront = jumpFront;
            avatar.jumpBack = jumpBack;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return prefab.GetComponent<ConvoySpriteAvatar>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    public static Sprite Import(string folder, string name, float pixelsPerUnit = 160f)
    {
        string path = folder + name + ".png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("Missing player sprite: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        settings.spritePivot = new Vector2(0.5f, 0f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new InvalidOperationException("Could not import player sprite: " + path);
        return sprite;
    }

    [MenuItem("Tools/Convoy/Apply Player 1 Sprites")]
    public static void ApplyToScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit play mode before replacing the player avatar.");
        ConvoyPrototype game = UnityEngine.Object.FindFirstObjectByType<ConvoyPrototype>();
        if (game == null || game.player == null)
            throw new InvalidOperationException("Open the Convoy prototype scene before applying player sprites.");
        ConvoySpriteAvatar prefab = CreateAvatarPrefab();
        Undo.RecordObjects(new UnityEngine.Object[] { game, game.player }, "Apply Player 1 sprites");
        game.playerAvatarPrefab = prefab;
        if (game.player.avatar != null)
            Undo.DestroyObjectImmediate(game.player.avatar.gameObject);
        PrototypeGeometry.BuildAvatar(game.player);
        Undo.RegisterCreatedObjectUndo(game.player.avatar.gameObject, "Apply Player 1 sprites");
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        EditorSceneManager.SaveScene(game.gameObject.scene);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Convoy/Apply Player 2 Sprites")]
    public static void ApplyPlayer2ToScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit play mode before configuring the player avatar.");
        ConvoyPrototype game = UnityEngine.Object.FindFirstObjectByType<ConvoyPrototype>();
        if (game == null || game.player == null)
            throw new InvalidOperationException("Open the Convoy prototype scene before applying player sprites.");
        ConvoySpriteAvatar prefab = CreatePlayer2AvatarPrefab();
        Undo.RecordObject(game, "Apply Player 2 sprites");
        game.player2AvatarPrefab = prefab;
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        EditorSceneManager.SaveScene(game.gameObject.scene);
        AssetDatabase.SaveAssets();
    }
}
