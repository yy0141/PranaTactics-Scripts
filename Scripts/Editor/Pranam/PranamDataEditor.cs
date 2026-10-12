using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

using PranaTactics.Pranam;

namespace PranaTactics.Editor.Pranam
{
    /// <summary>
    /// <see cref="PranamData"/>およびその派生クラスのInspectorに、
    /// プラーナムの動作スクリプトを選択するドロップダウンを追加します。
    /// </summary>
    [CustomEditor(typeof(PranamData), true)]
    public sealed class PranamDataEditor : UnityEditor.Editor
    {
        /// <summary>
        /// <see cref="PranamData"/>の射程または動作識別子を
        /// 保存するフィールドのSerializedPropertyパスです。
        /// </summary>
        private const string k_RangePropertyPath = "_range";

        /// <summary>
        /// プラーナム動作スクリプトを検索するフォルダです。
        /// </summary>
        private const string k_BehaviorScriptFolderPath =
            "Assets/Scripts/Runtime/Pranam/Behavior";

        /// <summary>
        /// 動作スクリプトが選択されていないことを表す値です。
        /// </summary>
        private const string k_NoneOptionName = "None";

        /// <summary>
        /// 動作スクリプトを選択する欄のラベルです。
        /// </summary>
        private const string k_ScriptDropdownLabel = "Behavior Script";

        /// <summary>
        /// プラーナムの動作識別子を保持するプロパティです。
        /// </summary>
        private SerializedProperty _rangeProperty;

        /// <summary>
        /// ドロップダウンへ表示するスクリプト名一覧です。
        /// </summary>
        private readonly List<string> _scriptNames = new();

        /// <summary>
        /// Popupへ渡すためにキャッシュされたスクリプト名配列です。
        /// </summary>
        private string[] _scriptNameOptions =
            Array.Empty<string>();

        /// <summary>
        /// カスタムエディターが有効になったとき、
        /// 必要なプロパティとスクリプト一覧を読み込みます。
        /// </summary>
        private void OnEnable()
        {
            FindSerializedProperties();
            LoadScriptNames();
        }

        /// <summary>
        /// Projectウィンドウ内のアセット構成が変更されたとき、
        /// スクリプト一覧を再読み込みします。
        /// </summary>
        private void OnProjectChange()
        {
            LoadScriptNames();
            Repaint();
        }

        /// <summary>
        /// <see cref="PranamData"/>のInspectorを描画します。
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            EditorGUILayout.Space(20.0f);

            if (_rangeProperty == null)
            {
                DrawMissingPropertyError();
                serializedObject.ApplyModifiedProperties();

                return;
            }

            DrawBehaviorScriptSection();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 必要なSerializedPropertyを取得します。
        /// </summary>
        private void FindSerializedProperties()
        {
            _rangeProperty =
                serializedObject.FindProperty(
                    k_RangePropertyPath);
        }

        /// <summary>
        /// プラーナム動作スクリプトを選択する領域を描画します。
        /// </summary>
        private void DrawBehaviorScriptSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox);

            EditorGUILayout.LabelField(
                "Behavior",
                EditorStyles.boldLabel);

            _rangeProperty.stringValue =
                DrawScriptDropdown(
                    k_ScriptDropdownLabel,
                    _rangeProperty.stringValue);

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 指定フォルダ内にあるMonoScriptのファイル名を読み込みます。
        /// </summary>
        private void LoadScriptNames()
        {
            _scriptNames.Clear();

            _scriptNames.Add(
                k_NoneOptionName);

            if (!AssetDatabase.IsValidFolder(
                    k_BehaviorScriptFolderPath))
            {
                _scriptNameOptions =
                    _scriptNames.ToArray();

                Debug.LogWarning(
                    $"{nameof(PranamDataEditor)}: " +
                    "プラーナム動作スクリプトの検索フォルダが存在しません。" +
                    $" Folder={k_BehaviorScriptFolderPath}");

                return;
            }

            string[] scriptGuids =
                AssetDatabase.FindAssets(
                    "t:MonoScript",
                    new[]
                    {
                        k_BehaviorScriptFolderPath
                    });

            HashSet<string> registeredNames =
                new(StringComparer.Ordinal);

            registeredNames.Add(
                k_NoneOptionName);

            foreach (string scriptGuid in scriptGuids)
            {
                string scriptPath =
                    AssetDatabase.GUIDToAssetPath(
                        scriptGuid);

                if (string.IsNullOrWhiteSpace(scriptPath))
                {
                    continue;
                }

                string scriptName =
                    Path.GetFileNameWithoutExtension(
                        scriptPath);

                if (string.IsNullOrWhiteSpace(scriptName) ||
                    !registeredNames.Add(scriptName))
                {
                    continue;
                }

                _scriptNames.Add(
                    scriptName);
            }

            _scriptNames.Sort(
                1,
                _scriptNames.Count - 1,
                StringComparer.Ordinal);

            _scriptNameOptions =
                _scriptNames.ToArray();
        }

