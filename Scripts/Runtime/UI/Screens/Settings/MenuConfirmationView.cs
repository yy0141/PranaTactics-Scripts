using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// シーン離脱とゲーム終了に共通の確認画面を保持します。
    /// </summary>
    public sealed class MenuConfirmationView
    {
        private readonly RectTransform _root;

        private readonly TMP_Text _message;

        private Action _confirmed;

        private readonly Button _cancel;

        public bool IsOpen => _root.gameObject.activeSelf;

        public MenuConfirmationView(Transform parent, TMP_FontAsset font)
        {
            var elements = new SettingsUiElements(font);

            _root = elements.Panel("MenuConfirmation", parent, new Color(0, 0, 0, 0.8f)).rectTransform;
            SettingsUiElements.Stretch(_root);
            RectTransform panel = elements.Panel("Panel", _root, new Color(0.063f, 0.094f, 0.122f)).rectTransform;

            SettingsUiElements.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-300, -140), new Vector2(300, 140));
            _message = elements.Text("Message", panel, "", 26);
            _message.alignment = TextAlignmentOptions.Center;
            _message.textWrappingMode = TextWrappingModes.Normal;
            SettingsUiElements.Place(
                _message.rectTransform,
                Vector2.zero,
                Vector2.one,
                new Vector2(30, 100),
                new Vector2(
                -30,
                -25));
            Button cancel = elements.Button("Cancel", panel, "キャンセル");

            _cancel = cancel;
            SettingsUiElements.Place(
                (RectTransform)cancel.transform,
                Vector2.zero,
                new Vector2(0.5f, 0),
                new Vector2(
                30,
                30),
                new Vector2(
                -10,
                85));
            cancel.onClick.AddListener(Hide);
            Button confirm = elements.Button("Confirm", panel, "はい");

            confirm.image.color = new Color(0.5f, 0.2f, 0.25f);
            SettingsUiElements.Place(
                (RectTransform)confirm.transform,
                new Vector2(0.5f, 0),
                Vector2.right,
                new Vector2(
                10,
                30),
                new Vector2(
                -30,
                85));
            confirm.onClick.AddListener(Confirm);
            cancel.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnRight = confirm };
            confirm.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnLeft = cancel };
            Hide();
        }

        /// <summary>
        /// 実行する操作を保持して確認を表示します。
        /// </summary>
        public void Show(string message, Action confirmed)
        {
            _message.text = message;
            _confirmed = confirmed;
            _root.gameObject.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(_cancel.gameObject);
        }

        /// <summary>
        /// 保留中の操作を破棄して閉じます。
        /// </summary>
        public void Hide()
        {
            _confirmed = null;
            _root.gameObject.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        /// <summary>
        /// 二重実行を防いで承認された操作を実行します。
        /// </summary>
        private void Confirm()
        {
            Action action = _confirmed;

            Hide();
            action?.Invoke();
        }
    }
}
