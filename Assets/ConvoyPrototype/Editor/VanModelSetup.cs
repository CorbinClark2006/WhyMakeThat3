using System;
using System.Collections.Generic;
using Convoy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VanModelSetup
{
    public const string SourcePath = "Assets/main_van/truckgreybox_v008.fbx";
    public const string PursuerSourcePath = "Assets/Game Assets/truckchaser_v001.fbx";
    public const string PursuerMaterialPath = "Assets/ConvoyPrototype/Materials/Grass.mat";
    public const string PlayerPrefabPath = "Assets/ConvoyPrototype/PlayerVanV008.prefab";
    public const string PursuerPrefabPath = "Assets/ConvoyPrototype/PursuerVanV001.prefab";

    public static void AssignPrefabs(ConvoyPrototype game)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        if (source == null)
            throw new InvalidOperationException("Missing van model: " + SourcePath);
        game.mainVanModelPrefab = CreatePlayerPrefab(source);
        game.pursuerVanModelPrefab = CreatePursuerPrefab();
    }

    private static GameObject CreatePursuerPrefab()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PursuerSourcePath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(PursuerMaterialPath);
        if (source == null || material == null)
            throw new InvalidOperationException("Missing chaser model or Grass material.");
        GameObject root = new GameObject("Intact v001 pursuer van");
        try
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.transform.SetParent(root.transform, false);
            model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            Bounds bounds = PrototypeGeometry.GetModelBounds(root.transform);
            float scale = Mathf.Min(2.9f / bounds.size.x, 2.65f / bounds.size.y, 7f / bounds.size.z);
            model.transform.localScale *= scale;
            bounds = PrototypeGeometry.GetModelBounds(root.transform);
            model.transform.localPosition += new Vector3(-bounds.center.x, 0.025f - bounds.min.y, -bounds.center.z);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            return PrefabUtility.SaveAsPrefabAsset(root, PursuerPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [MenuItem("Tools/Convoy/Apply Chaser Van Model")]
    public static void ApplyPursuerToScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit play mode before replacing chasers.");
        ConvoyPrototype game = UnityEngine.Object.FindFirstObjectByType<ConvoyPrototype>();
        if (game == null)
            throw new InvalidOperationException("Open the Convoy scene before replacing chasers.");
        const string oldPath = "Assets/ConvoyPrototype/PursuerVanV008.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PursuerPrefabPath) == null &&
            AssetDatabase.LoadAssetAtPath<GameObject>(oldPath) != null)
        {
            string error = AssetDatabase.MoveAsset(oldPath, PursuerPrefabPath);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(error);
        }
        Undo.RecordObject(game, "Replace chaser van model");
        game.pursuerVanModelPrefab = CreatePursuerPrefab();
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        EditorSceneManager.SaveScene(game.gameObject.scene);
        AssetDatabase.SaveAssets();
    }

    private static GameObject CreatePlayerPrefab(GameObject source)
    {
        GameObject root = new GameObject("Intact v008 player van");
        try
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.transform.SetParent(root.transform, false);
            float scale = 0.65f;
            model.transform.localScale = Vector3.one * scale;
            model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            model.transform.localPosition = new Vector3(0f, 0.0615f, -0.099f);
            {
                List<Vector3> vertices = new List<Vector3>();
                List<int> triangles = new List<int>();
                foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
                {
                    int start = vertices.Count;
                    foreach (Vector3 vertex in filter.sharedMesh.vertices)
                        vertices.Add(root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)));
                    foreach (int index in filter.sharedMesh.triangles)
                        triangles.Add(start + index);
                }
                Mesh collision = new Mesh { name = "Intact v008 collision" };
                collision.SetVertices(vertices);
                collision.SetTriangles(triangles, 0);
                collision.RecalculateBounds();
                collision.RecalculateNormals();
                const string collisionPath = "Assets/ConvoyPrototype/PlayerVanV008Collision.asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(collisionPath);
                if (existing == null)
                    AssetDatabase.CreateAsset(collision, collisionPath);
                else
                {
                    EditorUtility.CopySerialized(collision, existing);
                    UnityEngine.Object.DestroyImmediate(collision);
                    collision = existing;
                }
                root.AddComponent<MeshCollider>().sharedMesh = collision;
            }
            foreach (Transform child in root.GetComponentsInChildren<Transform>())
                child.gameObject.layer = 8;
            return PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [MenuItem("Tools/Convoy/Apply v008 Van Models")]
    public static void ApplyToScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit play mode before replacing van models.");
        ConvoyPrototype game = UnityEngine.Object.FindFirstObjectByType<ConvoyPrototype>();
        if (game == null || game.van == null)
            throw new InvalidOperationException("Open the Convoy scene before replacing van models.");
        Undo.RecordObject(game, "Install intact v008 vans");
        AssignPrefabs(game);
        if (game.mainVanModel != null)
            Undo.DestroyObjectImmediate(game.mainVanModel.gameObject);
        PrototypeGeometry.RemoveLegacyVanParts(game.van);
        PrototypeGeometry.InstallMainVanModel(game);
        Undo.RegisterCreatedObjectUndo(game.mainVanModel.gameObject, "Install intact v008 vans");
        FitGameplayAssets(game);
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        EditorSceneManager.SaveScene(game.gameObject.scene);
        AssetDatabase.SaveAssets();
    }

    private static void FitGameplayAssets(ConvoyPrototype game)
    {
        Transform cargo = game.van.Find("Jumpable cargo boxes");
        foreach (Transform box in cargo)
        {
            Undo.RecordObject(box, "Fit cargo to intact van");
            Vector3 p = box.localPosition;
            if (p.z > 1.1f)
                p.z = 1.45f;
            box.localPosition = p;
        }
        Transform frontStep = cargo.Find("Right cargo box 1");
        if (frontStep != null)
            frontStep.localPosition = new Vector3(0f, 1.24f, 1.45f);
        Transform rearStep = cargo.Find("Right cargo box 2 (2)");
        if (rearStep != null)
            rearStep.localPosition = new Vector3(0.48f, 1.05f, 0.65f);
        Move(game.ammoSupply, new Vector3(0.9f, 0.9f, 2.5f));
        if (game.supplyPack != null)
        {
            Move(game.supplyPack.transform, game.ammoSupply.localPosition);
            Undo.RecordObject(game.supplyPack.transform, "Fit battery to tiny character");
            game.supplyPack.transform.localScale = Vector3.one * ConvoyAmmoPack.Scale;
            BoxCollider batteryCollider = game.supplyPack.GetComponent<BoxCollider>();
            Undo.RecordObject(batteryCollider, "Keep passenger battery pickup non-blocking");
            batteryCollider.isTrigger = true;
        }
        Move(game.player.transform, new Vector3(-0.97f, 0.14f, -1.5f));
        Undo.RecordObject(game.vanHitbox, "Fit van damage hitbox");
        game.vanHitbox.center = new Vector3(0f, 0.6f, 1.25f);
        game.vanHitbox.size = new Vector3(3.5f, 3.5f, 9.5f);
    }

    private static void Move(Transform target, Vector3 position)
    {
        Undo.RecordObject(target, "Fit gameplay assets to intact van");
        target.localPosition = position;
    }
}
