using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using PranaTactics.Pranam;
using PranaTactics.Unit;

namespace PranaTactics.Editor.Pranam
{
    /// <summary>
    /// <see cref="UnitParams"/>に設定するメインおよびサブの
    /// プラーナムをドロップダウン形式で編集するカスタムエディターです。
    /// </summary>
    [CustomEditor(typeof(UnitParams))]
    public sealed class PranamSetEditor : UnityEditor.Editor
    {
        /// <summary>
        /// メインプラーナム一覧を参照するSerializedPropertyのパスです。
        /// </summary>
        private const string k_MainPranamPropertyPath =
            "_mainPranamSet._pranamNames";

        /// <summary>
        /// サブプラーナム一覧を参照するSerializedPropertyのパスです。
        /// </summary>
        private const string k_SubPranamPropertyPath =
            "_subPranamSet._pranamNames";

        /// <summary>
        /// デフォルトInspectorから除外する
        /// メインプラーナム設定のルートプロパティ名です。
        /// </summary>
        private const string k_MainPranamRootPropertyName =
            "_mainPranamSet";

        /// <summary>
        /// デフォルトInspectorから除外する
        /// サブプラーナム設定のルートプロパティ名です。
        /// </summary>
        private const string k_SubPranamRootPropertyName =
            "_subPranamSet";

        /// <summary>
        /// メインおよびサブに設定可能なプラーナム数です。
        /// </summary>
        private const int k_PranamSlotCount = 4;

        /// <summary>
        /// プラーナムが設定されていないスロットを表す値です。
        /// </summary>
        private const string k_EmptySlotValue = "NONE";

        /// <summary>
        /// プロジェクト内から取得したプラーナムデータ一覧です。
        /// </summary>
        private readonly List<PranamData> _pranamDataList = new();

        /// <summary>
        /// ドロップダウンへ表示するプラーナム名一覧です。
        /// </summary>
        private readonly List<string> _pranamDisplayNames = new();

        /// <summary>
        /// メインプラーナム設定の折り畳み状態です。
        /// </summary>
        private bool _isMainPranamFoldoutExpanded;

        /// <summary>
        /// サブプラーナム設定の折り畳み状態です。
        /// </summary>
        private bool _isSubPranamFoldoutExpanded;

        /// <summary>
        /// メインプラーナム一覧を参照するプロパティです。
        /// </summary>
        private SerializedProperty _mainPranamProperty;

        /// <summary>
        /// サブプラーナム一覧を参照するプロパティです。
        /// </summary>
        private SerializedProperty _subPranamProperty;

        /// <summary>
        /// カスタムエディターが有効になったとき、
        /// SerializedPropertyとプラーナムデータを読み込みます。
        /// </summary>
        private void OnEnable()
        {
            FindSerializedProperties();
            LoadPranamData();
        }

        /// <summary>
        /// Projectウィンドウ内のアセット構成が変更されたとき、
        /// プラーナムデータ一覧を再読み込みします。
        /// </summary>
        private void OnProjectChange()
        {
            LoadPranamData();
            Repaint();
        }

        /// <summary>
        /// UnitParamsのInspectorを描画します。
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultProperties();

            if (!HasValidPranamProperties())
            {
                DrawMissingPropertyError();
                serializedObject.ApplyModifiedProperties();

                return;
            }

            EnsureMinimumSlotCount(
                _mainPranamProperty);

            EnsureMinimumSlotCount(
                _subPranamProperty);

            EditorGUILayout.Space();

            _isMainPranamFoldoutExpanded =
                DrawPranamSection(
                    "メインプラーナム",
                    _isMainPranamFoldoutExpanded,
                    _mainPranamProperty);

            EditorGUILayout.Space();

            _isSubPranamFoldoutExpanded =
                DrawPranamSection(
                    "サブプラーナム",
                    _isSubPranamFoldoutExpanded,
                    _subPranamProperty);

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// UnitParamsから必要なSerializedPropertyを取得します。
        /// </summary>
        private void FindSerializedProperties()
        {
            _mainPranamProperty =
                serializedObject.FindProperty(
                    k_MainPranamPropertyPath);

            _subPranamProperty =
                serializedObject.FindProperty(
                    k_SubPranamPropertyPath);
        }

