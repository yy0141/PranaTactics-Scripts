using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PranaTactics.Settings
{
    /// <summary>
    /// JSON内の順序を維持する設定画面全体の定義です。
    /// </summary>
    public sealed class SettingsViewDefinition
    {
        public int SchemaVersion
        {
            get; set;
        }

        public List<SettingsCategoryDefinition> Categories { get; set; } = new List<SettingsCategoryDefinition>();

        /// <summary>
        /// JSONの表示枠数をゲームで使用する登録色数へ反映します。
        /// </summary>
        public void ApplyAppearanceLayout(UserSettings settings)
        {
            foreach (SettingsCategoryDefinition category in Categories)
            {
                foreach (SettingsSectionDefinition section in category.Sections)
                {
                    foreach (SettingItemDefinition item in section.Items)
                    {
                        if (item.Control == "teamColors")
                        {
                            settings.Appearance.EnemyTeamColorCount = item.ColorCount;
                        }
                        else if (item.Control == "routeColors")
                        {
                            settings.Appearance.AdditionalAllyRouteColorCount = item.AdditionalRowCount;
                        }
                    }
                }
            }

            settings.Appearance.Validate();
        }

        /// <summary>
        /// 項目ID、部品の型、選択肢、有効条件を検証します。
        /// </summary>
        public void Validate(SettingsCatalog catalog)
        {
            if (SchemaVersion != 1 || Categories == null || Categories.Count == 0)
            {
                throw new InvalidOperationException("設定画面の形式またはカテゴリが不正です。");
            }

            var categoryIds = new HashSet<string>();
            var itemIds = new HashSet<string>();

            foreach (SettingsCategoryDefinition category in Categories)
            {
                if (category == null || string.IsNullOrWhiteSpace(category.Id) ||
                    !categoryIds.Add(
                    category.Id) ||
                    category.Sections == null)
                {
                    throw new InvalidOperationException("カテゴリのIDが欠落または重複しています。");
                }

                var sectionIds = new HashSet<string>();

                foreach (SettingsSectionDefinition section in category.Sections)
                {
                    if (section == null || string.IsNullOrWhiteSpace(section.Id) ||
                        !sectionIds.Add(
                        section.Id) ||
                        section.Items == null)
                    {
                        throw new InvalidOperationException("小見出しが不正です。");
                    }

                    foreach (SettingItemDefinition item in section.Items)
                    {
                        if (item == null || !itemIds.Add(item.SettingId))
                        {
                            throw new InvalidOperationException("設定項目が欠落または重複しています。");
                        }

                        if (item.Control == "teamColors" || item.Control == "routeColors")
                        {
                            if ((item.Control == "teamColors" && (item.ColorCount < 1 || item.ColorCount > 6)) ||
                                (item.Control == "routeColors" && (item.AdditionalRowCount < 0 || item.AdditionalRowCount > 4)))
                            {
                                throw new InvalidOperationException("色の登録枠数が不正です: " + item.SettingId);
                            }

                            foreach (string colorId in item.GetColorSettingIds())
                            {
                                if (!itemIds.Add(colorId) || !catalog.Get(colorId).IsColor)
                                {
                                    throw new InvalidOperationException("色設定が重複または不正です: " + colorId);
                                }
                            }

                            continue;
                        }

                        SettingBinding binding = catalog.Get(item.SettingId);

                        bool isNumber = item.Control == "number" || item.Control == "numberSlider";
                        bool isChoice = item.Control == "choice" || item.Control == "dropdown" ||
                            item.Control == "keyChoice";

                        if ((!isNumber && !isChoice && item.Control != "key" && item.Control != "color") ||
                            (item.Control == "color") != binding.IsColor || isNumber != binding.IsNumber ||
                            (item.Control == "key") != binding.IsKey)
                        {
                            throw new InvalidOperationException("設定値と入力部品の型が一致しません: " + item.SettingId);
                        }

                        if (item.Control == "numberSlider")
                        {
                            if (!item.Min.HasValue || !item.Max.HasValue ||
                                float.IsNaN(item.Min.Value) || float.IsInfinity(item.Min.Value) ||
                                float.IsNaN(item.Max.Value) || float.IsInfinity(item.Max.Value) ||
                                item.Min.Value >= item.Max.Value ||
                                item.Min.Value < binding.Minimum || item.Max.Value > binding.Maximum)
                            {
                                throw new InvalidOperationException(
                                    "スライダーのmin/maxには設定の許容範囲内で最小値 < 最大値を指定してください: " +
                                    item.SettingId);
                            }
                        }

                        if (item.Control == "keyChoice")
                        {
                            SettingBinding keyBinding = catalog.Get(item.KeySettingId);

                            if (!keyBinding.IsKey || !itemIds.Add(item.KeySettingId))
                            {
                                throw new InvalidOperationException("キー設定が不正または重複しています: " + item.KeySettingId);
                            }
                        }
                        else if (item.KeySettingId != null)
                        {
                            throw new InvalidOperationException(
                                "キー設定の併設にはkeyChoiceを指定してください: " +
                                item.SettingId);
                        }

                        if (isChoice)
                        {
                            var values = new HashSet<string>();

                            if (item.Options == null || item.Options.Count < 2)
                            {
                                throw new InvalidOperationException("選択肢が不足しています: " + item.SettingId);
                            }

                            foreach (SettingOptionDefinition option in item.Options)
                            {
                                if (option == null || !values.Add(option.Value) ||
                                    !binding.Accepts(
                                    option.Value))
                                {
                                    throw new InvalidOperationException("選択肢が不正です: " + item.SettingId);
                                }
                            }

                            if (values.Count != binding.AllowedValues.Count)
                            {
                                throw new InvalidOperationException("選択肢が欠落しています: " + item.SettingId);
                            }
                        }

                        if (item.EnabledWhen != null &&
                            !catalog.Get(item.EnabledWhen.SettingId).Accepts(
                            item.EnabledWhen.EqualsValue))
                        {
                            throw new InvalidOperationException("有効条件が不正です: " + item.SettingId);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 一つのタブに対応する大見出しです。
    /// </summary>
    public sealed class SettingsCategoryDefinition
    {
        public string Id
        {
            get; set;
        }

        public string Label
        {
            get; set;
        }

        public List<SettingsSectionDefinition> Sections { get; set; } = new List<SettingsSectionDefinition>();
    }

    /// <summary>
    /// 設定行をまとめる小見出しです。
    /// </summary>
    public sealed class SettingsSectionDefinition
    {
        public string Id
        {
            get; set;
        }

        public string Label
        {
            get; set;
        }

        public List<SettingItemDefinition> Items { get; set; } = new List<SettingItemDefinition>();
    }

    /// <summary>
    /// 設定IDと表示部品の対応を保持します。
    /// </summary>
    public sealed class SettingItemDefinition
    {
        public int ColorCount { get; set; } = 6;
        public int AdditionalRowCount { get; set; } = 4;

        /// <summary>
        /// JSONで指定された登録枠を、保存項目の順序へ展開します。
        /// </summary>
        public IReadOnlyList<string> GetColorSettingIds()
        {
            var ids = new List<string>();
            if (Control == "teamColors")
            {
                for (int index = 0; index < ColorCount; index++)
                {
                    ids.Add("appearance.enemyTeamColors." + index);
                }
            }
            else if (Control == "routeColors")
            {
                for (int index = 0; index <= AdditionalRowCount; index++)
                {
                    ids.Add("appearance.allyRouteColors." + index + ".line");
                    ids.Add("appearance.allyRouteColors." + index + ".arrow");
                }
            }

            return ids;
        }

        public string SettingId
        {
            get; set;
        }

        public string Label
        {
            get; set;
        }

        public string Description
        {
            get; set;
        }

        public string Control
        {
            get; set;
        }

        public float? Min
        {
            get; set;
        }

        public float? Max
        {
            get; set;
        }

        public string KeySettingId
        {
            get; set;
        }

        public List<SettingOptionDefinition> Options
        {
            get; set;
        }

        public SettingConditionDefinition EnabledWhen
        {
            get; set;
        }
    }

    /// <summary>
    /// 選択肢の保存値と表示名を保持します。
    /// </summary>
    public sealed class SettingOptionDefinition
    {
        public string Value
        {
            get; set;
        }

        public string Label
        {
            get; set;
        }
    }

    /// <summary>
    /// 他の設定値に応じた編集可能条件を保持します。
    /// </summary>
    public sealed class SettingConditionDefinition
    {
        public string SettingId
        {
            get; set;
        }

        [JsonProperty("equals")]
        public string EqualsValue
        {
            get; set;
        }
    }
}
