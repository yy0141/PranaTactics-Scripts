using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using PranaTactics.Audio;
using PranaTactics.Combat;
using PranaTactics.UI.Gameplay;

namespace PranaTactics.Editor.Validation
{
    /// <summary>
    /// Unityに読み込まれた確認音、シーン参照、マーカー形状を検証します。
    /// </summary>
    public static class CombatFeedbackValidation
    {
        /// <summary>
        /// Editorメニューまたはバッチモードから演出アセットの整合性を検証します。
        /// </summary>
        [MenuItem("Prana Tactics/Validation/Combat Feedback")]
        public static void Validate()
        {
            CombatFeedbackProfile profile = AssetDatabase.LoadAssetAtPath<CombatFeedbackProfile>(
                "Assets/Datas/Sound/CombatFeedback.asset");
            Check(profile != null, "CombatFeedback profile must import successfully.");
            var clips = new HashSet<AudioClip>();
            foreach (CombatFeedbackKind kind in new[]
                { CombatFeedbackKind.UnitHit, CombatFeedbackKind.DefenseHit, CombatFeedbackKind.Elimination })
            {
                SoundCue cue = profile.GetCue(kind);
                Check(cue != null, $"Cue missing for {kind}.");
                AudioClip clip = cue.GetRandomClip();
                Check(clip != null && clips.Add(clip), $"Distinct imported audio is required for {kind}.");
                Check(clip.length > 0.03f && clip.length < 0.4f, $"Unexpected cue duration: {kind}.");
                Check(cue.SpatialBlend == 0.0f, $"Confirmation must use 2D audio: {kind}.");
            }

            ValidateSceneReferences(profile, clips);
            ValidateMarkerGeometry();
            Debug.Log("PASS: CombatFeedback imported cues, distinct damage audio, TitleScene profile wiring, marker geometry and elimination protection.");
        }

        /// <summary>
        /// TitleSceneの音声設定が有効なアセットを参照するか検証します。
        /// </summary>
        private static void ValidateSceneReferences(CombatFeedbackProfile profile, HashSet<AudioClip> clips)
        {
            Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/TitleScene.unity");
            bool wasLoaded = scene.isLoaded;
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity", OpenSceneMode.Additive);
            }

            try
            {
                SoundManager manager = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    manager = root.GetComponentInChildren<SoundManager>(true);
                    if (manager != null)
                    {
                        break;
                    }
                }

                Check(manager != null, "TitleScene must contain SoundManager.");
                var settings = new SerializedObject(manager);
                Check(settings.FindProperty("_combatFeedbackProfile").objectReferenceValue == profile,
                    "TitleScene must reference the combat profile.");
                SoundCue damage = settings.FindProperty("_damageReceivedCue").objectReferenceValue as SoundCue;
                AudioClip clip = damage != null ? damage.GetRandomClip() : null;
                Check(clip != null && !clips.Contains(clip), "Damage received must have its own audio.");
            }
            finally
            {
                if (!wasLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>
        /// 実際のUIメッシュと、撃破表示の上書き防止を検証します。
        /// </summary>
        private static void ValidateMarkerGeometry()
        {
            MicroReticleProfile profile = AssetDatabase.LoadAssetAtPath<MicroReticleProfile>(
                "Assets/Datas/HudProfiles/MicroReticle.asset");
            Check(profile != null, "MicroReticle profile must import successfully.");
            var markerObject = new GameObject("CombatFeedbackValidation", typeof(RectTransform));
            try
            {
                CombatFeedbackMarker marker = markerObject.AddComponent<CombatFeedbackMarker>();
                MethodInfo populate = typeof(CombatFeedbackMarker).GetMethod(
                    "OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (CombatFeedbackKind kind in new[]
                    { CombatFeedbackKind.UnitHit, CombatFeedbackKind.DefenseHit, CombatFeedbackKind.Elimination })
                {
                    marker.ResetResult();
                    marker.ShowResult(kind, profile);
                    using (var vertices = new VertexHelper())
                    {
                        populate.Invoke(marker, new object[] { vertices });
                        Check(vertices.currentVertCount == (kind == CombatFeedbackKind.Elimination ? 24 : 16),
                            $"Unexpected marker geometry: {kind}.");
                    }

                    Check(!marker.raycastTarget, "Feedback must not intercept input.");
                }

                marker.ShowResult(CombatFeedbackKind.UnitHit, profile);
                Check(marker.color == profile.GetFeedbackColor(CombatFeedbackKind.Elimination),
                    "Normal hits must not overwrite elimination feedback.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(markerObject);
            }
        }

        /// <summary>
        /// 検証失敗の理由をEditorまたはバッチログへ報告します。
        /// </summary>
        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