        /// <summary>
        /// 動作スクリプトを選択するドロップダウンを描画します。
        /// </summary>
        /// <param name="label">
        /// ドロップダウンへ表示するラベルです。
        /// </param>
        /// <param name="currentScriptName">
        /// 現在保存されているスクリプト名です。
        /// </param>
        /// <returns>
        /// 選択後のスクリプト名です。
        /// </returns>
        private string DrawScriptDropdown(
            string label,
            string currentScriptName)
        {
            if (string.IsNullOrWhiteSpace(
                    currentScriptName))
            {
                currentScriptName =
                    k_NoneOptionName;
            }

            int currentIndex =
                _scriptNames.IndexOf(
                    currentScriptName);

            if (currentIndex >= 0)
            {
                return DrawRegisteredScriptDropdown(
                    label,
                    currentScriptName,
                    currentIndex);
            }

            return DrawMissingScriptDropdown(
                label,
                currentScriptName);
        }

        /// <summary>
        /// 一覧に存在するスクリプト用のドロップダウンを描画します。
        /// </summary>
        /// <param name="label">
        /// ドロップダウンへ表示するラベルです。
        /// </param>
        /// <param name="currentScriptName">
        /// 現在保存されているスクリプト名です。
        /// </param>
        /// <param name="currentIndex">
        /// 現在選択されている項目のインデックスです。
        /// </param>
        /// <returns>
        /// 選択後のスクリプト名です。
        /// </returns>
        private string DrawRegisteredScriptDropdown(
            string label,
            string currentScriptName,
            int currentIndex)
        {
            EditorGUI.BeginChangeCheck();

            int selectedIndex =
                EditorGUILayout.Popup(
                    label,
                    currentIndex,
                    _scriptNameOptions);

            if (!EditorGUI.EndChangeCheck())
            {
                return currentScriptName;
            }

            if (selectedIndex < 0 ||
                selectedIndex >= _scriptNames.Count)
            {
                return currentScriptName;
            }

            return _scriptNames[selectedIndex];
        }

        /// <summary>
        /// 保存されているスクリプト名が現在の一覧に存在しない場合の
        /// ドロップダウンを描画します。
        /// </summary>
        /// <param name="label">
        /// ドロップダウンへ表示するラベルです。
        /// </param>
        /// <param name="missingScriptName">
        /// 一覧から見つからなかったスクリプト名です。
        /// </param>
        /// <returns>
        /// 選択後のスクリプト名です。
        /// </returns>
        private string DrawMissingScriptDropdown(
            string label,
            string missingScriptName)
        {
            string[] displayOptions =
                new string[
                    _scriptNameOptions.Length + 1];

            displayOptions[0] =
                $"Missing: {missingScriptName}";

            Array.Copy(
                _scriptNameOptions,
                0,
                displayOptions,
                1,
                _scriptNameOptions.Length);

            EditorGUI.BeginChangeCheck();

            int selectedIndex =
                EditorGUILayout.Popup(
                    label,
                    0,
                    displayOptions);

            if (!EditorGUI.EndChangeCheck() ||
                selectedIndex == 0)
            {
                return missingScriptName;
            }

            int scriptIndex =
                selectedIndex - 1;

            if (scriptIndex < 0 ||
                scriptIndex >= _scriptNames.Count)
            {
                return missingScriptName;
            }

            return _scriptNames[scriptIndex];
        }

        /// <summary>
        /// 対象プロパティを取得できない場合の
        /// エラーメッセージを描画します。
        /// </summary>
        private void DrawMissingPropertyError()
        {
            EditorGUILayout.HelpBox(
                $"{nameof(PranamDataEditor)}が" +
                $"{nameof(PranamData)}の動作識別子を取得できませんでした。\n" +
                $"Property Path: {k_RangePropertyPath}\n\n" +
                $"{nameof(PranamData)}側のフィールド名を変更した場合は、" +
                "カスタムエディターのSerializedPropertyパスも更新してください。",
                MessageType.Error);
        }
    }
}
