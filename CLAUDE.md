# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity **6000.6.0f1** project built from the Universal Render Pipeline (URP 17.7) template. There's very little custom content so far: one scene (`Assets/Scenes/SampleScene.unity`, the only scene in Build Settings) and scripts in `Assets/Scripts/`.

## Working with the editor

- **UnityMCP** (`com.coplaydev.unity-mcp`, installed from git in `Packages/manifest.json`) is the main way to drive the editor: creating GameObjects, editing scenes, reading the console, running tests. It only connects while the Unity editor is open with the MCP bridge running. If the connection fails, ask the user to start it rather than assuming it isn't set up.
- **Without MCP**, you can edit scene and asset YAML by hand. Requirements:
  - Every new asset needs a `.meta` file with a unique GUID.
  - New scene objects need unique `fileID`s.
  - Root Transforms must be added to the `SceneRoots` → `m_Roots` list at the end of the `.unity` file.
  - If the scene is open in the editor, Unity will ask the user to reload it.
- **The editor generates** the `.csproj` and `.sln` files. They're .gitignored, so don't edit them. There's no command-line build or lint step; Unity compiles scripts when they change.
- **Tests:** `com.unity.test-framework` is installed, but there are no tests or `.asmdef` files yet, so all scripts compile into `Assembly-CSharp`. Tests run from the editor's Test Runner or through MCP.

## Conventions and gotchas

- **Input:** `activeInputHandler: 1` means only the new Input System is enabled. Use `UnityEngine.InputSystem` and `Assets/InputSystem_Actions.inputactions`, not legacy `Input.GetKey`/`Input.GetAxis`.
- **Rendering:** URP is set per quality level.
  - The `Mobile` level uses `Assets/Settings/Mobile_RPAsset`; the `PC` level uses `PC_RPAsset`. Each has its own renderer asset.
  - `GraphicsSettings` has no default pipeline.
  - New renderers should use a URP material, such as the package's `Lit.mat` (guid `31321ba15b8f8eb4c954353edc038b1d`). Built-in `Default-Material` renders pink.
- **Template files:** `Assets/TutorialInfo/` and `Assets/Readme.asset` came with the template and can be deleted.
- **Git:**
  - `.gitattributes` sends binary assets (textures, models, audio, etc.) through **Git LFS**, so LFS must be installed.
  - `Library/`, `Temp/`, `Logs/`, `UserSettings/` and build output are ignored.
  - Always commit `.meta` files together with their assets.
