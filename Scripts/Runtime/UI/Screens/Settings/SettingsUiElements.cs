using TMPro;
using UnityEngine;
using UnityEngine.UI;

using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 設定画面で共通の配置、文字、配色を生成します。
    /// </summary>
    public sealed class SettingsUiElements
    {
        private readonly TMP_FontAsset _font;

        public static Color Accent => new Color(0.47f, 0.86f, 0.8f, 1.0f);

        public static Color Surface => new Color(0.65f, 0.72f, 0.8f, 0.12f);

        public SettingsUiElements(TMP_FontAsset font)
        {
            _font = font;
        }

        public RectTransform Rect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));

            gameObject.layer = 5;
            var rect = (RectTransform)gameObject.transform;

            rect.SetParent(parent, false);

            return rect;
        }

        public static void Place(
            RectTransform rect,
            Vector2 minimum,
            Vector2 maximum,
            Vector2 offsetMinimum,
            Vector2 offsetMaximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = offsetMinimum;
            rect.offsetMax = offsetMaximum;
            rect.localScale = Vector3.one;
        }

        public static void Stretch(RectTransform rect)
        {
            Place(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        public Image Panel(string name, Transform parent, Color color)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();

            image.color = color;

            return image;
        }

        public TMP_Text Text(string name, Transform parent, string value, float size = 24.0f)
        {
            RectTransform rect = Rect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();

            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.color = new Color(0.9f, 0.94f, 0.96f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(rect);

            return text;
        }

        public Button Button(string name, Transform parent, string caption)
        {
            Image image = Panel(name, parent, Surface);
            Button button = image.gameObject.AddComponent<Button>();

            button.targetGraphic = image;
            ColorBlock colors = button.colors;

            colors.highlightedColor = new Color(0.7f, 1.0f, 0.94f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            TMP_Text text = Text("Label", image.transform, caption);

            text.alignment = TextAlignmentOptions.Center;

            return button;
        }

        public TMP_InputField Input(Transform parent)
        {
            Image background = Panel("Number", parent, new Color(0.04f, 0.07f, 0.1f, 0.7f));
            TMP_InputField input = background.gameObject.AddComponent<TMP_InputField>();

            RectTransform viewport = Rect("Viewport", background.transform);

            Place(viewport, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-10, -4));
            viewport.gameObject.AddComponent<RectMask2D>();
            TMP_Text text = Text("Value", viewport, "");

            text.alignment = TextAlignmentOptions.Center;
            input.textViewport = viewport;
            input.textComponent = (TextMeshProUGUI)text;
            input.targetGraphic = background;
            input.contentType = TMP_InputField.ContentType.DecimalNumber;
            input.characterLimit = 12;
            input.restoreOriginalTextOnEscape = true;

            return input;
        }

        public TMP_Dropdown Dropdown(Transform parent)
        {
            Image background = Panel("Dropdown", parent, Surface);
            TMP_Dropdown dropdown = background.gameObject.AddComponent<TMP_Dropdown>();

            dropdown.targetGraphic = background;
            TMP_Text caption = Text("Caption", background.transform, "");

            Place(caption.rectTransform, Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-44, 0));
            dropdown.captionText = caption;
            TMP_Text arrow = Text("Arrow", background.transform, "▼");

            Place(
                arrow.rectTransform,
                new Vector2(1, 0),
                Vector2.one,
                new Vector2(-38, 0),
                new Vector2(-8, 0));
            Image template = Panel("Template", background.transform, new Color(0.08f, 0.13f, 0.17f, 1));
            RectTransform templateRect = template.rectTransform;

            templateRect.pivot = new Vector2(0.5f, 1);
            Place(templateRect, Vector2.zero, new Vector2(1, 0), new Vector2(0, -210), Vector2.zero);
            ScrollRect scroll = template.gameObject.AddComponent<SettingsScrollRect>();
            RectTransform viewport = Rect("Viewport", template.transform);

            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("Content", viewport);

            content.pivot = new Vector2(0.5f, 1);
            Place(content, new Vector2(0, 1), Vector2.one, new Vector2(0, -52), Vector2.zero);
            Image item = Panel("Item", content, Surface);

            Place(
                item.rectTransform,
                new Vector2(0, 0.5f),
                new Vector2(1, 0.5f),
                new Vector2(0, -26),
                new Vector2(
                0,
                26));
            UnityEngine.UI.Toggle toggle = item.gameObject.AddComponent<UnityEngine.UI.Toggle>();

            toggle.targetGraphic = item;
            Image selected = Panel("Selected", item.transform, new Color(0.3f, 0.6f, 0.56f, 0.6f));

            Stretch(selected.rectTransform);
            selected.raycastTarget = false;
            toggle.graphic = selected;
            TMP_Text label = Text("Label", item.transform, "");

            Place(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-18, 0));
            dropdown.itemText = label;
            dropdown.template = templateRect;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            template.gameObject.SetActive(false);

            return dropdown;
        }

        public Slider Slider(Transform parent)
        {
            RectTransform root = Panel("Slider", parent, Color.clear).rectTransform;
            Image track = Panel("Track", root, new Color(0.5f, 0.56f, 0.6f, 0.7f));

            Place(
                track.rectTransform,
                new Vector2(0, 0.5f),
                new Vector2(1, 0.5f),
                new Vector2(10, -2),
                new Vector2(
                -10,
                2));
            RectTransform area = Rect("HandleArea", root);

            Place(
                area,
                new Vector2(0, 0.5f),
                new Vector2(1, 0.5f),
                new Vector2(10, -12),
                new Vector2(-10, 12));
            Image handle = Panel("Handle", area, Accent);

            handle.rectTransform.sizeDelta = new Vector2(12, 0);
            Slider slider = root.gameObject.AddComponent<Slider>();

            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;

            return slider;
        }
    }
}
