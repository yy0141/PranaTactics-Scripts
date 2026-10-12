using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PranaTactics.UI.Screens.Header
{
    /// <summary>
    /// 対象画面から共通ヘッダーを一度だけ追加読み込みします。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeaderUiBootstrap : MonoBehaviour
    {
        /// <summary>
        /// シーンを跨いで完了を確認するヘッダー読み込み要求です。
        /// </summary>
        private static AsyncOperation s_loadOperation;

        /// <summary>
        /// 再生開始時に読み込み状態を初期化します。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            s_loadOperation = null;
        }

        /// <summary>
        /// 他画面からの同時要求を共有します。
        /// </summary>
        private IEnumerator Start()
        {
            while (!SceneManager.GetSceneByName("HeaderUI").isLoaded)
            {
                if (s_loadOperation == null || s_loadOperation.isDone)
                {
                    s_loadOperation = SceneManager.LoadSceneAsync("HeaderUI", LoadSceneMode.Additive);
                    if (s_loadOperation == null)
                    {
                        Debug.LogError("HeaderUIを読み込めませんでした。ビルドのシーン設定を確認してください。", this);
                        yield break;
                    }
                }

                yield return s_loadOperation;
            }
        }
    }
}
