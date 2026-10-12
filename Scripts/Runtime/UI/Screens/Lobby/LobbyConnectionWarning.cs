using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// ロビー上にネットワーク未接続の警告を表示します。
    /// </summary>
    public static class LobbyConnectionWarning
    {
        private const string k_Message = "ネットワークに接続されていません";

        /// <summary>
        /// クリックで閉じる警告を生成し、所有者へ返します。
        /// </summary>
        public static GameObject Show(Transform owner)
        {
            Canvas canvas = owner.GetComponentInParent<Canvas>().rootCanvas;
            var overlay = new GameObject(
                "NetworkConnectionWarning",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));

            overlay.transform.SetParent(canvas.transform, false);
            var bounds = (RectTransform)overlay.transform;

            bounds.anchorMin = Vector2.zero;
            bounds.anchorMax = Vector2.one;
            bounds.offsetMin = Vector2.zero;
            bounds.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
            Button dismiss = overlay.GetComponent<Button>();

            dismiss.onClick.AddListener(() => Object.Destroy(overlay));

            var label = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));

            label.transform.SetParent(overlay.transform, false);
            var labelBounds = (RectTransform)label.transform;

            labelBounds.anchorMin = new Vector2(0.05f, 0.3f);
            labelBounds.anchorMax = new Vector2(0.95f, 0.7f);
            labelBounds.offsetMin = Vector2.zero;
            labelBounds.offsetMax = Vector2.zero;
            TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset sceneFont = null;

            foreach (TMP_Text source in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if (source == text || source.font == null)
                {
                    continue;
                }

                if (sceneFont == null)
                {
                    sceneFont = source.font;
                }

                if (source.font.HasCharacters(k_Message))
                {
                    sceneFont = source.font;
                    break;
                }
            }

            if (sceneFont != null)
            {
                text.font = sceneFont;
            }

            text.text = k_Message + "\n\nクリックして閉じる";
            text.fontSize = 36.0f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18.0f;
            text.fontSizeMax = 36.0f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            return overlay;
        }
    }
}
