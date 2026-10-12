using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 設定への入口となるメニューを一度だけ生成し、表示を再利用します。
    /// </summary>
    public sealed class GameMenuView
    {
        public RectTransform Root
        {
            get;
        }

        private readonly RectTransform _tutorial;

        private readonly RectTransform _back;

        private readonly RectTransform _quit;

        public GameMenuView(
            Transform parent,
            TMP_FontAsset font,
            Action openSettings,
            Action close,
            Action quit,
            Action tutorial,
            Action back)
        {
            var elements = new SettingsUiElements(font);

            Root = elements.Panel("GameMenu", parent, new Color(0.015f, 0.025f, 0.04f, 0.78f)).rectTransform;
            SettingsUiElements.Stretch(Root);
            RectTransform panel = elements.Panel("Panel", Root, new Color(0.063f, 0.094f, 0.122f, 1.0f)).rectTransform;

            SettingsUiElements.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-250, -219), new Vector2(250, 219));
            TMP_Text title = elements.Text("Title", panel, "メニュー", 36);

            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            SettingsUiElements.Place(
                title.rectTransform,
                Vector2.up,
                Vector2.one,
                new Vector2(160, -88),
                new Vector2(
                -160,
                -24));
            CreateLine(elements, panel, 68, 156);
            CreateLine(elements, panel, 340, 428);
            Button closeButton = elements.Button("Close", panel, "");

            closeButton.image.color = Color.clear;
            closeButton.GetComponentInChildren<TMP_Text>().fontSize = 44;
            SettingsUiElements.Place((RectTransform)closeButton.transform, Vector2.one, Vector2.one,
                new Vector2(-70, -82), new Vector2(-18, -26));
            closeButton.onClick.AddListener(() => close());

            for (int i = 0; i < 2; i++)
            {
                Image stroke = elements.Panel("Cross", closeButton.transform, new Color(0.9f, 0.94f, 0.96f));

                stroke.raycastTarget = false;
                SettingsUiElements.Place(stroke.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(
                    0.5f,
                    0.5f),
                    new Vector2(-18, -1.5f), new Vector2(18, 1.5f));
                stroke.rectTransform.localRotation = Quaternion.Euler(0, 0, i == 0 ? 45 : -45);
            }

            CreateButton(elements, panel, "Settings", "設定", 118, false, openSettings);
            _tutorial = CreateButton(elements, panel, "Tutorial", "チュートリアル", 190, false, tutorial);
            _back = CreateButton(elements, panel, "BackToLobby", "ロビーへ戻る", 190, false, back);
            _quit = CreateButton(elements, panel, "Quit", "デスクトップへ戻る", 262, true, quit);
            TMP_Text version = elements.Text("Version", panel, Application.version, 13);

            version.alignment = TextAlignmentOptions.Center;
            version.color = new Color(0.58f, 0.62f, 0.65f);
            SettingsUiElements.Place(
                version.rectTransform,
                Vector2.zero,
                Vector2.right,
                new Vector2(24, 18),
                new Vector2(
                -24,
                42));
            Root.gameObject.SetActive(false);
        }

        /// <summary>
        /// 見出しの両側に細い装飾線を配置します。
        /// </summary>
        private static void CreateLine(SettingsUiElements elements, Transform parent, float left, float right)
        {
            Image line = elements.Panel("Divider", parent, new Color(0.65f, 0.69f, 0.71f, 0.3f));

            line.raycastTarget = false;
            SettingsUiElements.Place(
                line.rectTransform,
                Vector2.up,
                Vector2.up,
                new Vector2(left, -55),
                new Vector2(
                right,
                -54));
        }

        /// <summary>
        /// 現在のシーンで利用可能な操作だけを表示します。
        /// </summary>
        public void Configure(bool tutorial, bool back, bool quit)
        {
            _tutorial.gameObject.SetActive(tutorial);
            _back.gameObject.SetActive(back);
            _quit.gameObject.SetActive(quit);
            float top = tutorial || back ? 262 : 190;

            SettingsUiElements.Place(_quit, Vector2.up, Vector2.one,
                new Vector2(108, -top - 52), new Vector2(-108, -top));
        }

        /// <summary>
        /// 枠線付きのメニュー操作を配置します。
        /// </summary>
        private static RectTransform CreateButton(SettingsUiElements elements, Transform parent, string name, string label,
            float top, bool isExit, Action action)
        {
            Image border = elements.Panel(name + "Border", parent, isExit
                ? new Color(1.0f, 0.28f, 0.37f) : new Color(0.31f, 0.35f, 0.38f));

            SettingsUiElements.Place(border.rectTransform, Vector2.up, Vector2.one,
                new Vector2(108, -top - 52), new Vector2(-108, -top));
            Button button = elements.Button(name, border.transform, label);

            button.image.color = isExit ? new Color(0.31f, 0.23f, 0.26f) : new Color(0.15f, 0.18f, 0.21f);
            button.GetComponentInChildren<TMP_Text>().fontSize = 22;
            SettingsUiElements.Place(
                (RectTransform)button.transform,
                Vector2.zero,
                Vector2.one,
                Vector2.one,
                -Vector2.one);
            button.onClick.AddListener(() => action());

            return border.rectTransform;
        }
    }
}
