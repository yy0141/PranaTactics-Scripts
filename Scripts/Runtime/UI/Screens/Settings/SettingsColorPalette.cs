using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using PranaTactics.Settings;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 登録色の見本と、画面前面で色を選ぶ共通パレットを生成します。
    /// </summary>
    public sealed class SettingsColorPalette
    {
        /// <summary>
        /// カラーパレットの選択色です。
        /// </summary>
        private static readonly string[] s_colors =
        {
            "#FFFFFF", "#C0C0C0", "#606060", "#202020", "#FF334D", "#AA6633",
            "#FF66AA", "#FF9933", "#FFD633", "#FFFF99", "#66EE66", "#228844",
            "#33CCFF", "#1980FF", "#3344AA", "#66FFCC", "#CC66FF", "#8844AA",
            "#FFBBDD", "#FFCC99", "#99CCFF", "#BBDD99", "#CCCCFF", "#33AAAA"
        };

        private readonly SettingsUiElements _elements;
        private readonly SettingsPresenter _presenter;
        private readonly RectTransform _overlay;
        private readonly TMP_Text _title;
        private readonly List<Button> _choices = new List<Button>();
        private string _settingId;

        /// <summary>
        /// スクロール領域に遮られない色選択画面を生成します。
        /// </summary>
        public SettingsColorPalette(SettingsUiElements elements, SettingsPresenter presenter, RectTransform parent)
        {
            _elements = elements;
            _presenter = presenter;
            _overlay = elements.Panel("ColorPalette", parent, new Color(0, 0, 0, 0.82f)).rectTransform;
            SettingsUiElements.Stretch(_overlay);
            _overlay.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            RectTransform panel = elements.Panel("Panel", _overlay, new Color(0.06f, 0.09f, 0.13f)).rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(640, 430);
            _title = elements.Text("Title", panel, "色を選択", 24);
            SettingsUiElements.Place(_title.rectTransform, new Vector2(0, 1), Vector2.one,
                new Vector2(24, -60), new Vector2(-80, -16));
            Button close = elements.Button("Close", panel, "×");
            SettingsUiElements.Place(close.image.rectTransform, Vector2.one, Vector2.one,
                new Vector2(-64, -60), new Vector2(-16, -16));
            close.onClick.AddListener(Close);
            for (int index = 0; index < s_colors.Length; index++)
            {
                int slot = index;
                Button choice = elements.Button("Color" + index, panel, "");
                ColorUtility.TryParseHtmlString(s_colors[index], out Color color);
                choice.image.color = color;
                TMP_Text choiceLabel = choice.GetComponentInChildren<TMP_Text>();
                choiceLabel.fontSize = 16;
                choiceLabel.color = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f > 0.6f
                    ? Color.black : Color.white;
                int column = index % 6;
                int row = index / 6;
                SettingsUiElements.Place(choice.image.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(28 + column * 98, -156 - row * 82),
                    new Vector2(108 + column * 98, -86 - row * 82));
                choice.onClick.AddListener(() => Select(s_colors[slot]));
                _choices.Add(choice);
            }

            Close();
        }

        /// <summary>
        /// 設定種別に応じて一列の見本、または線と矢じりの二列を作成します。
        /// </summary>
        public Action Create(RectTransform parent, SettingItemDefinition item)
        {
            var refreshers = new List<Action>();
            if (item.Control == "color")
            {
                refreshers.Add(CreateSwatch(parent, item.SettingId, item.Label, "選択", 0, 1, 8, 8));
            }
            else if (item.Control == "teamColors")
            {
                for (int index = 0; index < item.ColorCount; index++)
                {
                    refreshers.Add(CreateSwatch(parent, "appearance.enemyTeamColors." + index,
                        "敵チーム " + (index + 1), (index + 1).ToString(),
                        (float)index / item.ColorCount, (float)(index + 1) / item.ColorCount, 8, 8));
                }
            }
            else
            {
                CreateLabel(parent, "線", 0.25f, 0.61f, 0, 32);
                CreateLabel(parent, "矢じり", 0.64f, 1, 0, 32);
                for (int index = 0; index <= item.AdditionalRowCount; index++)
                {
                    string caption = index == 0 ? "プレイヤー" : "味方 " + index;
                    float top = 32 + index * 78;
                    CreateLabel(parent, caption, 0, 0.24f, top, 74);
                    refreshers.Add(CreateSwatch(parent, "appearance.allyRouteColors." + index + ".line",
                        caption + "：線", "線", 0.25f, 0.61f, top + 8, top + 70, true));
                    refreshers.Add(CreateSwatch(parent, "appearance.allyRouteColors." + index + ".arrow",
                        caption + "：矢じり", "矢じり", 0.64f, 1, top + 8, top + 70, true));
                }
            }

            return () =>
            {
                foreach (Action refresh in refreshers)
                {
                    refresh();
                }
            };
        }

        /// <summary>
        /// 色を保存し、ゲーム表示と設定画面を更新します。
        /// </summary>
        private void Select(string color)
        {
            var item = new SettingItemDefinition { SettingId = _settingId, Control = "color" };
            if (_presenter.Change(item, color, true))
            {
                Close();
                return;
            }

            _title.text = _presenter.Error ?? "色を保存できませんでした。";
        }

        /// <summary>
        /// 登録枠の色見本と選択ボタンを生成します。
        /// </summary>
        private Action CreateSwatch(RectTransform parent, string id, string title, string caption,
            float left, float right, float bottom, float top, bool fromTop = false)
        {
            Button button = _elements.Button(id, parent, caption);
            if (fromTop)
            {
                SettingsUiElements.Place(button.image.rectTransform, new Vector2(left, 1), new Vector2(right, 1),
                    new Vector2(4, -top), new Vector2(-4, -bottom));
            }
            else
            {
                SettingsUiElements.Place(button.image.rectTransform, new Vector2(left, 0), new Vector2(right, 1),
                    new Vector2(4, bottom), new Vector2(-4, -top));
            }

            button.onClick.AddListener(() => Open(id, title));
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            label.fontSize = 18;
            return () =>
            {
                ColorUtility.TryParseHtmlString(_presenter.Read(id), out Color color);
                button.image.color = color;
                label.color = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f > 0.6f
                    ? Color.black : Color.white;
            };
        }

        /// <summary>
        /// 配色表の見出しまたはユニット名を配置します。
        /// </summary>
        private void CreateLabel(RectTransform parent, string caption, float left, float right, float top, float height)
        {
            TMP_Text text = _elements.Text(caption, parent, caption, 18);
            text.alignment = TextAlignmentOptions.Center;
            SettingsUiElements.Place(text.rectTransform, new Vector2(left, 1), new Vector2(right, 1),
                new Vector2(0, -top - height), new Vector2(0, -top));
        }

        /// <summary>
        /// 選択対象を固定して、現在色を示すパレットを開きます。
        /// </summary>
        private void Open(string id, string title)
        {
            _settingId = id;
            _title.text = title + "の色を選択";
            string selected = _presenter.Read(id);
            for (int index = 0; index < _choices.Count; index++)
            {
                _choices[index].GetComponentInChildren<TMP_Text>().text = s_colors[index] == selected ? "選択中" : "";
            }

            _overlay.SetAsLastSibling();
            _overlay.gameObject.SetActive(true);
        }

        /// <summary>
        /// 設定値を変更せず、色選択画面を閉じます。
        /// </summary>
        public void Close()
        {
            _settingId = null;
            _overlay.gameObject.SetActive(false);
        }
    }
}
