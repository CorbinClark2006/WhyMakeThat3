using System.IO;
using Convoy;
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BrowserMultiplayerSetup
{
    [MenuItem("Tools/Convoy/Configure Browser Multiplayer")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying)
            throw new System.InvalidOperationException("Exit Play mode before configuring multiplayer.");
        var game = Object.FindAnyObjectByType<ConvoyPrototype>();
        if (game == null)
            throw new System.InvalidOperationException("Open the authored ConvoyPrototype scene first.");
        NetworkObject world = CreatePrefab<ConvoyNetworkWorld>("ConvoyNetworkWorld");
        NetworkObject entity = CreatePrefab<ConvoyNetworkEntity>("ConvoyNetworkEntity");
        NetworkObject input = CreatePrefab<ConvoyNetworkInput>("ConvoyNetworkInput");
        var session = game.GetComponent<ConvoyMultiplayer>();
        if (session == null) session = game.gameObject.AddComponent<ConvoyMultiplayer>();
        session.worldPrefab = world;
        session.entityPrefab = entity;
        session.inputPrefab = input;
        game.network = session;
        EditorUtility.SetDirty(game);
        EditorUtility.SetDirty(session);
        NetworkProjectConfigUtilities.RebuildPrefabTable();
        PlayerSettings.runInBackground = true;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        EditorSceneManager.SaveScene(game.gameObject.scene);
        Debug.Log("Browser multiplayer configured without rebuilding the van or cargo.");
    }

    private static NetworkObject CreatePrefab<T>(string name) where T : NetworkBehaviour
    {
        const string folder = "Assets/ConvoyPrototype/Resources";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/ConvoyPrototype", "Resources");
        string path = folder + "/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<NetworkObject>(path);
        if (existing != null) return existing;
        var root = new GameObject(name);
        try
        {
            var obj = root.AddComponent<NetworkObject>();
            obj.Flags = typeof(T) == typeof(ConvoyNetworkInput) ?
                NetworkObjectFlags.V2 | NetworkObjectFlags.DestroyWhenStateAuthorityLeaves :
                NetworkObjectFlags.V2 | NetworkObjectFlags.MasterClientObject | NetworkObjectFlags.AllowStateAuthorityOverride;
            root.AddComponent<T>();
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<NetworkObject>();
        }
        finally { Object.DestroyImmediate(root); }
    }

    [MenuItem("Tools/Convoy/Build Browser Co-op")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
            throw new System.InvalidOperationException("Exit Play mode before building.");
        if (Application.isBatchMode)
            EditorSceneManager.OpenScene("Assets/Scenes/ConvoyPrototype.unity");
        Configure();
        PlayerSettings.WebGL.template = "PROJECT:Convoy";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        string output = Path.GetFullPath("Build/WebGL");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/ConvoyPrototype.unity" },
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.InvalidOperationException("Browser build failed: " + report.summary.result);
        File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty);
        Debug.Log("Browser co-op build saved to " + output);
    }
}
