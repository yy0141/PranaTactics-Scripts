using System;
using TMPro;
using UnityEngine;

using PranaTactics.Core;

namespace PranaTactics.Tutorial
{
    [Serializable]
    public sealed class TutorialInputBinding
    {
        public KeyCode Key;

        [Tooltip("空欄ならキー名を自動表示します。")]
        public string DisplayName;

        public TutorialInputBinding(KeyCode key)
        {
            Key = key;
        }

        public string Label => !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : Key == KeyCode.Mouse0 ? "左クリック" : Key == KeyCode.Mouse1 ? "右クリック" : Key == KeyCode.Return ? "Enter" : Key.ToString();
    }

    [Serializable]
    public sealed class TutorialInputBindings
    {
        public TutorialInputBinding Confirm = new TutorialInputBinding(KeyCode.Mouse0);

        public TutorialInputBinding Menu = new TutorialInputBinding(KeyCode.F1);

        public TutorialInputBinding Document = new TutorialInputBinding(KeyCode.F2);

        public string Help => Menu.Label + "：補助メニュー   " + Document.Label + "：兵装資料";

        public string Format(string text) => text.Replace("{confirm}", Confirm.Label).Replace(
            "{menu}",
            Menu.Label).Replace(
            "{document}",
            Document.Label);
    }

    [Serializable]
    public sealed class TutorialElementStyle
    {
        public Vector2 Position;

        public Vector2 Size;

        public Vector2 Anchor = new Vector2(0.5f, 0.5f);

        public Vector2 Pivot = new Vector2(0.5f, 0.5f);

        public float FontSize = 22;

        public Color Background = new Color(0.025f, 0.05f, 0.085f, 0.96f);

        public Color TextColor = Color.white;

        public TutorialElementStyle(float x, float y, float w, float h, float font = 22)
        {
            Position = new Vector2(x, y);
            Size = new Vector2(w, h);
            FontSize = font;
        }
    }

    [Serializable]
    public sealed class TutorialTheme
    {
        public Color BlockedClimbOutlineColor = new Color(1f, 0.25f, 0.2f, 1f);

        [Range(0f, 10f)]
        public float BlockedClimbOutlineWidth = 6f;

        public Vector2 ReferenceResolution = new Vector2(1600, 900);

        [Range(0, 1)]
        public float ScreenMatch = 0.5f;

        public int SortingOrder = 100;

        public Color LoadingColor = new Color(0.025f, 0.05f, 0.085f, 1);

        public Vector2 ButtonTextPadding = new Vector2(12, 8);

        [Range(0.1f, 1)]
        public float MinimumFontRatio = 0.75f;

        public Vector4 ObjectivePadding = new Vector4(20, 20, 16, 16);

        public float ObjectiveSpacing = 14;

        [Tooltip("説明送りアイコンの幅(X)と高さ(Y)。Canvasの基準解像度上のサイズです。")]
        public Vector2 ExplanationIconSize = new Vector2(72, 72);

        [Tooltip("パネル内側の右下からの余白。Xを増やすと左へ、Yを増やすと上へ移動します。")]
        public Vector2 ExplanationIconInset = new Vector2(0, 0);

        public string ContinueCaption = "次へ";

        [Tooltip("説明送りの専用行を基準とするラベルの配置・サイズ・文字色です。")]
        public TutorialElementStyle ContinueCaptionStyle = new TutorialElementStyle(0, 76, 72, 26, 18)
        {
            Anchor = new Vector2(1, 0),
            Pivot = new Vector2(1, 0)
        };

        public Color ProgressColor = new Color(0.65f, 0.9f, 0.65f, 1);

        public Color ProgressTrackColor = new Color(0.3f, 0.42f, 0.34f, 1);

        [Min(1)]
        public float ProgressHeight = 34;

        [Min(1)]
        public float ProgressNodeSize = 26;

        [Min(1)]
        public float ProgressLineWidth = 3;

        public TutorialElementStyle ProgressPanel = new TutorialElementStyle(200, 400, 540, 60);

        public TutorialElementStyle ObjectivePanel = new TutorialElementStyle(20, -20, 480, 0)
        {
            Anchor = new Vector2(0, 1),
            Pivot = new Vector2(0, 1)
        };

        public TutorialElementStyle Title = new TutorialElementStyle(0, 0, 440, 50, 26);

        public TutorialElementStyle Body = new TutorialElementStyle(0, 0, 430, 210, 21);

        public TutorialElementStyle Objective = new TutorialElementStyle(0, 0, 430, 70, 21);

        public TutorialElementStyle Status = new TutorialElementStyle(0, 0, 440, 45, 18);

        public TutorialElementStyle MenuPanel = new TutorialElementStyle(535, 40, 480, 410);

        public TutorialElementStyle MenuTitle = new TutorialElementStyle(0, 165, 450, 45, 24);

        public TutorialElementStyle Previous = Button(0, 100, 430, 48);

        public TutorialElementStyle Retry = Button(0, 42, 430, 48);

        public TutorialElementStyle Plan = Button(0, -74, 430, 48);

        public TutorialElementStyle Leave = Button(0, -148, 450, 48);

        public TutorialElementStyle DocumentPanel = new TutorialElementStyle(480, 130, 600, 610);

        public TutorialElementStyle DocumentText = new TutorialElementStyle(0, 0, 560, 570, 22);

        private static TutorialElementStyle Button(float x, float y, float w, float h) => new TutorialElementStyle(
            x,
            y,
            w,
            h)
        {
            Background = new Color(0.10f, 0.28f, 0.35f, 1)
        };
    }

    [CreateAssetMenu(menuName = "Prana Tactics/Tutorial/Presentation Settings")]
    public sealed class TutorialUiResources : ScriptableObject
    {
        [SerializeField]
        private TMP_FontAsset _font;

        [SerializeField]
        private Texture2D _explanationIcon;

        public Texture2D ExplanationIcon => _explanationIcon;

        public TutorialInputBindings InputBindings = new TutorialInputBindings();

        public TutorialTheme Theme = new TutorialTheme();

        private static TutorialUiResources s_instance;

        public static TutorialUiResources Current
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = GameDataRepository.Instance.GetTutorialUiData();
                }

                return s_instance;
            }
        }

        public static TMP_FontAsset Font => Current._font != null ? Current._font : TMP_Settings.defaultFontAsset;
    }
}