        /// <summary>
        /// プロジェクト内に存在する全ての
        /// <see cref="PranamData"/>アセットを読み込みます。
        /// </summary>
        private void LoadPranamData()
        {
            _pranamDataList.Clear();
            _pranamDisplayNames.Clear();

            _pranamDisplayNames.Add(
                k_EmptySlotValue);

            string[] assetGuids =
                AssetDatabase.FindAssets(
                    $"t:{nameof(PranamData)}");

            foreach (string assetGuid in assetGuids)
            {
                string assetPath =
                    AssetDatabase.GUIDToAssetPath(
                        assetGuid);

                PranamData pranamData =
                    AssetDatabase.LoadAssetAtPath<PranamData>(
                        assetPath);

                if (pranamData == null)
                {
                    continue;
                }

                _pranamDataList.Add(
                    pranamData);
            }

            _pranamDataList.Sort(
                ComparePranamData);

            BuildDisplayNameList();
        }

        /// <summary>
        /// プラーナムデータ一覧からドロップダウンへ表示する
        /// 名前一覧を生成します。
        /// </summary>
        private void BuildDisplayNameList()
        {
            HashSet<string> registeredNames =
                new(StringComparer.Ordinal);

            registeredNames.Add(
                k_EmptySlotValue);

            foreach (PranamData pranamData in _pranamDataList)
            {
                string displayName =
                    pranamData.DisplayName;

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    Debug.LogWarning(
                        $"{nameof(PranamSetEditor)}: " +
                        "表示名が設定されていないPranamDataを除外しました。" +
                        $" Asset={pranamData.name}",
                        pranamData);

                    continue;
                }

                if (!registeredNames.Add(displayName))
                {
                    Debug.LogWarning(
                        $"{nameof(PranamSetEditor)}: " +
                        "同じ表示名のPranamDataが複数存在します。" +
                        "文字列だけでは区別できないため、" +
                        "後から見つかったアセットを一覧から除外しました。" +
                        $" DisplayName={displayName}," +
                        $" Asset={pranamData.name}",
                        pranamData);

                    continue;
                }

                _pranamDisplayNames.Add(
                    displayName);
            }
        }

