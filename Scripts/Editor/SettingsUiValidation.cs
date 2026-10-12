using System;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

using PranaTactics.Settings;
using PranaTactics.UI.Screens;

namespace PranaTactics.Editor
{
    /// <summary>
    /// 再生中の設定UIについて接続先とインスタンス再利用を検証します。
    /// </summary>
    public static class SettingsUiValidation
    {
        [MenuItem("Tools/Prana Tactics/Validate retained Settings UI %#F8")]
        public static async void Validate()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("設定UIの検証は再生中に実行してください。");

                return;
            }

            await SettingsUiBootstrap.EnsureLoadedAsync();
            SettingsUIManager manager = SettingsUIManager.Instance;
            string originalJson = SettingsJson.Serialize(SettingsManager.Current);

            try
            {
                manager.Open();
                await Awaitable.NextFrameAsync();
                Transform menu = manager.transform.Find("GameMenu");
                Transform root = manager.transform.Find("SettingsScreen");

                Check(
                    manager.IsOpen && menu.gameObject.activeSelf && !root.gameObject.activeSelf,
                    "最初はメニューだけ表示");
                menu.Find("Panel/SettingsBorder/Settings").GetComponent<Button>().onClick.Invoke();
                Check(root.gameObject.activeSelf && !menu.gameObject.activeSelf, "設定へ移行");
                manager.Toggle();
                Check(
                    menu.gameObject.activeSelf && !root.gameObject.activeSelf && manager.IsOpen,
                    "Escでメニューへ戻る");
                Check(MenuScenePolicy.CanReturnToLobby("SettingScene", false), "射撃練習からロビー");
                Check(MenuScenePolicy.CanReturnToLobby("TutorialScene", false), "チュートリアルからロビー");
                Check(MenuScenePolicy.CanReturnToLobby("Solo_CharacterSelectScene", true), "ソロ選択からロビー");
                Check(MenuScenePolicy.CanReturnToLobby("Solo_InGameScene", true), "ソロ本編からロビー");
                Check(MenuScenePolicy.CanReturnToLobby("InGameScene", true), "共有シーンのソロ本編からロビー");

                foreach (string scene in new[] { "LobbyScene", "CharacterSelectScene", "InGameScene", "ResultScene", "TitleScene" })
                {
                    Check(!MenuScenePolicy.CanReturnToLobby(scene, false), "オンライン・対象外シーンではロビーへ戻れない");
                }

                Transform confirmation = manager.transform.Find("MenuConfirmation");

                menu.Find("Panel/QuitBorder/Quit").GetComponent<Button>().onClick.Invoke();
                Check(confirmation.gameObject.activeSelf && Application.isPlaying, "終了前に確認");
                manager.Toggle();
                Check(!confirmation.gameObject.activeSelf && menu.gameObject.activeSelf, "確認のEscはキャンセル");
                menu.Find("Panel/QuitBorder/Quit").GetComponent<Button>().onClick.Invoke();
                confirmation.Find("Panel/Cancel").GetComponent<Button>().onClick.Invoke();
                Check(!confirmation.gameObject.activeSelf && manager.IsOpen, "確認ボタンでキャンセル");
                manager.Toggle();
                Check(!manager.IsOpen, "メニューを閉じる");
                manager.Open();
                menu.Find("Panel/SettingsBorder/Settings").GetComponent<Button>().onClick.Invoke();

                foreach (Button tab in root.Find("Tabs").GetComponentsInChildren<Button>(true))
                {
                    tab.onClick.Invoke();
                    await Awaitable.NextFrameAsync();
                }

                root.Find("Tabs/controls").GetComponent<Button>().onClick.Invoke();
                Check(root.Find("KeyCapture") == null, "キー入力用ポップアップを生成しない");

                foreach (string mode in new[] { "mapDisplayMode", "walkMode", "stopMode" })
                {
                    root.Find("Tabs/controls").GetComponent<Button>().onClick.Invoke();
                    Button binding = root.Find(
                        "controls/Viewport/Content/controls." + mode +
                        "/Control/Key/Binding").GetComponent<Button>();
                    TMP_Text bindingLabel = binding.GetComponentInChildren<TMP_Text>();
                    string previousLabel = bindingLabel.text;
                    string previousSettings = SettingsJson.Serialize(SettingsManager.Current);

                    binding.onClick.Invoke();
                    Check(
                        bindingLabel.text == "" && binding.transform.Find("Caret").gameObject.activeSelf,
                        "キー欄を空にしてカーソルを表示");
                    Check(!UnityEngine.EventSystems.EventSystem.current.sendNavigationEvents, "キー待機中のUI操作を抑止");
                    root.Find("Tabs/general").GetComponent<Button>().onClick.Invoke();
                    Check(
                        bindingLabel.text == previousLabel &&
                        !binding.transform.Find("Caret").gameObject.activeSelf,
                        "タブ移動でキャンセルし表示を復元");
                    Check(
                        previousSettings == SettingsJson.Serialize(SettingsManager.Current),
                        "キャンセル時の設定値を維持");
                    await Awaitable.NextFrameAsync();
                }

                Button general = root.Find("Tabs/general").GetComponent<Button>();

                general.onClick.Invoke();
                Transform content = root.Find("general/Viewport/Content");

                content.Find("general.minimapMode/Control/fixed").GetComponent<Button>().onClick.Invoke();
                float sensitivity = SettingsManager.Current.MouseSensitivity;
                TMP_InputField zoom = content.Find("general.minimapZoom/Control").GetComponentInChildren<TMP_InputField>(
                    true);

                zoom.onEndEdit.Invoke("1.75");
                Check(
                    SettingsManager.Current.MinimapZoom == 1.75f &&
                    SettingsManager.Current.MouseSensitivity == sensitivity,
                    "倍率と感度の接続先");
                content.Find("general.minimapMode/Control/variable").GetComponent<Button>().onClick.Invoke();
                Check(!zoom.GetComponentInParent<CanvasGroup>().interactable, "可変時の倍率無効化");
                Transform[] before = root.GetComponentsInChildren<Transform>(true);

                manager.Close();
                manager.Open();
                Check(menu == manager.transform.Find("GameMenu"), "メニューを再生成しないこと");
                menu.Find("Panel/SettingsBorder/Settings").GetComponent<Button>().onClick.Invoke();

                foreach (Button tab in root.Find("Tabs").GetComponentsInChildren<Button>(true))
                {
                    tab.onClick.Invoke();
                }

                general.onClick.Invoke();
                await Awaitable.NextFrameAsync();
                Transform[] after = root.GetComponentsInChildren<Transform>(true);

                Check(before.SequenceEqual(after), "開閉とタブ切替でUIを再生成しないこと");
                Canvas.ForceUpdateCanvases();

                foreach (Scrollbar scrollbar in root.GetComponentsInChildren<Scrollbar>(true))
                {
                    Check(scrollbar.handleRect.rect.width <= 10.0f, "スクロールバー幅");
                }

                System.IO.File.WriteAllText(
                    "Temp/SettingsUiValidation.txt",
                    "PASS: menu navigation, settings bindings, retained instances and scrollbar layout.");
                Debug.Log(
                    "PASS: menu navigation, settings bindings, retained instances and scrollbar layout.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                UserSettings original = SettingsJson.Deserialize(originalJson, out bool migrated);

                SettingsManager.Current.General = original.General;
                SettingsManager.Current.Controls = original.Controls;
                SettingsManager.Current.Audio = original.Audio;
                SettingsManager.Save();
                manager.Close();
                manager.Open();
            }

            await Awaitable.EndOfFrameAsync();
            ScreenCapture.CaptureScreenshot("Temp/Settings-General.png");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("設定UI検証失敗: " + message);
            }
        }
    }
}
