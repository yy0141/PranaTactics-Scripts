using UnityEditor;
using UnityEngine;

namespace PranaTactics.AI.Solo.Editor
{
    /// <summary>
    /// AIカタログの元データ参照とEditor同期操作を表示します。
    /// </summary>
    [CustomEditor(typeof(AiDefinitionCatalog))]
    public sealed class AiDefinitionCatalogEditor : UnityEditor.Editor
    {
        /// <summary>
        /// 参照元のある共通項目を読み取り専用で表示します。
        /// </summary>
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "元データを指定した共通項目はEditorで自動同期・保存されます。" +
                "数値は兵装データ、名前と分類はキャラクターデータで変更してください。" +
                "AI固有の判断値、能力タグと視認条件はこのカタログで調整できます。",
                MessageType.Info);
            DrawDefaultInspector();

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("元データからすべてのAIカタログを同期"))
                {
                    AiDefinitionCatalogSynchronizer.SynchronizeAll();
                }
            }
        }
    }
}