        /// <summary>
        /// 通常のInspector項目を描画します。
        /// </summary>
        /// <remarks>
        /// メインおよびサブのプラーナム設定は
        /// 専用ドロップダウンで描画するため除外します。
        /// </remarks>
        private void DrawDefaultProperties()
        {
            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                k_MainPranamRootPropertyName,
                k_SubPranamRootPropertyName);
        }

        /// <summary>
        /// 指定されたプラーナム設定セクションを描画します。
        /// </summary>
        /// <param name="title">
        /// セクションへ表示するタイトルです。
        /// </param>
        /// <param name="isExpanded">
        /// 現在の折り畳み状態です。
        /// </param>
        /// <param name="pranamArrayProperty">
        /// プラーナム名を保持する配列プロパティです。
        /// </param>
        /// <returns>
        /// 描画後の折り畳み状態です。
        /// </returns>
        private bool DrawPranamSection(
            string title,
            bool isExpanded,
            SerializedProperty pranamArrayProperty)
        {
            isExpanded =
                EditorGUILayout.Foldout(
                    isExpanded,
                    title,
                    true);

            if (!isExpanded)
            {
                return false;
            }

            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox);

            for (int slotIndex = 0;
                slotIndex < k_PranamSlotCount;
                slotIndex++)
            {
                SerializedProperty slotProperty =
                    pranamArrayProperty
                        .GetArrayElementAtIndex(
                            slotIndex);

                string currentValue =
                    slotProperty.stringValue;

                slotProperty.stringValue =
                    DrawPranamDropdown(
                        $"プラーナム {slotIndex + 1}",
                        currentValue);
            }

            EditorGUILayout.EndVertical();

            return true;
        }

        /// <summary>
        /// プラーナムを選択するドロップダウンを描画します。
        /// </summary>
        /// <param name="label">
        /// ドロップダウンへ表示するラベルです。
        /// </param>
        /// <param name="currentValue">
        /// 現在保存されているプラーナム名です。
        /// </param>
        /// <returns>
        /// 選択後のプラーナム名です。
        /// </returns>
        private string DrawPranamDropdown(
            string label,
            string currentValue)
        {
            if (string.IsNullOrWhiteSpace(currentValue))
            {
                currentValue =
                    k_EmptySlotValue;
            }

            int currentIndex =
                _pranamDisplayNames.IndexOf(
                    currentValue);

            if (currentIndex >= 0)
            {
                return DrawRegisteredPranamDropdown(
                    label,
                    currentValue,
                    currentIndex);
            }

            return DrawMissingPranamDropdown(
                label,
                currentValue);
        }

        /// <summary>
        /// 一覧に登録されているプラーナム用の
        /// ドロップダウンを描画します。
        /// </summary>
        /// <param name="label">
        /// ドロップダウンへ表示するラベルです。
        /// </param>
        /// <param name="currentValue">
        /// 現在保存されているプラーナム名です。
        /// </param>
        /// <param name="currentIndex">
        /// 現在選択されている項目のインデックスです。
        /// </param>
        /// <returns>
        /// 選択後のプラーナム名です。
        /// </returns>
        private string DrawRegisteredPranamDropdown(
            string label,
            string currentValue,
            int currentIndex)
        {
            EditorGUI.BeginChangeCheck();

            int selectedIndex =
                EditorGUILayout.Popup(
                    label,
                    currentIndex,
                    _pranamDisplayNames.ToArray());

            if (!EditorGUI.EndChangeCheck())
            {
                return currentValue;
            }

            return _pranamDisplayNames[
                selectedIndex];
        }

        /// <summary>
        /// 現在保存されている名前が一覧に存在しない場合の
        /// ドロップダウンを描画します。
        /// </summary>
        /// <param name="label">
        /// ドロップダウンへ表示するラベルです。
        /// </param>
        /// <param name="missingValue">
        /// 一覧から見つからなかった現在値です。
        /// </param>
        /// <returns>
        /// 選択後のプラーナム名です。
        /// </returns>
        private string DrawMissingPranamDropdown(
            string label,
            string missingValue)
        {
            string[] displayOptions =
                new string[
                    _pranamDisplayNames.Count + 1];

            displayOptions[0] =
                $"Missing: {missingValue}";

            for (int index = 0;
                index < _pranamDisplayNames.Count;
                index++)
            {
                displayOptions[index + 1] =
                    _pranamDisplayNames[index];
            }

            EditorGUI.BeginChangeCheck();

            int selectedIndex =
                EditorGUILayout.Popup(
                    label,
                    0,
                    displayOptions);

            if (!EditorGUI.EndChangeCheck() ||
                selectedIndex == 0)
            {
                return missingValue;
            }

            return _pranamDisplayNames[
                selectedIndex - 1];
        }

        /// <summary>
        /// 指定された配列に最低限必要なスロット数を確保します。
        /// </summary>
        /// <param name="arrayProperty">
        /// スロットを保持する配列プロパティです。
        /// </param>
        private static void EnsureMinimumSlotCount(
            SerializedProperty arrayProperty)
        {
            while (arrayProperty.arraySize <
                k_PranamSlotCount)
            {
                int newElementIndex =
                    arrayProperty.arraySize;

                arrayProperty.InsertArrayElementAtIndex(
                    newElementIndex);

                SerializedProperty newElement =
                    arrayProperty.GetArrayElementAtIndex(
                        newElementIndex);

                newElement.stringValue =
                    k_EmptySlotValue;
            }
        }

        /// <summary>
        /// 必要なSerializedPropertyを取得できているか確認します。
        /// </summary>
        /// <returns>
        /// 両方のプロパティを取得できている場合は<c>true</c>です。
        /// </returns>
        private bool HasValidPranamProperties()
        {
            return _mainPranamProperty != null &&
                _subPranamProperty != null;
        }

        /// <summary>
        /// UnitParamsの構造とプロパティパスが一致しない場合の
        /// エラーメッセージを描画します。
        /// </summary>
        private void DrawMissingPropertyError()
        {
            EditorGUILayout.HelpBox(
                $"{nameof(PranamSetEditor)}が必要なプロパティを" +
                $"{nameof(UnitParams)}から取得できませんでした。\n" +
                $"Main Path: {k_MainPranamPropertyPath}\n" +
                $"Sub Path: {k_SubPranamPropertyPath}\n\n" +
                "UnitParams側のフィールド名を変更した場合は、" +
                "このエディターのSerializedPropertyパスも更新してください。",
                MessageType.Error);
        }

        /// <summary>
        /// プラーナムデータを表示名順に並べ替えるための
        /// 比較処理です。
        /// </summary>
        /// <param name="left">
        /// 比較対象となる左側のデータです。
        /// </param>
        /// <param name="right">
        /// 比較対象となる右側のデータです。
        /// </param>
        /// <returns>
        /// 並べ替えに使用する比較結果です。
        /// </returns>
        private static int ComparePranamData(
            PranamData left,
            PranamData right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int displayNameComparison =
                string.Compare(
                    left.DisplayName,
                    right.DisplayName,
                    StringComparison.Ordinal);

            if (displayNameComparison != 0)
            {
                return displayNameComparison;
            }

            return string.Compare(
                left.name,
                right.name,
                StringComparison.Ordinal);
        }
    }
}
