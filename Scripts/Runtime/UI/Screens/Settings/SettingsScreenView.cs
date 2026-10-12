using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using PranaTactics.Settings;
using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 起動時に全カテゴリを生成し、以後は表示と値だけを更新します。
    /// </summary>
    public sealed class SettingsScreenView : IDisposable
    {
        private readonly SettingsPresenter _presenter;

        private readonly SettingsUiElements _elements;

        private readonly SettingsControlFactory _factory;

        private readonly SettingsKeyCapture _keyCapture;

        private readonly List<GameObject> _pages = new List<GameObject>();

        private readonly List<Button> _tabs = new List<Button>();

        private readonly List<Action> _refreshers = new List<Action>();

        private TMP_Text _status;

        private Button _backButton;

        public RectTransform Root
        {
            get;
        }

        public SettingsScreenView(Transform parent, TMP_FontAsset font, SettingsViewDefinition definition,
            SettingsPresenter presenter, Action close, Action back)
        {
            _presenter = presenter;
            _elements = new SettingsUiElements(font);
            Image background = _elements.Panel(
                "SettingsScreen",
                parent,
                new Color(
                0.035f,
                0.055f,
                0.09f,
                0.995f));

            Root = background.rectTransform;
            SettingsUiElements.Stretch(Root);
            _keyCapture = new SettingsKeyCapture();
            _factory = new SettingsControlFactory(_elements, presenter, _keyCapture, Root);
            TMP_Text title = _elements.Text("Title", Root, "SETTINGS", 16);

            title.color = SettingsUiElements.Accent;
            SettingsUiElements.Place(
                title.rectTransform,
                new Vector2(0.07f, 1),
                new Vector2(0.3f, 1),
                new Vector2(
                0,
                -42),
                new Vector2(
                0,
                -12));
            RectTransform tabs = _elements.Rect("Tabs", Root);

            SettingsUiElements.Place(
                tabs,
                new Vector2(0.25f, 1),
                new Vector2(0.75f, 1),
                new Vector2(0, -114),
                new Vector2(
                0,
                -50));
            Image divider = _elements.Panel("Divider", Root, new Color(0.7f, 0.85f, 0.9f, 0.25f));

            SettingsUiElements.Place(
                divider.rectTransform,
                new Vector2(0.07f, 1),
                new Vector2(0.93f, 1),
                new Vector2(
                0,
                -124),
                new Vector2(
                0,
                -123));

            for (int i = 0; i < definition.Categories.Count; i++)
            {
                int index = i;
                SettingsCategoryDefinition category = definition.Categories[i];
                Button tab = _elements.Button(category.Id, tabs, category.Label);

                SettingsUiElements.Place(
                    (RectTransform)tab.transform,
                    new Vector2(
                    (float)i / definition.Categories.Count,
                    0),
                    new Vector2(
                    (float)(i +
                    1) / definition.Categories.Count,
                    1),
                    new Vector2(
                    6,
                    0),
                    new Vector2(
                    -6,
                    0));
                tab.onClick.AddListener(() => SelectPage(index));
                _tabs.Add(tab);
                _pages.Add(BuildPage(category));
            }

            RectTransform footer = _elements.Rect("Footer", Root);

            SettingsUiElements.Place(
                footer,
                new Vector2(0.07f, 0),
                new Vector2(0.93f, 0),
                new Vector2(0, 20),
                new Vector2(
                0,
                120));
            Button closeButton = _elements.Button("Close", footer, "メニューへ戻る");

            closeButton.image.color = new Color(0.2f, 0.6f, 0.55f, 0.95f);
            SettingsUiElements.Place(
                (RectTransform)closeButton.transform,
                new Vector2(0.37f, 0),
                new Vector2(
                0.63f,
                1),
                new Vector2(
                0,
                26),
                new Vector2(
                0,
                -18));
            closeButton.onClick.AddListener(() => close());
            _status = _elements.Text("Status", footer, "", 16);
            _status.alignment = TextAlignmentOptions.Center;
            SettingsUiElements.Place(
                _status.rectTransform,
                new Vector2(0.25f, 0),
                new Vector2(0.75f, 0),
                new Vector2(
                0,
                -5),
                new Vector2(
                0,
                20));
            _backButton = _elements.Button("BackToLobby", footer, "ロビーへ戻る");
            SettingsUiElements.Place(
                (RectTransform)_backButton.transform,
                new Vector2(0.79f, 0),
                Vector2.one,
                new Vector2(
                0,
                26),
                new Vector2(
                0,
                -18));
            _backButton.onClick.AddListener(() => back());
            _presenter.ValuesChanged += Refresh;
            ConfigureVisibility(false);
            SelectPage(0);
            Root.gameObject.SetActive(false);
        }

        private GameObject BuildPage(SettingsCategoryDefinition category)
        {
            RectTransform page = _elements.Rect(category.Id, Root);

            SettingsUiElements.Place(
                page,
                new Vector2(0.07f, 0),
                new Vector2(0.93f, 1),
                new Vector2(0, 145),
                new Vector2(
                0,
                -148));
            ScrollRect scroll = page.gameObject.AddComponent<SettingsScrollRect>();
            Image viewport = _elements.Panel("Viewport", page, Color.clear);

            SettingsUiElements.Place(
                viewport.rectTransform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(
                -24,
                0));
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = _elements.Rect("Content", viewport.transform);

            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            float y = 0;
            int rowIndex = 0;

            foreach (SettingsSectionDefinition section in category.Sections)
            {
                TMP_Text heading = _elements.Text(section.Id, content, section.Label, 26);

                heading.fontStyle = FontStyles.Bold;
                SettingsUiElements.Place(
                    heading.rectTransform,
                    new Vector2(0, 1),
                    Vector2.one,
                    new Vector2(
                    0,
                    -y - 64),
                    new Vector2(
                    0,
                    -y));
                y += 72;

                foreach (SettingItemDefinition item in section.Items)
                {
                    float rowHeight = item.Control == "routeColors" ? 32 + (item.AdditionalRowCount + 1) * 78 : 78;
                    Color color = rowIndex++ % 2 == 0 ? new Color(0.7f, 0.77f, 0.85f, 0.12f) : new Color(
                        0.7f,
                        0.77f,
                        0.85f,
                        0.06f);
                    Image row = _elements.Panel(item.SettingId, content, color);

                    SettingsUiElements.Place(
                        row.rectTransform,
                        new Vector2(0, 1),
                        Vector2.one,
                        new Vector2(
                        0,
                        -y - rowHeight),
                        new Vector2(
                        0,
                        -y));
                    TMP_Text label = _elements.Text("Label", row.transform, item.Label, 23);

                    SettingsUiElements.Place(
                        label.rectTransform,
                        Vector2.zero,
                        new Vector2(0.47f, 1),
                        new Vector2(
                        22,
                        string.IsNullOrEmpty(
                        item.Description) ? 0 : 22),
                        new Vector2(
                        -12,
                        0));

                    if (!string.IsNullOrEmpty(item.Description))
                    {
                        TMP_Text description = _elements.Text(
                            "Description",
                            row.transform,
                            item.Description,
                            15);

                        description.color = new Color(0.62f, 0.7f, 0.76f);
                        SettingsUiElements.Place(
                            description.rectTransform,
                            Vector2.zero,
                            new Vector2(
                            0.47f,
                            0),
                            new Vector2(
                            22,
                            8),
                            new Vector2(
                            -12,
                            32));
                    }

                    RectTransform controls = _elements.Rect("Control", row.transform);

                    SettingsUiElements.Place(
                        controls,
                        new Vector2(0.48f, 0),
                        Vector2.one,
                        Vector2.zero,
                        Vector2.zero);
                    _refreshers.Add(_factory.Create(controls, item));
                    y += rowHeight + 4;
                }

                y += 26;
            }

            content.sizeDelta = new Vector2(0, y);
            scroll.content = content;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 38;
            Image track = _elements.Panel("Scrollbar", page, new Color(0.6f, 0.7f, 0.75f, 0.1f));

            SettingsUiElements.Place(
                track.rectTransform,
                new Vector2(1, 0),
                Vector2.one,
                new Vector2(-8, 0),
                Vector2.zero);
            Image handle = _elements.Panel("Handle", track.transform, new Color(0.74f, 0.85f, 0.86f));

            handle.rectTransform.sizeDelta = Vector2.zero;
            Scrollbar bar = track.gameObject.AddComponent<Scrollbar>();

            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            return page.gameObject;
        }

        private void SelectPage(int index)
        {
            _factory.Flush();

            for (int i = 0; i < _pages.Count; i++)
            {
                _pages[i].SetActive(i == index);
                _tabs[i].image.color = i == index ? new Color(0.3f, 0.6f, 0.55f, 0.45f) : Color.clear;
                _tabs[i].GetComponentInChildren<TMP_Text>().color = i == index ? SettingsUiElements.Accent : Color.white;
            }

            Refresh();
        }

        public void ConfigureVisibility(bool backToLobby)
        {
            _backButton.gameObject.SetActive(backToLobby);
        }

        public void Refresh()
        {
            foreach (Action refresh in _refreshers)
            {
                refresh();
            }

            _status.text = _presenter.Error ?? "変更は自動で保存されます";
        }

        public bool PollKeyCapture() => _keyCapture.Poll();

        public bool FlushAndSave()
        {
            _keyCapture.Cancel();
            _factory.Flush();
            EventSystem.current?.SetSelectedGameObject(null);

            return _presenter.Save();
        }

        public void Dispose()
        {
            _keyCapture.Dispose();
            _presenter.ValuesChanged -= Refresh;
        }
    }
}
