#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace PranaTactics.Editor
{
    /// <summary>
    /// シーン内のMissing Scriptを検索します。
    /// </summary>
    public static class MissingScriptFinder
    {
        /// <summary>
        /// 開いているシーン内からMissing Scriptを検索します。
        /// </summary>
        [MenuItem("Tools/Prana Tactics/Find Missing Scripts")]
        private static void FindMissingScripts()
        {
            GameObject[] gameObjects =
                Object.FindObjectsByType<GameObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            int missingScriptCount = 0;

            GameObject firstMissingObject = null;

            foreach (GameObject gameObject in gameObjects)
            {
                int missingCount =
                    GameObjectUtility
                        .GetMonoBehavioursWithMissingScriptCount(
                            gameObject);

                if (missingCount <= 0)
                {
                    continue;
                }

                missingScriptCount +=
                    missingCount;

                if (firstMissingObject == null)
                {
                    firstMissingObject =
                        gameObject;
                }

                string sceneName =
                    gameObject.scene.IsValid()
                        ? gameObject.scene.name
                        : "(Invalid Scene)";

                string scenePath =
                    gameObject.scene.IsValid()
                        ? gameObject.scene.path
                        : "(No Scene Path)";

                Debug.LogError(
                    $"Missing Scriptを検出しました。\n" +
                    $"Object={GetHierarchyPath(gameObject)}\n" +
                    $"Scene={sceneName}\n" +
                    $"ScenePath={scenePath}\n" +
                    $"HideFlags={gameObject.hideFlags}\n" +
                    $"EntityId={gameObject.GetEntityId()}\n" +
                    $"ActiveSelf={gameObject.activeSelf}\n" +
                    $"ActiveInHierarchy={gameObject.activeInHierarchy}\n" +
                    $"MissingCount={missingCount}",
                    gameObject);
            }

            Debug.Log(
                $"Missing Scriptの検索が完了しました。" +
                $" Count={missingScriptCount}");

            if (firstMissingObject != null)
            {
                Selection.activeGameObject =
                    firstMissingObject;

                EditorGUIUtility.PingObject(
                    firstMissingObject);

                Debug.Log(
                    $"最初のMissing Scriptオブジェクトを選択しました。" +
                    $" Object={GetHierarchyPath(firstMissingObject)}",
                    firstMissingObject);
            }
        }

        /// <summary>
        /// オブジェクトのHierarchy上のパスを取得します。
        /// </summary>
        private static string GetHierarchyPath(
            GameObject gameObject)
        {
            string path =
                gameObject.name;

            Transform currentTransform =
                gameObject.transform.parent;

            while (currentTransform != null)
            {
                path =
                    $"{currentTransform.name}/{path}";

                currentTransform =
                    currentTransform.parent;
            }

            return path;
        }
    }
}

#endif
