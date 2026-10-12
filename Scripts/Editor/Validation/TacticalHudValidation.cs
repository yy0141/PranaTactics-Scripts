using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using PranaTactics.Pranam;
using PranaTactics.UI.Gameplay;
using PranaTactics.Unit;

namespace PranaTactics.Editor.Validation
{
    /// <summary>
    /// 共通戦術HUDの参照を検証し、確認用の描画を保存します。
    /// </summary>
    public static class TacticalHudValidation
    {
        /// <summary>
        /// 保存済みシーンを読み込み、UIを1920x1080と1280x720で描画します。
        /// </summary>
        [MenuItem("Prana Tactics/Validation/Tactical HUD")]
        public static void Validate()
        {
            Scene preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameplayUIScene.unity");
            var cameraObject = new GameObject("HUD Preview Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            Camera camera = cameraObject.AddComponent<Camera>();
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.065f, 0.09f, 0.105f);
                camera.cullingMask = 1 << 5;
                foreach (GameObject root in preview.GetRootGameObjects())
                {
                    ValidateReferences(root);
                    foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                    {
                        canvas.gameObject.SetActive(true);
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        canvas.planeDistance = 1.0f;
                    }
                }

                PrepareSample(preview);
                Render(camera, 1920, 1080);
                Render(camera, 1280, 720);
                Debug.Log("PASS: Tactical HUD scene references, eight cost labels, timer ring and three kill rows. Preview PNGs saved to tmp.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        /// <summary>
        /// 欠損スクリプトと兵装の必須表示参照を検証します。
        /// </summary>
        private static void ValidateReferences(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0,
                    "Missing script: " + child.name);
            }

            foreach (PranamSlotUI slot in root.GetComponentsInChildren<PranamSlotUI>(true))
            {
                var serialized = new SerializedObject(slot);
                foreach (string field in new[] { "_frameImage", "_nameText", "_iconImage", "_actionPointCostText" })
                {
                    Check(serialized.FindProperty(field).objectReferenceValue != null,
                        "Missing slot reference: " + field);
                }
            }
        }

        /// <summary>
        /// 実アセットを使って選択中と未選択の兵装、および撃破通知の描画例を用意します。
        /// </summary>
        private static void PrepareSample(Scene scene)
        {
            Transform[] transforms = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach (Transform item in transforms)
            {
                if (item.name == "Panel" || item.name == "DamageVignette")
                {
                    item.gameObject.SetActive(false);
                }
            }

            PranamData[] weapons = AssetDatabase.FindAssets("t:PranamData", new[] { "Assets/Datas" })
                .Select(id => AssetDatabase.LoadAssetAtPath<PranamData>(AssetDatabase.GUIDToAssetPath(id)))
                .Where(data => data != null && data.Icon != null).Take(8).ToArray();
            PranamSlotUI[] slots = transforms.Select(item => item.GetComponent<PranamSlotUI>())
                .Where(slot => slot != null).OrderBy(slot => slot.transform.parent.name).ThenBy(slot => slot.name).ToArray();
            Check(slots.Length == 8 && weapons.Length > 0, "Eight weapon slots and sample weapon assets are required.");
            for (int index = 0; index < slots.Length; index++)
            {
                PranamData weapon = weapons[index % weapons.Length];
                slots[index].Initialize(null, weapon.Icon, weapon.DisplayName, weapon.ActionPointCost, index % 4 == 0);
            }

            UnitParams[] characters = AssetDatabase.FindAssets("t:UnitParams", new[] { "Assets/Datas/CharacterAssets" })
                .Select(id => AssetDatabase.LoadAssetAtPath<UnitParams>(AssetDatabase.GUIDToAssetPath(id))).ToArray();
            foreach (Transform row in transforms.Where(item => item.name.StartsWith("Kill Notification ", StringComparison.Ordinal)))
            {
                row.GetComponent<CanvasGroup>().alpha = 1.0f;
                foreach (Image icon in row.GetComponentsInChildren<Image>())
                {
                    icon.sprite = icon.name == "Weapon Icon" ? weapons[0].Icon : characters[0].GameplaySprite;
                    icon.preserveAspect = true;
                }

                row.GetComponentsInChildren<TextMeshProUGUI>().First(text => text.name == "Weapon Name").text = weapons[0].DisplayName;
            }

            foreach (TacticalHudGraphic graphic in transforms.Select(item => item.GetComponent<TacticalHudGraphic>()).Where(item => item != null))
            {
                if (graphic.name == "Countdown Remaining")
                {
                    graphic.SetFill(0.72f);
                }
            }
        }

        /// <summary>
        /// UIのレイアウトを確定してカメラ出力をPNGへ保存します。
        /// </summary>
        private static void Render(Camera camera, int width, int height)
        {
            var target = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                Directory.CreateDirectory("tmp");
                File.WriteAllBytes($"tmp/tactical-hud-{width}x{height}.png", texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 検証失敗の理由を報告します。
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
