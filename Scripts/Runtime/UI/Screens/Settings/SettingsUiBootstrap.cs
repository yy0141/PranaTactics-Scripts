using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 全画面設定UIの初回ロードを共有し、生成済みUIを再利用します。
    /// </summary>
    public static class SettingsUiBootstrap
    {
        private static Task s_loadingTask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            s_loadingTask = null;
        }

        public static Task EnsureLoadedAsync()
        {
            if (SettingsUIManager.Instance != null)
            {
                return Task.CompletedTask;
            }

            return s_loadingTask ??= LoadAsync();
        }

        private static async Task LoadAsync()
        {
            try
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(
                    "SettingsUIScene",
                    LoadSceneMode.Additive);

                while (!operation.isDone)
                {
                    await Awaitable.NextFrameAsync(Application.exitCancellationToken);
                }

                if (SettingsUIManager.Instance == null)
                {
                    throw new InvalidOperationException("設定UIを初期化できませんでした。");
                }
            }
            catch
            {
                s_loadingTask = null;
                throw;
            }
        }
    }
}
