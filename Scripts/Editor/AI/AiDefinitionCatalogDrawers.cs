using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using PranaTactics.Pranam;

namespace PranaTactics.AI.Solo.Editor
{
    /// <summary>
    /// AiDefinitionCatalog内の文字列IDを、既知のIDから選択できるようにします。
    /// 保存形式は従来どおりstringであるため、実行時コードとの互換性を維持します。
    /// </summary>
    internal static class AiDefinitionPopupUtility
    {
        private const float k_EditButtonWidth = 44.0f;

        private const float k_ButtonSpacing = 3.0f;

        private static readonly HashSet<string> s_editingProperties = new HashSet<string>(
            StringComparer.Ordinal);

        public static void DrawWeaponId(Rect position, SerializedProperty property, GUIContent label)
        {
            List<string> options = BuildWeaponIds(property);

            DrawSelectableString(position, property, label, options, GetWeaponLabel);
        }

        public static void DrawRoleId(Rect position, SerializedProperty property, GUIContent label)
        {
            List<string> options = BuildRoleIds(property);

            DrawSelectableString(position, property, label, options, GetRoleLabel);
        }

        public static void DrawRoleAlias(Rect position, SerializedProperty property, GUIContent label)
        {
            List<string> options = BuildRoleAliases(property);

            DrawSelectableString(position, property, label, options, GetRoleAliasLabel);
        }

        public static void DrawAbilityTag(Rect position, SerializedProperty property, GUIContent label)
        {
            List<string> options = new List<string>();

            AddUnique(options, string.Empty);
            IReadOnlyList<string> knownTags = AiAbilityTags.All;

            for (int i = 0; i < knownTags.Count; i++)
            {
                AddUnique(options, knownTags[i]);
            }

            AddUnique(options, property.stringValue);
            DrawSelectableString(position, property, label, options, GetAbilityLabel);
        }

        private static void DrawSelectableString(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            List<string> options,
            Func<string, string> getDisplayLabel)
        {
            string propertyKey = GetPropertyKey(property);
            bool isEditing = s_editingProperties.Contains(propertyKey);
            Rect valueRect = position;

            valueRect.width -= k_EditButtonWidth + k_ButtonSpacing;
            Rect buttonRect = position;

            buttonRect.x = valueRect.xMax + k_ButtonSpacing;
            buttonRect.width = k_EditButtonWidth;

            if (isEditing)
            {
                property.stringValue = EditorGUI.DelayedTextField(valueRect, label, property.stringValue);

                if (GUI.Button(buttonRect, "選択"))
                {
                    s_editingProperties.Remove(propertyKey);
                }

                return;
            }

            string[] displayOptions = new string[options.Count];

            for (int i = 0; i < options.Count; i++)
            {
                displayOptions[i] = getDisplayLabel(options[i]);
            }

            int selectedIndex = FindIndex(options, property.stringValue);

            if (selectedIndex < 0)
            {
                AddUnique(options, property.stringValue);
                selectedIndex = options.Count - 1;
                Array.Resize(ref displayOptions, options.Count);
                displayOptions[selectedIndex] = getDisplayLabel(property.stringValue);
            }

            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUI.Popup(valueRect, label.text, selectedIndex, displayOptions);

            if (EditorGUI.EndChangeCheck() && nextIndex >= 0 && nextIndex < options.Count)
            {
                property.stringValue = options[nextIndex];
            }

            if (GUI.Button(buttonRect, "編集"))
            {
                s_editingProperties.Add(propertyKey);
            }
        }

        private static List<string> BuildWeaponIds(SerializedProperty property)
        {
            List<string> result = new List<string>();
            IReadOnlyList<string> builtInIds = AiWeaponIds.All;

            for (int i = 0; i < builtInIds.Count; i++)
            {
                AddUnique(result, builtInIds[i]);
            }

            SerializedProperty weapons = property.serializedObject.FindProperty("_weapons");

            AddIdsFromArray(result, weapons, "_id");
            AddUnique(result, property.stringValue);

            return result;
        }

