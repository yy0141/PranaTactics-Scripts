using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using PranaTactics.Pranam;
using PranaTactics.Unit;

namespace PranaTactics.AI.Solo.Editor
{
    /// <summary>
    /// プロジェクト内の元データ変更をAIカタログへ保存します。
    /// </summary>
    [InitializeOnLoad]
    public static class AiDefinitionCatalogSynchronizer
    {
        private static bool s_isQueued;

        private static bool s_isSynchronizing;

        /// <summary>
        /// Inspector変更、Undoとスクリプト再読み込み後の同期を登録します。
        /// </summary>
        static AiDefinitionCatalogSynchronizer()
        {
            Undo.postprocessModifications += OnModifications;
            Undo.undoRedoPerformed += QueueSynchronization;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            QueueSynchronization();
        }

        /// <summary>
        /// AssetDatabaseを操作できるタイミングへ同期を延期します。
        /// </summary>
        public static void QueueSynchronization()
        {
            if (s_isQueued || s_isSynchronizing)
            {
                return;
            }

            s_isQueued = true;
            EditorApplication.delayCall += SynchronizeAll;
        }

        /// <summary>
        /// プロジェクト内のすべてのカタログを同期して保存します。
        /// </summary>
        [MenuItem("Prana Tactics/AI/Synchronize Definition Catalogs")]
        public static void SynchronizeAll()
        {
            s_isQueued = false;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueSynchronization();
                return;
            }

            s_isSynchronizing = true;

            try
            {
                List<PranamData> weapons = LoadAssets<PranamData>();
                List<UnitParams> characters = LoadAssets<UnitParams>();

                foreach (AiDefinitionCatalog catalog in LoadAssets<AiDefinitionCatalog>())
                {
                    if (SynchronizeCatalog(catalog, weapons, characters))
                    {
                        AssetDatabase.SaveAssetIfDirty(catalog);
                    }
                }
            }
            finally
            {
                s_isSynchronizing = false;
            }
        }

