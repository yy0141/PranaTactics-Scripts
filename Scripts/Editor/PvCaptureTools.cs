using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PranaTactics.Editor.Promo
{
    /// <summary>
    /// 通常ビルドと分離したWindows向けPV撮影ビルドを作成します。
    /// </summary>
    public sealed class PvCaptureTools : IProcessSceneWithReport
    {
        private const string k_ScenePath = "Assets/Scenes/PVScene.unity";

        private static bool s_isCaptureBuild;

        public int callbackOrder => 0;

        [MenuItem("Tools/PV Capture/Open Scene")]
        public static void OpenScene()
        {
            if (!EditorApplication.isPlaying && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(k_ScenePath);
            }
        }

        [MenuItem("Tools/PV Capture/Build Windows Capture Player")]
        public static void BuildCapturePlayer()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Playを停止してから撮影ビルドを作成してください。");
            }

            string output = EditorUtility.SaveFolderPanel("PV専用ビルドの出力先", "", "PV-Capture");

            if (string.IsNullOrEmpty(output))
            {
                return;
            }

            var scenes = new List<string> { k_ScenePath };

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !scenes.Contains(scene.path))
                {
                    scenes.Add(scene.path);
                }
            }

            foreach (string scenePath in new[] { "Assets/Scenes/GameplayUIScene.unity", "Assets/Scenes/MacroPhaseUIScene.unity", "Assets/Scenes/SettingsUIScene.unity", "Assets/Scenes/ResultScene.unity" })
            {
                if (!scenes.Contains(scenePath))
                {
                    scenes.Add(scenePath);
                }
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = Path.Combine(output, "PV-Capture.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { "PV_CAPTURE" }
            };

            s_isCaptureBuild = true;

            try
            {
                BuildReport report = BuildPipeline.BuildPlayer(options);

                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException("PV専用ビルドに失敗しました。Build Reportを確認してください。");
                }

                File.Copy("Assets/Promo/Example.json", Path.Combine(output, "Example.json"), true);
                File.Copy("Assets/Promo/Gameplay.json", Path.Combine(output, "Gameplay.json"), true);
                File.Copy(
                    "Assets/Promo/GameplayMultiple.json",
                    Path.Combine(output, "GameplayMultiple.json"),
                    true);
                Debug.Log($"PV撮影ビルド: {options.locationPathName}。-pvScenario \"JSONの絶対パス\" で外部演出を読み込めます。");
            }
            finally
            {
                s_isCaptureBuild = false;
            }
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report != null && scene.path == k_ScenePath && !s_isCaptureBuild)
            {
                throw new BuildFailedException("PVSceneは通常ビルドに含められません。Tools/PV Captureから専用ビルドを作成してください。");
            }
        }
    }
}