        private static List<string> BuildRoleIds(SerializedProperty property)
        {
            List<string> result = new List<string>();
            IReadOnlyList<string> builtInIds = AiRoleIds.All;

            for (int i = 0; i < builtInIds.Count; i++)
            {
                AddUnique(result, builtInIds[i]);
            }

            SerializedProperty roles = property.serializedObject.FindProperty("_roles");

            AddIdsFromArray(result, roles, "_id");
            AddUnique(result, property.stringValue);

            return result;
        }

        private static List<string> BuildRoleAliases(SerializedProperty property)
        {
            List<string> result = new List<string>();

            AddUnique(result, string.Empty);
            AddUnique(result, "generic");
            AddUnique(result, "safe");
            AddUnique(result, "attacker");
            AddUnique(result, "attack");
            AddUnique(result, "近接");
            AddUnique(result, "shooter");
            AddUnique(result, "shoot");
            AddUnique(result, "射撃");
            AddUnique(result, "gunner");
            AddUnique(result, "gun");
            AddUnique(result, "銃手");
            AddUnique(result, "sniper");
            AddUnique(result, "snipe");
            AddUnique(result, "狙撃");
            SerializedProperty roles = property.serializedObject.FindProperty("_roles");

            if (roles != null && roles.isArray)
            {
                for (int i = 0; i < roles.arraySize; i++)
                {
                    SerializedProperty role = roles.GetArrayElementAtIndex(i);
                    SerializedProperty aliases = role.FindPropertyRelative("_aliases");

                    if (aliases == null || !aliases.isArray)
                    {
                        continue;
                    }

                    for (int aliasIndex = 0; aliasIndex < aliases.arraySize; aliasIndex++)
                    {
                        AddUnique(result, aliases.GetArrayElementAtIndex(aliasIndex).stringValue);
                    }
                }
            }

            AddUnique(result, property.stringValue);

            return result;
        }

        private static void AddIdsFromArray(
            List<string> result,
            SerializedProperty arrayProperty,
            string relativeIdName)
        {
            if (arrayProperty == null || !arrayProperty.isArray)
            {
                return;
            }

            for (int i = 0; i < arrayProperty.arraySize; i++)
            {
                SerializedProperty element = arrayProperty.GetArrayElementAtIndex(i);
                SerializedProperty idProperty = element.FindPropertyRelative(relativeIdName);

                if (idProperty != null)
                {
                    AddUnique(result, idProperty.stringValue);
                }
            }
        }

        private static void AddUnique(List<string> values, string value)
        {
            string safeValue = value ?? string.Empty;

            if (FindIndex(values, safeValue) < 0)
            {
                values.Add(safeValue);
            }
        }

        private static int FindIndex(List<string> values, string value)
        {
            string safeValue = value ?? string.Empty;

            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], safeValue, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string GetPropertyKey(SerializedProperty property)
        {
            UnityEngine.Object target = property.serializedObject.targetObject;
            EntityId entityId = target != null ? target.GetEntityId() : default;

            return entityId + ":" + property.propertyPath;
        }

        private static string GetWeaponLabel(string id)
        {
            switch (id)
            {
                case AiWeaponIds.Generic:
                    return "汎用  (weapon_ai.generic)";
                case AiWeaponIds.Zephyr:
                    return "ゼファー  (weapon_ai.zephyr)";
                case AiWeaponIds.Umbra:
                    return "ウンブラ  (weapon_ai.umbra)";
                case AiWeaponIds.Scutum:
                    return "スクトゥム  (weapon_ai.scutum)";
                case AiWeaponIds.Palma:
                    return "パルマ  (weapon_ai.palma)";
                case AiWeaponIds.Arcus:
                    return "アルクス  (weapon_ai.arcus)";
                case AiWeaponIds.Obscura:
                    return "オブスクラ  (weapon_ai.obscura)";
                case AiWeaponIds.Gerade:
                    return "ゲラーデ  (weapon_ai.gerade)";
                case AiWeaponIds.Erupt:
                    return "イラプト  (weapon_ai.erupt)";
                case AiWeaponIds.Captare:
                    return "カプターレ  (weapon_ai.captare)";
                case AiWeaponIds.SeriesGerade:
                    return "セリエス（ゲラーデ）  (weapon_ai.series_gerade)";
                case AiWeaponIds.SeriesCaptare:
                    return "セリエス（カプターレ）  (weapon_ai.series_captare)";
                case AiWeaponIds.SeriesArcus:
                    return "セリエス（アルクス）  (weapon_ai.series_arcus)";
                case AiWeaponIds.OrbisGerade:
                    return "オルビス（ゲラーデ）  (weapon_ai.orbis_gerade)";
                case AiWeaponIds.Spargo:
                    return "スパルゴ  (weapon_ai.spargo)";
                case AiWeaponIds.Levis:
                    return "レヴィス  (weapon_ai.levis)";
                case AiWeaponIds.Stilla:
                    return "スティラ  (weapon_ai.stilla)";
                case AiWeaponIds.Destinare:
                    return "デスティナーレ  (weapon_ai.destinare)";
                default:
                    return string.IsNullOrEmpty(id) ? "未設定" : id;
            }
        }

