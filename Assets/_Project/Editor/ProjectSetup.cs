using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Silence.Editor
{
    /// <summary>
    /// One-shot project setup: quality settings for pixel art and the Boot, Menu, Map and Battle scenes,
    /// each with a Pixel Perfect Camera (32 PPU, 1920x1080) and a global 2D light.
    /// Safe to re-run: existing scenes are left alone.
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod Silence.Editor.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        const string SceneDir = "Assets/_Project/Scenes";
        static readonly string[] Scenes = { "Boot", "Menu", "Map", "Battle" };

        [MenuItem("Silence/Project Setup")]
        public static void Run()
        {
            ApplyQuality();
            var buildScenes = new EditorBuildSettingsScene[Scenes.Length];
            for (int i = 0; i < Scenes.Length; i++)
            {
                string path = $"{SceneDir}/{Scenes[i]}.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) CreateScene(path);
                buildScenes[i] = new EditorBuildSettingsScene(path, true);
            }
            EditorBuildSettings.scenes = buildScenes;
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Done.");
        }

        static void ApplyQuality()
        {
            string[] names = QualitySettings.names;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.antiAliasing = 0;
                QualitySettings.vSyncCount = 1;
            }
            QualitySettings.SetQualityLevel(current, false);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
        }

        static void CreateScene(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = PixelArtImporter.PixelsPerUnit;
            ppc.refResolutionX = 1920;
            ppc.refResolutionY = 1080;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.None;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.None;

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;

            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
