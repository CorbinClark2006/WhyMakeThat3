using System;
using Convoy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NatureSpriteSetup
{
    public static readonly string[] Names = { "Tree1", "BIG_funky_rock", "funky_rock", "funky_rock_3",
        "funky_grass", "funky_grass_2", "funky_grasss_3", "funky_shroom_1", "funky_shroom_3" };

    public static void AssignSprites(ConvoyPrototype game)
    {
        game.natureSprites = new Sprite[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            string path = "Assets/Game Assets/nature/" + Names[i] + ".png";
            PlayerSpriteSetup.Import("Assets/Game Assets/nature/", Names[i], 100f);
            Texture2D image = new Texture2D(2, 2);
            try
            {
                if (!image.LoadImage(System.IO.File.ReadAllBytes(path)))
                    throw new InvalidOperationException("Could not read nature sprite: " + path);
                Color32[] pixels = image.GetPixels32();
                int bottom = -1;
                for (int y = 0; y < image.height && bottom < 0; y++)
                    for (int x = 0; x < image.width; x++)
                        if (pixels[y * image.width + x].a > 0)
                        {
                            bottom = y;
                            break;
                        }
                if (bottom < 0)
                    throw new InvalidOperationException("Nature sprite has no visible pixels: " + path);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, (float)bottom / image.height);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                game.natureSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
        game.natureMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/ConvoyPrototype/Materials/Player sprite.mat");
        if (game.natureMaterial == null)
            throw new InvalidOperationException("Create the player sprite material before assigning roadside nature.");
        Shader shader = Shader.Find("Convoy/Nature Billboard");
        if (shader == null)
            throw new InvalidOperationException("The nature billboard shader is missing.");
        game.natureBatchMaterials = new Material[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            string path = "Assets/ConvoyPrototype/Materials/Nature " + Names[i] + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.mainTexture = game.natureSprites[i].texture;
            EditorUtility.SetDirty(material);
            game.natureBatchMaterials[i] = material;
        }
    }

    [MenuItem("Tools/Convoy/Apply Roadside Nature Sprites")]
    public static void ApplyToScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before replacing roadside scenery.");
        ConvoyPrototype game = UnityEngine.Object.FindFirstObjectByType<ConvoyPrototype>();
        if (game == null || game.road == null)
            throw new InvalidOperationException("Open the Convoy scene before applying roadside nature.");
        Undo.RegisterFullObjectHierarchyUndo(game.road.gameObject, "Replace roadside scenery");
        Undo.RecordObject(game, "Assign nature sprites");
        AssignSprites(game);
        PrototypeGeometry.ReplaceRoadsideNature(game);
        SaveBatchMeshes(game);
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        EditorSceneManager.SaveScene(game.gameObject.scene);
        AssetDatabase.SaveAssets();
    }

    public static void SaveBatchMeshes(ConvoyPrototype game)
    {
        const string folder = "Assets/ConvoyPrototype/NatureMeshes";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/ConvoyPrototype", "NatureMeshes");
        int tileIndex = 0;
        foreach (Transform tile in game.road)
        {
            string path = folder + "/Tile" + tileIndex++ + ".asset";
            UnityEngine.Object[] old = AssetDatabase.LoadAllAssetsAtPath(path);
            Mesh root = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            ConvoyNatureBatch[] batches = tile.GetComponentsInChildren<ConvoyNatureBatch>();
            for (int i = 0; i < batches.Length; i++)
            {
                Mesh mesh = batches[i].GetComponent<MeshFilter>().sharedMesh;
                if (i == 0 && root != null)
                {
                    EditorUtility.CopySerialized(mesh, root);
                    batches[i].MarkMeshSaved();
                    UnityEngine.Object.DestroyImmediate(mesh);
                    batches[i].GetComponent<MeshFilter>().sharedMesh = root;
                    EditorUtility.SetDirty(root);
                }
                else
                {
                    if (i == 0) AssetDatabase.CreateAsset(mesh, path);
                    else AssetDatabase.AddObjectToAsset(mesh, path);
                    batches[i].MarkMeshSaved();
                }
            }
            foreach (UnityEngine.Object asset in old)
                if (asset != root)
                    UnityEngine.Object.DestroyImmediate(asset, true);
        }
        AssetDatabase.SaveAssets();
    }
}