        private static string GetRoleLabel(string id)
        {
            switch (id)
            {
                case AiRoleIds.GenericSafe:
                    return "汎用安全  (role.generic_safe)";
                case AiRoleIds.Attacker:
                    return "アタッカー  (role.attacker)";
                case AiRoleIds.Shooter:
                    return "シューター  (role.shooter)";
                case AiRoleIds.Gunner:
                    return "ガンナー  (role.gunner)";
                case AiRoleIds.Sniper:
                    return "スナイパー  (role.sniper)";
                default:
                    return string.IsNullOrEmpty(id) ? "未設定" : id;
            }
        }

        private static string GetRoleAliasLabel(string alias)
        {
            switch (alias)
            {
                case "generic":
                    return "汎用  (generic)";
                case "safe":
                    return "安全  (safe)";
                case "attacker":
                    return "アタッカー  (attacker)";
                case "attack":
                    return "攻撃  (attack)";
                case "近接":
                    return "近接";
                case "shooter":
                    return "シューター  (shooter)";
                case "shoot":
                    return "射撃  (shoot)";
                case "射撃":
                    return "射撃";
                case "gunner":
                    return "ガンナー  (gunner)";
                case "gun":
                    return "銃  (gun)";
                case "銃手":
                    return "銃手";
                case "sniper":
                    return "スナイパー  (sniper)";
                case "snipe":
                    return "狙撃  (snipe)";
                case "狙撃":
                    return "狙撃";
                default:
                    return string.IsNullOrEmpty(alias) ? "未設定" : alias;
            }
        }

        private static string GetAbilityLabel(string id)
        {
            switch (id)
            {
                case AiAbilityTags.Direct:
                    return "直進・直接攻撃  (ability.direct)";
                case AiAbilityTags.Arc:
                    return "放物線攻撃  (ability.arc)";
                case AiAbilityTags.Homing:
                    return "追尾  (ability.homing)";
                case AiAbilityTags.Tracking:
                    return "継続追跡  (ability.tracking)";
                case AiAbilityTags.RapidFire:
                    return "自動連射  (ability.rapid_fire)";
                case AiAbilityTags.ShieldDeploy:
                    return "シールド生成  (ability.shield_deploy)";
                case AiAbilityTags.FullBodyDefense:
                    return "全身防御  (ability.full_body_defense)";
                case AiAbilityTags.StealthActivate:
                    return "ステルス発動  (ability.stealth_activate)";
                case AiAbilityTags.Reveal:
                    return "索敵・ステルス解除  (ability.reveal)";
                case AiAbilityTags.HighCommitment:
                    return "長い硬直  (ability.high_commitment)";
                default:
                    return string.IsNullOrEmpty(id) ? "未設定" : id;
            }
        }
    }

