// Trial (2026-11-27): builds Assets/Scenes/ModelCity.unity, the 3D city model test (see ModelCity.cs). Not part of the game yet.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Sector.ModelCity;

public static class ModelCityBuilder
{
    const string Root = "Assets/ModelCity";

    [MenuItem("LEVIATHAN/ModelCity/Build Scene")]
    public static void Build()
    {
        var profile = Profile();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Assemble(null, profile, false);
        // fog on in the saved scene, so the URP fog variants are kept
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 200; RenderSettings.fogEndDistance = 700;
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ModelCity.unity");
        Debug.Log("ModelCity scene built");
    }

    // Adds the model to the game scene (SampleScene) as an inactive root "ModelCity"; OpsScreen switches it on in its
    // 입체 모형 view (OpsModelView.cs). Run again to replace it.
    [MenuItem("LEVIATHAN/ModelCity/Add To Game Scene")]
    public static void AddToGameScene()
    {
        var profile = Profile();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        foreach (var go in scene.GetRootGameObjects()) if (go.name == "ModelCity") Object.DestroyImmediate(go);
        var rootGo = new GameObject("ModelCity");
        Assemble(rootGo, profile, true);
        rootGo.SetActive(false);
        // the game camera never sees fog (the painting covers it), but the scene must have fog on to keep the variants
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 2000; RenderSettings.fogEndDistance = 4000;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("ModelCity added to SampleScene");
    }

    static VolumeProfile Profile()
    {
        var profilePath = $"{Root}/ModelCityVolume.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(1.0f); bloom.intensity.Override(0.9f); bloom.scatter.Override(0.65f);
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true); color.postExposure.Override(0.2f); color.contrast.Override(18f); color.saturation.Override(4f);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.2f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            AssetDatabase.SaveAssets();
        }
        return profile;
    }

    // camera, sun, volume, the city and its HUD; under `root` (the city component on it) or as scene roots
    static void Assemble(GameObject root, VolumeProfile profile, bool embedded)
    {
        GameObject Make(string name) { var go = new GameObject(name); if (root != null) go.transform.SetParent(root.transform, false); return go; }
        var camGo = Make(embedded ? "ModelCamera" : "Main Camera");
        if (!embedded) camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = true; cam.depth = embedded ? 10 : 0;
        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camData.antialiasingQuality = AntialiasingQuality.High;

        var sun = Make(embedded ? "ModelSun" : "Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.8f;

        var vol = Make("Global Volume").AddComponent<Volume>();
        vol.isGlobal = true; vol.sharedProfile = profile;

        var city = (root != null ? root : Make("ModelCity")).AddComponent<ModelCity>();
        city.sun = sun; city.cam = cam;

        var hudGo = Make("ModelHud");
        var doc = hudGo.AddComponent<UnityEngine.UIElements.UIDocument>();
        doc.panelSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/UI/SectorPanelSettings.asset");
        doc.sortingOrder = embedded ? -1 : 0;   // under the game's own UI
        var hud = hudGo.AddComponent<ModelHud>();
        hud.city = city; hud.embedded = embedded; hud.styleSheet = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>($"{Root}/ModelHud.uss");
        city.hud = hud;
    }
}
