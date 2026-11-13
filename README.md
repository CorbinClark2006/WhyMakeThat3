# WhyMakeThat3

A Unity project built collaboratively through GitHub.

**Repository:** https://github.com/CorbinClark2006/WhyMakeThat3

## Get the project

Install:

- Git and [Git LFS](https://git-lfs.com/).
- [Unity Hub](https://unity.com/download) and **Unity 6000.4.1f1**. The exact editor version is recorded in [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt).

Then run:

```sh
git lfs install
git clone https://github.com/CorbinClark2006/WhyMakeThat3.git
cd WhyMakeThat3
git lfs pull
```

In Unity Hub, choose **Add project from disk** and select the cloned folder. Open it with the editor version above and allow Unity to import assets and restore packages. Open [SampleScene](Assets/Scenes/SampleScene.unity) to get started.

## Work together

Anyone can clone this public repository. To push branches directly, the owner must invite your GitHub account through **Settings > Collaborators**, and you must accept the invitation. Public visibility alone does not grant push access.

For each change, start from an up-to-date `main` and use a separate branch:

```sh
git switch main
git pull --ff-only origin main
git switch -c feature/describe-your-change
```

Make and save your changes in Unity, then inspect and commit them:

```sh
git status
git add Assets Packages ProjectSettings
git diff --cached --stat
git commit -m "Describe your change"
git push -u origin feature/describe-your-change
```

Stage any other intentional files, such as documentation, separately. Open a pull request on GitHub, review it together, and merge it into `main`. Everyone can then pull the merged changes. Without collaborator access, fork the repository, push your branch to your fork, and open a pull request back here.

### Unity collaboration rules

- Commit assets and their `.meta` files together; move and rename assets inside Unity to preserve references.
- Keep [Assets](Assets), [Packages](Packages), and [ProjectSettings](ProjectSettings) in version control. Commit package manifest and lockfile changes together.
- Do not commit generated caches, builds, local settings, credentials, or API keys. The [ignore rules](.gitignore) exclude Unity's generated directories.
- Save and close Unity before pulling changes that affect scenes, packages, or project settings, then reopen it afterward.
- Coordinate before editing the same scene or prefab. Prefer separate scenes and prefabs to reduce merge conflicts.
- Keep text serialization and visible metadata enabled. Do not resolve scene, prefab, or `.meta` conflicts by blindly taking one side.
- Git LFS is configured for binary assets through [.gitattributes](.gitattributes). Every contributor needs Git LFS installed before adding binary assets.
- Only upload assets and code you have permission to publish publicly.

## Optional: MCP for Unity

[MCP for Unity](https://github.com/CoplayDev/unity-mcp) is included in the Unity package dependencies. It is not required for normal editing or play mode.

To use it with VS Code:

1. Install [uv](https://docs.astral.sh/uv/getting-started/installation/) and ensure `uvx` is on your PATH.
2. Open the project in Unity and wait for packages to finish importing.
3. Open **Window > MCP for Unity**. Select **stdio** transport and start the Unity bridge if it is stopped.
4. Open this folder in VS Code. Use **MCP: List Servers**, select `unityMCP`, and start/trust it when prompted.

The workspace configuration is in [.vscode/mcp.json](.vscode/mcp.json). Both the Unity package and the server follow upstream `main`; the Unity lockfile records the resolved package revision. Avoid configuring a second user-level `unityMCP` server for the same workspace.