    /// <summary>
    /// PranamDataのWeaponAiDefinitionIdを兵装ID選択式で表示します。
    /// </summary>
    [CustomPropertyDrawer(typeof(WeaponAiDefinitionIdAttribute))]
    public sealed class WeaponAiDefinitionIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);

                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            AiDefinitionPopupUtility.DrawWeaponId(position, property, label);
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }

    /// <summary>
    /// ロール定義のAliasesを選択式で表示します。
    /// </summary>
    [CustomPropertyDrawer(typeof(AiRoleDefinition))]
    public sealed class AiRoleDefinitionDrawer : PropertyDrawer
    {
        private const float k_VerticalSpacing = 2.0f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect line = GetLine(position);

            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();

                return;
            }

            EditorGUI.indentLevel++;
            DrawProperty(ref line, property, "_id", "Id");
            DrawProperty(ref line, property, "_displayName", "Display Name");
            DrawProperty(ref line, property, "_legacyRole", "Legacy Role");
            DrawAliases(ref line, property);
            DrawHeader(ref line, "Movement");
            DrawProperty(ref line, property, "_preferredMinimumCells", "Preferred Minimum Cells");
            DrawProperty(ref line, property, "_preferredMaximumCells", "Preferred Maximum Cells");
            DrawProperty(ref line, property, "_dangerWeight", "Danger Weight");
            DrawProperty(ref line, property, "_highGroundWeight", "High Ground Weight");
            DrawProperty(ref line, property, "_movementWeight", "Movement Weight");
            DrawProperty(ref line, property, "_movementDutyRatio", "Movement Duty Ratio");
            DrawProperty(ref line, property, "_pathLengthMultiplier", "Path Length Multiplier");
            DrawProperty(ref line, property, "_minimumPathUsageRatio", "Minimum Path Usage Ratio");
            DrawProperty(ref line, property, "_planningPriority", "Planning Priority");
            DrawHeader(ref line, "Formation");
            DrawProperty(ref line, property, "_minimumAllyDistance", "Minimum Ally Distance");
            DrawProperty(ref line, property, "_isolationDistance", "Isolation Distance");
            DrawHeader(ref line, "Threat");
            DrawProperty(ref line, property, "_threatMinimumCells", "Threat Minimum Cells");
            DrawProperty(ref line, property, "_threatMaximumCells", "Threat Maximum Cells");
            DrawProperty(ref line, property, "_nearThreat", "Near Threat");
            DrawProperty(ref line, property, "_peakThreat", "Peak Threat");
            DrawProperty(ref line, property, "_farThreat", "Far Threat");
            DrawHeader(ref line, "Weapon Affinity");
            MoveToNextLine(ref line);
            SerializedProperty affinities = property.FindPropertyRelative("_weaponAffinities");

            line.height = EditorGUI.GetPropertyHeight(affinities, true);
            EditorGUI.PropertyField(line, affinities, new GUIContent("Entries"), true);
            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;

            if (!property.isExpanded)
            {
                return lineHeight;
            }

            float height = 25.0f * lineHeight + 25.0f * k_VerticalSpacing;
            SerializedProperty aliases = property.FindPropertyRelative("_aliases");

            if (aliases != null && aliases.isExpanded)
            {
                int additionalLines = 1 + aliases.arraySize;

                height += additionalLines * (lineHeight + k_VerticalSpacing);
            }

            SerializedProperty affinities = property.FindPropertyRelative("_weaponAffinities");

            height += EditorGUI.GetPropertyHeight(affinities, true);

            return height;
        }

        private static void DrawAliases(ref Rect line, SerializedProperty property)
        {
            MoveToNextLine(ref line);
            SerializedProperty aliases = property.FindPropertyRelative("_aliases");

            aliases.isExpanded = EditorGUI.Foldout(line, aliases.isExpanded, "Aliases", true);

            if (!aliases.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            MoveToNextLine(ref line);
            int nextSize = EditorGUI.IntField(line, "Size", aliases.arraySize);

            if (nextSize != aliases.arraySize)
            {
                aliases.arraySize = Mathf.Max(0, nextSize);
            }

            for (int i = 0; i < aliases.arraySize; i++)
            {
                MoveToNextLine(ref line);
                AiDefinitionPopupUtility.DrawRoleAlias(
                    line,
                    aliases.GetArrayElementAtIndex(i),
                    new GUIContent(
                    "Element " +
                    i));
            }

            EditorGUI.indentLevel--;
        }

        private static void DrawHeader(ref Rect line, string header)
        {
            MoveToNextLine(ref line);
            EditorGUI.LabelField(line, header, EditorStyles.boldLabel);
        }

        private static void DrawProperty(
            ref Rect line,
            SerializedProperty root,
            string relativeName,
            string displayName)
        {
            MoveToNextLine(ref line);
            EditorGUI.PropertyField(
                line,
                root.FindPropertyRelative(relativeName),
                new GUIContent(displayName));
        }

        private static Rect GetLine(Rect position)
        {
            return new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        }

        private static void MoveToNextLine(ref Rect line)
        {
            line.y += EditorGUIUtility.singleLineHeight + k_VerticalSpacing;
            line.height = EditorGUIUtility.singleLineHeight;
        }
    }

    [CustomPropertyDrawer(typeof(AiWeaponDefinition))]
    public sealed class AiWeaponDefinitionDrawer : PropertyDrawer
    {
        private const float k_VerticalSpacing = 2.0f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect line = GetLine(position);

            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();

                return;
            }

            EditorGUI.indentLevel++;
            DrawProperty(ref line, property, "_sourceData", "Source Weapon Data");
            PranamData source = property.FindPropertyRelative("_sourceData").objectReferenceValue as PranamData;
            SerializedObject sourceProperties = source != null ? new SerializedObject(source) : null;
            MoveToNextLine(ref line);
            using (new EditorGUI.DisabledScope(source != null && !string.IsNullOrWhiteSpace(source.WeaponAiDefinitionId)))
            {
                AiDefinitionPopupUtility.DrawWeaponId(
                    line,
                    property.FindPropertyRelative("_id"),
                    new GUIContent(
                    "Id"));
            }
            MoveToNextLine(ref line);
            using (new EditorGUI.DisabledScope(AiDefinitionCatalogSynchronizer.GetWeaponRole(source).HasValue))
            {
                AiDefinitionPopupUtility.DrawRoleId(
                    line,
                    property.FindPropertyRelative("_roleId"),
                    new GUIContent(
                    "Role Id"));
            }
            DrawProperty(ref line, property, "_visibilityRequirement", "Visibility Requirement");
            DrawProperty(ref line, property, "_useMode", "Use Mode");
            DrawAbilityTags(ref line, property);
            DrawProperty(ref line, property, "_preferredMinimumFactor", "Preferred Minimum Factor");
            DrawProperty(ref line, property, "_preferredMaximumFactor", "Preferred Maximum Factor");
            MoveToNextLine(ref line);
            EditorGUI.LabelField(line, "Source Data / Fallback", EditorStyles.boldLabel);
            DrawProperty(ref line, property, "_fallbackMinimumRange", "Fallback Minimum Range");
            using (new EditorGUI.DisabledScope(sourceProperties != null && sourceProperties.FindProperty("_diameter") != null))
            {
                DrawProperty(ref line, property, "_fallbackMaximumRange", "Fallback Maximum Range");
            }
            using (new EditorGUI.DisabledScope(source != null))
            {
                DrawProperty(ref line, property, "_fallbackPranaCost", "Fallback Prana Cost");
            }
            using (new EditorGUI.DisabledScope(sourceProperties != null && sourceProperties.FindProperty("_attackPower") != null))
            {
                DrawProperty(ref line, property, "_fallbackExpectedDamage", "Fallback Expected Damage");
            }
            using (new EditorGUI.DisabledScope(source != null && typeof(PranamBase).Assembly.GetType("PranaTactics.Pranam." + source.Range) != null))
            {
                DrawProperty(ref line, property, "_usesHold", "Uses Hold");
            }
            sourceProperties?.Dispose();
            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        /// <remarks>
        /// <para>Source, Id, RoleId, Visibility, UseMode, AbilityTags,</para>
        /// <para>PreferredMin, PreferredMax, Header, fallback x4, UsesHold</para>
        /// <para>Size + each element</para>
        /// </remarks>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int lineCount = 1;

            if (property.isExpanded)
            {
                lineCount += 14;
                SerializedProperty tags = property.FindPropertyRelative("_abilityTags");

                if (tags != null && tags.isExpanded)
                {
                    lineCount += 1 + tags.arraySize;
                }
            }

            return lineCount * EditorGUIUtility.singleLineHeight +
                Mathf.Max(0, lineCount - 1) * k_VerticalSpacing;
        }

        private static void DrawAbilityTags(ref Rect line, SerializedProperty property)
        {
            MoveToNextLine(ref line);
            SerializedProperty tags = property.FindPropertyRelative("_abilityTags");

            tags.isExpanded = EditorGUI.Foldout(line, tags.isExpanded, "Ability Tags", true);

            if (!tags.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            MoveToNextLine(ref line);
            int nextSize = EditorGUI.IntField(line, "Size", tags.arraySize);

            if (nextSize != tags.arraySize)
            {
                tags.arraySize = Mathf.Max(0, nextSize);
            }

            for (int i = 0; i < tags.arraySize; i++)
            {
                MoveToNextLine(ref line);
                AiDefinitionPopupUtility.DrawAbilityTag(
                    line,
                    tags.GetArrayElementAtIndex(i),
                    new GUIContent(
                    "Element " +
                    i));
            }

            EditorGUI.indentLevel--;
        }

        private static void DrawProperty(
            ref Rect line,
            SerializedProperty root,
            string relativeName,
            string displayName)
        {
            MoveToNextLine(ref line);
            EditorGUI.PropertyField(
                line,
                root.FindPropertyRelative(relativeName),
                new GUIContent(displayName));
        }

        private static Rect GetLine(Rect position)
        {
            return new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        }

        private static void MoveToNextLine(ref Rect line)
        {
            line.y += EditorGUIUtility.singleLineHeight + k_VerticalSpacing;
        }
    }

    /// <summary>
    /// キャラクターBindingのRole Idも同じ選択肢へ統一します。
    /// </summary>
    [CustomPropertyDrawer(typeof(AiCharacterRoleBinding))]
    public sealed class AiCharacterRoleBindingDrawer : PropertyDrawer
    {
        private const float k_VerticalSpacing = 2.0f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                line.y += EditorGUIUtility.singleLineHeight + k_VerticalSpacing;
                SerializedProperty source = property.FindPropertyRelative("_sourceCharacter");
                EditorGUI.PropertyField(line, source, new GUIContent("Source Character Data"));
                line.y += EditorGUIUtility.singleLineHeight + k_VerticalSpacing;
                using (new EditorGUI.DisabledScope(source.objectReferenceValue != null))
                {
                    EditorGUI.PropertyField(
                        line,
                        property.FindPropertyRelative("_characterId"),
                        new GUIContent(
                        "Character Id"));
                    line.y += EditorGUIUtility.singleLineHeight + k_VerticalSpacing;
                    AiDefinitionPopupUtility.DrawRoleId(
                        line,
                        property.FindPropertyRelative("_roleId"),
                        new GUIContent(
                        "Role Id"));
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int lineCount = property.isExpanded ? 4 : 1;

            return lineCount * EditorGUIUtility.singleLineHeight + (lineCount - 1) * k_VerticalSpacing;
        }
    }

    /// <summary>
    /// Weapon AffinityのWeapon Role Idも選択式にします。
    /// </summary>
    [CustomPropertyDrawer(typeof(AiRoleAffinity))]
    public sealed class AiRoleAffinityDrawer : PropertyDrawer
    {
        private const float k_VerticalSpacing = 2.0f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                line.y += EditorGUIUtility.singleLineHeight + k_VerticalSpacing;
                AiDefinitionPopupUtility.DrawRoleId(
                    line,
                    property.FindPropertyRelative("_weaponRoleId"),
                    new GUIContent(
                    "Weapon Role Id"));
                line.y += EditorGUIUtility.singleLineHeight + k_VerticalSpacing;
                EditorGUI.PropertyField(
                    line,
                    property.FindPropertyRelative("_combatUtility"),
                    new GUIContent(
                    "Combat Utility"));
                line.y += EditorGUIUtility.singleLineHeight + k_VerticalSpacing;
                EditorGUI.PropertyField(
                    line,
                    property.FindPropertyRelative("_planningUtility"),
                    new GUIContent(
                    "Planning Utility"));
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int lineCount = property.isExpanded ? 4 : 1;

            return lineCount * EditorGUIUtility.singleLineHeight + (lineCount - 1) * k_VerticalSpacing;
        }
    }
}
