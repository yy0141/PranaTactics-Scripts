using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 共通リザルトUIシーンを所有シーンの寿命に合わせて読み込みます。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResultUiSceneLoader : MonoBehaviour
    {
        /// <summary>
        /// ローカルで追加するUIシーン名です。
        /// </summary>
        private const string k_SceneName = "ResultUIScene";

        /// <summary>
        /// 二重生成を防ぎ、読み込み途中の遷移でもUIを破棄します。
        /// </summary>
        private async void Start()
        {
            try
            {
                if (!SceneManager.GetSceneByName(k_SceneName).isLoaded)
                {
                    AsyncOperation operation = SceneManager.LoadSceneAsync(k_SceneName, LoadSceneMode.Additive);
                    while (!operation.isDone)
                    {
                        await Awaitable.NextFrameAsync();
                    }
                    if (this == null && SceneManager.GetSceneByName(k_SceneName).isLoaded)
                    {
                        SceneManager.UnloadSceneAsync(k_SceneName);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// 所有シーンを離れるときにUIを破棄します。
        /// </summary>
        private void OnDestroy()
        {
            if (SceneManager.GetSceneByName(k_SceneName).isLoaded)
            {
                SceneManager.UnloadSceneAsync(k_SceneName);
            }
        }
    }
}
