using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

using PranaTactics.Unit;
using PranaTactics.Unit.Presentation;

namespace PranaTactics.Editor.Validation
{
    /// <summary>
    /// Unity上で撃破演出の生成、描画設定とUnitカメラの表示対象を検証します。
    /// </summary>
    public static class UnitEliminationValidation
    {
        /// <summary>
        /// 実際の設定アセットとUnityの生成処理を使って回帰を検証します。
        /// </summary>
        [MenuItem("Tools/Prana Tactics/Validation/Unit Elimination")]
        public static void Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("撃破演出の検証はPlayモードを終了してから実行してください。");
                return;
            }

            UnitEliminationProfile profile = AssetDatabase.LoadAssetAtPath<UnitEliminationProfile>(
                "Assets/Datas/UnitAssets/UnitEliminationEffect.asset");
            Check(profile != null, "撃破演出設定を読み込めません。");
            Scene scene = EditorSceneManager.NewPreviewScene();
            UnitEliminationEffect effect = null;
            UnitEliminationProfile copy = UnityEngine.Object.Instantiate(profile);
            try
            {
                effect = UnitEliminationEffect.Spawn(copy, Vector3.zero, scene);
                Check(effect != null, "撃破エフェクトを生成できません。");
                Check(effect.RenderAtTime(copy.ExpansionDuration), "膨張後に再生が継続していません。");
                Check(effect.GetComponentsInChildren<Collider>().Length == 0, "撃破演出にColliderが残っています。");
                ValidateRenderers(effect, copy);
                ValidateCameras(effect.gameObject.layer);
                double end = copy.ExpansionDuration + copy.HoldDuration + copy.CollapseDuration + 0.01;
                Check(!effect.RenderAtTime(end), "撃破演出が終了しません。");
                Debug.Log("PASS: Unit elimination VFX Graph, GPU texture, Unit cameras and completion.");
            }
            finally
            {
                if (effect != null)
                {
                    UnityEngine.Object.DestroyImmediate(effect.gameObject);
                }

                UnityEngine.Object.DestroyImmediate(copy);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>
        /// VFX Graphへの接続とGPUが描画した球体の色・透明度を検証します。
        /// </summary>
        private static void ValidateRenderers(UnitEliminationEffect effect, UnitEliminationProfile profile)
        {
            Check(effect.GetComponentsInChildren<LineRenderer>().Length == 0, "旧LineRendererが残っています。");
            UnityEngine.VFX.VisualEffect visualEffect = effect.GetComponent<UnityEngine.VFX.VisualEffect>();
            Check(visualEffect != null && visualEffect.visualEffectAsset == profile.EffectAsset,
                "VFX Graphが設定されていません。");
            Check(visualEffect.HasTexture("EffectTexture"), "EffectTextureが公開されていません。");
            Check(!ShaderUtil.ShaderHasError(profile.ProceduralMaterial.shader), "描画シェーダーにエラーがあります。");
            RenderTexture texture = visualEffect.GetTexture("EffectTexture") as RenderTexture;
            Check(texture != null && texture.IsCreated(), "VFXテクスチャが生成されていません。");
            RenderTexture previous = RenderTexture.active;
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
            try
            {
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                pixels.Apply();
                Color center = pixels.GetPixel(texture.width / 2, texture.height / 2);
                Check(center.a > 0.99f, "球体中心が透けています。");
                Check(center.r < 0.05f && center.b < 0.05f, "球体中心が黒くありません。");
                Check(pixels.GetPixel(0, 0).a < 0.01f, "VFXの外側が透明ではありません。");
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        /// <summary>
        /// Resources内のUnitカメラから撃破演出が表示可能か検証します。
        /// </summary>
        private static void ValidateCameras(int effectLayer)
        {
            int cameraCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources" }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (MicroUnitBehavior micro in prefab.GetComponentsInChildren<MicroUnitBehavior>(true))
                {
                    Camera camera = micro.UnitCamera;
                    Check(camera != null && (camera.cullingMask & (1 << effectLayer)) != 0,
                        $"Unitカメラの描画対象に撃破演出が含まれていません。Prefab={prefab.name}");
                    cameraCount++;
                }
            }

            Check(cameraCount > 0, "検証するUnitカメラが見つかりません。");
        }

        /// <summary>
        /// 検証失敗を呼び出し側へ通知します。
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
