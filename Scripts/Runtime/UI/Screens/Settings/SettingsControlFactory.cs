using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using PranaTactics.Settings;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// JSONの種別に対応する入力部品を生成します。
    /// </summary>
    public sealed class SettingsControlFactory
    {
        private readonly SettingsUiElements _elements;

        private readonly SettingsPresenter _presenter;

        private readonly SettingsKeyCapture _keyCapture;
        private readonly SettingsColorPalette _colorPalette;

        private readonly Dictionary<string, Func<RectTransform, SettingItemDefinition, Action>> _builders;

        private readonly List<TMP_InputField> _inputs = new List<TMP_InputField>();

        public SettingsControlFactory(
            SettingsUiElements elements,
            SettingsPresenter presenter,
            SettingsKeyCapture keyCapture, RectTransform overlayParent)
        {
            _elements = elements;
            _presenter = presenter;
            _keyCapture = keyCapture;
            _colorPalette = new SettingsColorPalette(elements, presenter, overlayParent);
            _builders = new Dictionary<string, Func<RectTransform, SettingItemDefinition, Action>>
            {
                ["number"] = (root, item) => Number(root, item, false),
                ["numberSlider"] = (root, item) => Number(root, item, true),
                ["choice"] = Choice,
                ["dropdown"] = Dropdown,
                ["key"] = Key,
                ["keyChoice"] = KeyChoice,
                ["color"] = _colorPalette.Create,
                ["teamColors"] = _colorPalette.Create,
                ["routeColors"] = _colorPalette.Create
            };
        }

        public Action Create(RectTransform parent, SettingItemDefinition item)
        {
            CanvasGroup group = parent.gameObject.AddComponent<CanvasGroup>();
            Action refresh = _builders[item.Control](parent, item);

            return () =>
            {
                bool enabled = _presenter.IsEnabled(item);

                group.interactable = enabled;
                group.alpha = enabled ? 1.0f : 0.35f;
                refresh();
            };
        }

        public void Flush()
        {
            _keyCapture.Cancel();
            _colorPalette.Close();

            foreach (TMP_InputField input in _inputs)
            {
                if (input.isFocused)
                {
                    input.DeactivateInputField();
                }
            }
        }

        /// <summary>
        /// キー入力と操作方式を横並びに配置し、各設定IDへ独立して保存します。
        /// </summary>
        private Action KeyChoice(RectTransform parent, SettingItemDefinition item)
        {
            RectTransform keyRoot = _elements.Rect("Key", parent);

            SettingsUiElements.Place(
                keyRoot,
                Vector2.zero,
                new Vector2(0.45f, 1),
                Vector2.zero,
                new Vector2(
                -6,
                0));
            RectTransform choiceRoot = _elements.Rect("Mode", parent);

            SettingsUiElements.Place(
                choiceRoot,
                new Vector2(0.45f, 0),
                Vector2.one,
                new Vector2(6, 8),
                new Vector2(
                0,
                -8));
            var keyItem = new SettingItemDefinition
            {
                SettingId = item.KeySettingId,
                Label = item.Label,
                Control = "key",
                EnabledWhen = item.EnabledWhen
            };
            Action refreshKey = Key(keyRoot, keyItem);
            Action refreshChoice = Choice(choiceRoot, item);

            return () =>
            {
                refreshKey();
                refreshChoice();
            };
        }

        private Action Key(RectTransform parent, SettingItemDefinition item)
        {
            Button button = _elements.Button("Binding", parent, "");

            SettingsUiElements.Place(
                (RectTransform)button.transform,
                Vector2.zero,
                Vector2.one,
                new Vector2(
                4,
                8),
                new Vector2(
                -4,
                -8));
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            Image caret = _elements.Panel("Caret", button.transform, SettingsUiElements.Accent);

            caret.raycastTarget = false;
            SettingsUiElements.Place(
                caret.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(
                -1,
                -14),
                new Vector2(
                1,
                14));
            caret.gameObject.SetActive(false);
            Action refresh = () =>
            {
                if (!_keyCapture.IsEditing(button))
                {
                    label.text = KeyboardBinding.Display(_presenter.Read(item.SettingId));
                }
            };

            button.onClick.AddListener(() => _keyCapture.Begin(button, label, caret.gameObject, refresh,
                value => _presenter.Change(item, value, true)));

            return refresh;
        }

        private Action Number(RectTransform parent, SettingItemDefinition item, bool hasSlider)
        {
            TMP_InputField input = _elements.Input(parent);

            _inputs.Add(input);
            SettingsUiElements.Place(
                (RectTransform)input.transform,
                Vector2.zero,
                new Vector2(
                hasSlider ? 0.24f : 1,
                1),
                new Vector2(
                4,
                10),
                new Vector2(
                -4,
                -10));
            input.onEndEdit.AddListener(value => _presenter.Change(item, value, true));
            Slider slider = null;

            if (hasSlider)
            {
                slider = _elements.Slider(parent);
                SettingsUiElements.Place(
                    (RectTransform)slider.transform,
                    new Vector2(0.29f, 0),
                    Vector2.one,
                    new Vector2(
                    0,
                    8),
                    new Vector2(
                    -16,
                    -8));
                slider.minValue = item.Min.Value;
                slider.maxValue = item.Max.Value;
                slider.onValueChanged.AddListener(
                    value => _presenter.Change(
                    item,
                    value.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture),
                    false));
                SettingsSliderCommit commit = slider.gameObject.AddComponent<SettingsSliderCommit>();

                commit.Commit = () => _presenter.Save();
            }

            return () =>
            {
                string value = _presenter.Read(item.SettingId);

                if (!input.isFocused)
                {
                    input.SetTextWithoutNotify(value);
                }

                if (slider != null)
                {
                    slider.SetValueWithoutNotify(float.Parse(value, CultureInfo.InvariantCulture));
                }
            };
        }

        private Action Choice(RectTransform parent, SettingItemDefinition item)
        {
            var buttons = new List<Button>();

            for (int i = 0; i < item.Options.Count; i++)
            {
                SettingOptionDefinition option = item.Options[i];
                Button button = _elements.Button(option.Value, parent, option.Label);

                SettingsUiElements.Place(
                    (RectTransform)button.transform,
                    new Vector2(
                    (float)i / item.Options.Count,
                    0),
                    new Vector2(
                    (float)(i +
                    1) / item.Options.Count,
                    1),
                    new Vector2(
                    2,
                    2),
                    new Vector2(
                    -2,
                    -2));
                button.onClick.AddListener(() => _presenter.Change(item, option.Value, true));
                buttons.Add(button);
            }

            return () =>
            {
                for (int i = 0; i < buttons.Count; i++)
                {
                    bool selected = _presenter.Read(item.SettingId) == item.Options[i].Value;

                    buttons[i].image.color = selected ? new Color(0.55f, 0.75f, 0.72f, 0.48f) : SettingsUiElements.Surface;
                }
            };
        }

        private Action Dropdown(RectTransform parent, SettingItemDefinition item)
        {
            TMP_Dropdown dropdown = _elements.Dropdown(parent);

            SettingsUiElements.Place(
                (RectTransform)dropdown.transform,
                Vector2.zero,
                Vector2.one,
                new Vector2(
                4,
                8),
                new Vector2(
                -4,
                -8));
            var options = new List<string>();

            foreach (SettingOptionDefinition option in item.Options)
            {
                options.Add(option.Label);
            }

            dropdown.AddOptions(options);
            dropdown.onValueChanged.AddListener(
                index => _presenter.Change(
                item,
                item.Options[index].Value,
                true));

            return () =>
            {
                int index = item.Options.FindIndex(option => option.Value == _presenter.Read(item.SettingId));

                dropdown.SetValueWithoutNotify(Math.Max(0, index));
                dropdown.RefreshShownValue();
            };
        }
    }
}