        /// <summary>
        /// AI固有の判断値を保持し、参照元から取得できる項目だけを更新します。
        /// </summary>
        public static bool SynchronizeCatalog(
            AiDefinitionCatalog catalog,
            IReadOnlyList<PranamData> weapons,
            IReadOnlyList<UnitParams> characters)
        {
            if (catalog == null)
            {
                return false;
            }

            using var serialized = new SerializedObject(catalog);
            SerializedProperty definitions = serialized.FindProperty("_weapons");

            for (int index = 0; index < definitions.arraySize; index++)
            {
                SynchronizeWeapon(definitions.GetArrayElementAtIndex(index), weapons);
            }

            SerializedProperty bindings = serialized.FindProperty("_characters");

            for (int index = 0; index < bindings.arraySize; index++)
            {
                SynchronizeCharacter(bindings.GetArrayElementAtIndex(index), characters);
            }

            return serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 兵装IDまたは保存済みの参照から元データを特定して共通値を更新します。
        /// </summary>
        private static void SynchronizeWeapon(
            SerializedProperty definition,
            IReadOnlyList<PranamData> weapons)
        {
            SerializedProperty reference = definition.FindPropertyRelative("_sourceData");
            PranamData source = reference.objectReferenceValue as PranamData;

            if (source == null)
            {
                string id = definition.FindPropertyRelative("_id").stringValue;
                source = FindUnique(weapons, weapon => string.Equals(weapon.WeaponAiDefinitionId, id, StringComparison.OrdinalIgnoreCase));
            }

            if (source == null)
            {
                return;
            }

            reference.objectReferenceValue = source;

            if (!string.IsNullOrWhiteSpace(source.WeaponAiDefinitionId))
            {
                definition.FindPropertyRelative("_id").stringValue = source.WeaponAiDefinitionId;
            }

            using var sourceProperties = new SerializedObject(source);
            CopyFloat(sourceProperties, "_diameter", definition, "_fallbackMaximumRange");
            CopyFloat(sourceProperties, "_attackPower", definition, "_fallbackExpectedDamage");
            definition.FindPropertyRelative("_fallbackPranaCost").floatValue = Mathf.Max(0, source.PranaCost);

            Type behaviorType = typeof(PranamBase).Assembly.GetType("PranaTactics.Pranam." + source.Range);

            if (behaviorType != null && typeof(PranamBase).IsAssignableFrom(behaviorType))
            {
                definition.FindPropertyRelative("_usesHold").boolValue = typeof(AutomaticGunnerBehavior).IsAssignableFrom(behaviorType);
            }

            AiUnitRole? role = GetWeaponRole(source);

            if (role.HasValue)
            {
                definition.FindPropertyRelative("_roleId").stringValue = ResolveRoleId(role.Value);
            }
        }

        /// <summary>
        /// キャラクター名と分類を元データから更新します。
        /// </summary>
        private static void SynchronizeCharacter(
            SerializedProperty binding,
            IReadOnlyList<UnitParams> characters)
        {
            SerializedProperty reference = binding.FindPropertyRelative("_sourceCharacter");
            UnitParams source = reference.objectReferenceValue as UnitParams;

            if (source == null)
            {
                string name = binding.FindPropertyRelative("_characterId").stringValue;
                source = FindUnique(characters, character => string.Equals(character.UnitName, name, StringComparison.Ordinal));
            }

            if (source == null)
            {
                return;
            }

            reference.objectReferenceValue = source;
            binding.FindPropertyRelative("_characterId").stringValue = source.UnitName;
            AiUnitRole role = source.Type switch
            {
                UnitParams.CharacterType.Attacker => AiUnitRole.Attacker,
                UnitParams.CharacterType.Shooter => AiUnitRole.Shooter,
                UnitParams.CharacterType.Gunner => AiUnitRole.Gunner,
                UnitParams.CharacterType.Sniper => AiUnitRole.Sniper,
                _ => AiUnitRole.GenericSafe
            };
            binding.FindPropertyRelative("_roleId").stringValue = ResolveRoleId(role);
        }

        /// <summary>
        /// 型から確定できる兵装ロールだけを取得します。
        /// </summary>
        public static AiUnitRole? GetWeaponRole(PranamData source)
        {
            return source switch
            {
                AttackerPranamData => AiUnitRole.Attacker,
                ShooterPranamData => AiUnitRole.Shooter,
                GunnerPranamData => AiUnitRole.Gunner,
                SniperPranamData => AiUnitRole.Sniper,
                _ => null
            };
        }

        /// <summary>
        /// 元データの分類に対応する標準ロールIDを取得します。
        /// </summary>
        private static string ResolveRoleId(AiUnitRole role)
        {
            return role switch
            {
                AiUnitRole.Attacker => AiRoleIds.Attacker,
                AiUnitRole.Shooter => AiRoleIds.Shooter,
                AiUnitRole.Gunner => AiRoleIds.Gunner,
                AiUnitRole.Sniper => AiRoleIds.Sniper,
                _ => AiRoleIds.GenericSafe
            };
        }

        /// <summary>
        /// 元データに存在する数値だけをコピーします。
        /// </summary>
        private static void CopyFloat(SerializedObject source, string sourceName, SerializedProperty target, string targetName)
        {
            SerializedProperty value = source.FindProperty(sourceName);

            if (value != null)
            {
                target.FindPropertyRelative(targetName).floatValue = Mathf.Max(0.0f, value.floatValue);
            }
        }

        /// <summary>
        /// 重複候補がある場合は推測で元データを選びません。
        /// </summary>
        private static T FindUnique<T>(IReadOnlyList<T> sources, Predicate<T> matches) where T : UnityEngine.Object
        {
            T result = null;

            foreach (T source in sources)
            {
                if (source == null || !matches(source))
                {
                    continue;
                }

                if (result != null)
                {
                    Debug.LogWarning("AIカタログの同期元が重複しています。Inspectorで元データを指定してください。", source);
                    return null;
                }

                result = source;
            }

            return result;
        }

        /// <summary>
        /// 自作データアセットだけを読み込みます。
        /// </summary>
        private static List<T> LoadAssets<T>() where T : UnityEngine.Object
        {
            var result = new List<T>();

            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/Datas", "Assets/Resources" }))
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));

                if (asset != null)
                {
                    result.Add(asset);
                }
            }

            return result;
        }

        /// <summary>
        /// 関連するデータのInspector変更だけに同期を予約します。
        /// </summary>
        private static UndoPropertyModification[] OnModifications(UndoPropertyModification[] modifications)
        {
            foreach (UndoPropertyModification modification in modifications)
            {
                if (modification.currentValue.target is PranamData or UnitParams or AiDefinitionCatalog)
                {
                    QueueSynchronization();
                    break;
                }
            }

            return modifications;
        }

        /// <summary>
        /// プレイ中に保留したデータ同期を編集モードへ戻った後に行います。
        /// </summary>
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                QueueSynchronization();
            }
        }
    }

    /// <summary>
    /// データの再インポートと移動後にカタログ同期を予約します。
    /// </summary>
    public sealed class AiDefinitionCatalogPostprocessor : AssetPostprocessor
    {
        /// <summary>
        /// 関連アセット変更後にEditorの遅延同期を開始します。
        /// </summary>
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string[] paths in new[] { imported, deleted, moved, movedFrom })
            {
                foreach (string path in paths)
                {
                    if (path.StartsWith("Assets/Datas/", StringComparison.Ordinal) ||
                        path.StartsWith("Assets/Resources/", StringComparison.Ordinal))
                    {
                        AiDefinitionCatalogSynchronizer.QueueSynchronization();
                        return;
                    }
                }
            }
        }
    }
}
