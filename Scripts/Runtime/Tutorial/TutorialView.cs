using System;

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PranaTactics.Tutorial
{
    /// <summary>
    /// 訓練表示のみを担当し、状態遷移や達成判定は行いません。
    /// </summary>
    public sealed class TutorialView : MonoBehaviour
    {
        private static TutorialTheme Theme => TutorialUiResources.Current.Theme;

        private static TutorialInputBindings Bindings => TutorialUiResources.Current.InputBindings;

        private GameObject _menu;

        private GameObject _objectivePanel;

        private GameObject _loadingBackdrop;

        private TMP_Text _title;

        private TMP_Text _body;

        private TMP_Text _objective;

        private TMP_Text _status;

        private TMP_Text _documentText;

        private GameObject _plan;

        private GameObject _document;

        private GameObject _checklist;

        private TutorialProgressGraphic _progress;

        private GameObject _progressPanel;

        private GameObject _explanationHint;

        private readonly GameObject[] _checkmarks = new GameObject[3];

        private readonly System.Collections.Generic.List<Button> _lessonButtons = new System.Collections.Generic.List<Button>();

        public event Action Retry, Previous, Plan, Leave;

        public static TutorialView Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = CreateCanvas("Tutorial UI", parent);
            TutorialView view = root.AddComponent<TutorialView>();

            view.Build(font);

            return view;
        }

        /// <remarks>
        /// <para>固定座標ではなくTMPの折り返し後の必要高さで並べ、隣の文章へはみ出させない。</para>
        /// </remarks>
        private void Build(TMP_FontAsset font)
        {
            _loadingBackdrop = Panel(transform, "Loading backdrop", Theme.ObjectivePanel);
            RectTransform backdropRect = (RectTransform)_loadingBackdrop.transform;

            backdropRect.anchorMin = backdropRect.anchorMax = new Vector2(0, 0.5f);
            backdropRect.offsetMin = backdropRect.offsetMax = Vector2.zero;
            _loadingBackdrop.GetComponent<Image>().color = Theme.LoadingColor;
            _objectivePanel = Panel(transform, "Objective", Theme.ObjectivePanel);
            _objectivePanel.GetComponent<Image>().raycastTarget = false;
            _title = Text(_objectivePanel.transform, "準備中", Theme.Title, font);
            _progressPanel = Panel(transform, "Progress panel", Theme.ProgressPanel);
            _progressPanel.GetComponent<Image>().raycastTarget = false;
            var progressObject = new GameObject(
                "Lesson progress",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TutorialProgressGraphic));
            RectTransform progressRect = (RectTransform)progressObject.transform;

            progressRect.SetParent(_progressPanel.transform, false);
            RectTransform progressPanelRect = (RectTransform)_progressPanel.transform;

            progressPanelRect.anchorMin = progressPanelRect.anchorMax = new Vector2(0.5f, 0);
            progressRect.sizeDelta = new Vector2(
                Theme.ProgressPanel.Size.x - Theme.ObjectivePadding.x - Theme.ObjectivePadding.y,
                Mathf.Max(
                Theme.ProgressHeight,
                Theme.ProgressNodeSize));
            _progress = progressObject.GetComponent<TutorialProgressGraphic>();
            _progress.SetProgress(TutorialStep.Briefing, new bool[(int)TutorialStep.Complete - 1], Theme);
            _body = Text(_objectivePanel.transform, "", Theme.Body, font);
            BuildChecklist(font);
            _objective = Text(_objectivePanel.transform, "", Theme.Objective, font);
            _status = Text(_objectivePanel.transform, Bindings.Help, Theme.Status, font);
            BuildExplanationHint(font);
            VerticalLayoutGroup layout = _objectivePanel.AddComponent<VerticalLayoutGroup>();

            layout.padding = new RectOffset(
                (int)Theme.ObjectivePadding.x,
                (int)Theme.ObjectivePadding.y,
                (int)Theme.ObjectivePadding.z,
                (int)Theme.ObjectivePadding.w);
            layout.spacing = Theme.ObjectiveSpacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            foreach (TMP_Text label in new[]
            {
                _title,
                _body,
                _objective,
                _status
            }

            )
            {
                label.enableAutoSizing = false;
                label.alignment = TextAlignmentOptions.TopLeft;
            }

            _objectivePanel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _menu = Panel(transform, "Lesson options", Theme.MenuPanel);
            Text(_menu.transform, "訓練メニュー / " + Bindings.Menu.Label + "で閉じる", Theme.MenuTitle, font);
            Button(_menu.transform, "前の課題へ", Theme.Previous, font, () => Previous?.Invoke());
            Button(_menu.transform, "この課題を再挑戦", Theme.Retry, font, () => Retry?.Invoke());
            _plan = Button(_menu.transform, "経路を引き直す", Theme.Plan, font, () => Plan?.Invoke()).gameObject;
            _lessonButtons.AddRange(_menu.GetComponentsInChildren<Button>(true));
            Button(_menu.transform, "訓練をスキップしてロビーへ", Theme.Leave, font, () => Leave?.Invoke());
            _document = Panel(transform, "Weapon reference", Theme.DocumentPanel);
            _documentText = Text(_document.transform, "", Theme.DocumentText, font);
            _documentText.alignment = TextAlignmentOptions.TopLeft;
            _documentText.enableAutoSizing = true;
            _document.SetActive(false);
            Loading("ローカルの訓練場を読み込んでいます。");
            SetMenu(false);
        }

        public void Loading(string message)
        {
            _explanationHint.SetActive(false);
            _status.gameObject.SetActive(true);
            _progressPanel.SetActive(false);
            _loadingBackdrop.SetActive(true);
            _title.text = "訓練シミュレーター";
            _body.gameObject.SetActive(true);
            _body.text = message;

            foreach (Button button in _lessonButtons)
            {
                button.interactable = false;
            }

            _plan.SetActive(false);
        }

        public void Present(string title, string body, string objective, bool canPlan)
        {
            _explanationHint.SetActive(false);
            _status.gameObject.SetActive(true);
            _progressPanel.SetActive(true);
            _checklist.SetActive(false);
            _loadingBackdrop.SetActive(false);

            foreach (Button button in _lessonButtons)
            {
                button.interactable = true;
            }

            _title.text = Bindings.Format(title);
            _body.text = Bindings.Format(body);
            _objective.text = Bindings.Format(objective);
            _body.gameObject.SetActive(!string.IsNullOrWhiteSpace(body));
            _objective.gameObject.SetActive(!string.IsNullOrWhiteSpace(objective));
            _status.text = Bindings.Help;
            _plan.SetActive(canPlan);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_objectivePanel.transform);
        }

        public void ShowDocument(string details)
        {
            _documentText.text = details;
            _document.SetActive(true);
        }

        public void ShowExplanationHint(string action)
        {
            bool showIcon = Bindings.Confirm.Key == KeyCode.Mouse0 &&
                TutorialUiResources.Current.ExplanationIcon != null;

            _explanationHint.SetActive(showIcon);
            _status.gameObject.SetActive(!showIcon);

            if (!showIcon)
            {
                _status.text = Bindings.Confirm.Label + "：" + action;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_objectivePanel.transform);
        }

        public void HideContinueHint()
        {
            _explanationHint.SetActive(false);
            _status.gameObject.SetActive(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_objectivePanel.transform);
        }

        /// <remarks>
        /// <para>レイアウト内に専用行を確保し、本文と重ならないようにします。</para>
        /// </remarks>
        private void BuildExplanationHint(TMP_FontAsset font)
        {
            _explanationHint = new GameObject(
                "Explanation hint",
                typeof(RectTransform),
                typeof(LayoutElement));
            _explanationHint.transform.SetParent(_objectivePanel.transform, false);
            LayoutElement space = _explanationHint.GetComponent<LayoutElement>();

            space.preferredHeight = Mathf.Max(
                Theme.ExplanationIconSize.y + Theme.ExplanationIconInset.y,
                Theme.ContinueCaptionStyle.Size.y +
                Mathf.Max(
                0,
                Theme.ContinueCaptionStyle.Position.y));
            var icon = new GameObject(
                "Left click icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            RectTransform rect = icon.GetComponent<RectTransform>();

            rect.SetParent(_explanationHint.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(-Theme.ExplanationIconInset.x, Theme.ExplanationIconInset.y);
            rect.sizeDelta = Theme.ExplanationIconSize;
            RawImage image = icon.GetComponent<RawImage>();

            image.texture = TutorialUiResources.Current.ExplanationIcon;
            image.raycastTarget = false;
            TMP_Text caption = Text(
                _explanationHint.transform,
                Theme.ContinueCaption,
                Theme.ContinueCaptionStyle,
                font);

            caption.name = "Next caption";
            caption.enableAutoSizing = false;
            caption.alignment = TextAlignmentOptions.Center;
            _explanationHint.SetActive(false);
        }

        public void HideDocument()
        {
            _document.SetActive(false);
        }

        public void Complete(bool final)
        {
            _objective.gameObject.SetActive(true);
            _objective.text = final ? "訓練完了！" : "課題達成！";
        }

        public void ShowContinueHint(TutorialStep step)
        {
            string action = step == TutorialStep.Briefing ? "次の説明へ" : step == TutorialStep.Complete ? "ロビーへ戻る" : step == TutorialStep.Plan ? "計画を実行する" : "次の課題へ進む";

            ShowExplanationHint(action);
        }

        public void ShowPlanConfirmation()
        {
            _objective.text = "経路設計が完了しました。";
            ShowContinueHint(TutorialStep.Plan);
        }

        public void SetMenu(bool open)
        {
            _menu.SetActive(open);
        }

        public void SetExerciseChecklist(bool planned, bool moved, bool hit)
        {
            _checklist.SetActive(true);
            _checkmarks[0].SetActive(planned);
            _checkmarks[1].SetActive(moved);
            _checkmarks[2].SetActive(hit);
        }

        public void ShowExerciseChecklist(bool visible)
        {
            _checklist.SetActive(visible);
        }

        public void SetProgress(TutorialStep step, bool[] completed)
        {
            _progressPanel.SetActive(true);
            _progress.gameObject.SetActive(true);
            _progress.SetProgress(step, completed, Theme);
        }

        /// <remarks>
        /// <para>フォントに依存しない四角枠とチェックマーク。</para>
        /// </remarks>
        private void BuildChecklist(TMP_FontAsset font)
        {
            _checklist = new GameObject(
                "Exercise checklist",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            _checklist.transform.SetParent(_objectivePanel.transform, false);
            VerticalLayoutGroup list = _checklist.GetComponent<VerticalLayoutGroup>();

            list.spacing = Theme.ObjectiveSpacing;
            list.childControlWidth = list.childControlHeight = true;
            list.childForceExpandHeight = false;
            string[] captions =
            {
                "行動力10で経路を作る",
                "計画を実行し2マス移動",
                "移動後に標的へ命中"
            };
            float size = Theme.Body.FontSize;

            for (int i = 0; i < captions.Length; i++)
            {
                var row = new GameObject(
                    "Task " + i,
                    typeof(RectTransform),
                    typeof(HorizontalLayoutGroup),
                    typeof(LayoutElement));

                row.transform.SetParent(_checklist.transform, false);
                row.GetComponent<LayoutElement>().preferredHeight = size * 1.6f;
                HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();

                layout.spacing = Theme.ObjectiveSpacing;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                var box = new GameObject("Checkbox", typeof(RectTransform), typeof(LayoutElement));

                box.transform.SetParent(row.transform, false);
                LayoutElement bounds = box.GetComponent<LayoutElement>();

                bounds.minWidth = bounds.preferredWidth = size;
                bounds.minHeight = bounds.preferredHeight = size;
                Stroke(box.transform, new Vector2(0, size / 2), new Vector2(size, size * 0.08f), 0);
                Stroke(box.transform, new Vector2(0, -size / 2), new Vector2(size, size * 0.08f), 0);
                Stroke(box.transform, new Vector2(-size / 2, 0), new Vector2(size * 0.08f, size), 0);
                Stroke(box.transform, new Vector2(size / 2, 0), new Vector2(size * 0.08f, size), 0);
                var mark = new GameObject("Check", typeof(RectTransform));

                mark.transform.SetParent(box.transform, false);
                ((RectTransform)mark.transform).sizeDelta = Vector2.zero;
                Stroke(
                    mark.transform,
                    new Vector2(-size * 0.17f, -size * 0.08f),
                    new Vector2(
                    size * 0.4f,
                    size * 0.12f),
                    -45);
                Stroke(
                    mark.transform,
                    new Vector2(size * 0.15f, size * 0.03f),
                    new Vector2(
                    size * 0.7f,
                    size * 0.12f),
                    55);
                _checkmarks[i] = mark;
                TMP_Text label = Text(row.transform, captions[i], Theme.Body, font);

                label.alignment = TextAlignmentOptions.MidlineLeft;
                LayoutElement textLayout = label.gameObject.AddComponent<LayoutElement>();

                textLayout.minWidth = textLayout.preferredWidth = 0;
                textLayout.flexibleWidth = 1;
            }

            _checklist.SetActive(false);
        }

        private static void Stroke(Transform parent, Vector2 position, Vector2 size, float angle)
        {
            var stroke = new GameObject("Stroke", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)stroke.transform;

            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0, 0, angle);
            Image graphic = stroke.GetComponent<Image>();

            graphic.color = Theme.Body.TextColor;
            graphic.raycastTarget = false;
        }

        private static GameObject CreateCanvas(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            Canvas canvas = root.GetComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Theme.SortingOrder;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Theme.ReferenceResolution;
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = Theme.ScreenMatch;

            if (EventSystem.current == null)
            {
                new GameObject("Tutorial Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            return root;
        }

        private static GameObject Panel(Transform parent, string name, TutorialElementStyle style)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = obj.GetComponent<RectTransform>();

            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = style.Anchor;
            rect.pivot = style.Pivot;
            rect.anchoredPosition = style.Position;
            rect.sizeDelta = style.Size;
            obj.GetComponent<Image>().color = style.Background;

            return obj;
        }

        private static TMP_Text Text(
            Transform parent,
            string text,
            TutorialElementStyle style,
            TMP_FontAsset font)
        {
            var obj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = obj.GetComponent<RectTransform>();

            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = style.Anchor;
            rect.pivot = style.Pivot;
            rect.anchoredPosition = style.Position;
            rect.sizeDelta = style.Size;
            TMP_Text label = obj.GetComponent<TMP_Text>();

            label.richText = true;
            label.font = font;
            label.fontSize = style.FontSize;
            label.color = style.TextColor;
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = true;
            label.fontSizeMin = style.FontSize * Theme.MinimumFontRatio;
            label.fontSizeMax = style.FontSize;
            label.raycastTarget = false;

            return label;
        }

        private static Button Button(
            Transform parent,
            string caption,
            TutorialElementStyle style,
            TMP_FontAsset font,
            Action clicked)
        {
            GameObject obj = Panel(parent, caption, style);
            Button button = obj.AddComponent<Button>();

            button.targetGraphic = obj.GetComponent<Image>();
            button.onClick.AddListener(() => clicked());
            TMP_Text label = Text(obj.transform, caption, style, font);
            RectTransform labelRect = (RectTransform)label.transform;

            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Theme.ButtonTextPadding * 0.5f;
            labelRect.offsetMax = -Theme.ButtonTextPadding * 0.5f;

            return button;
        }
    }
}
