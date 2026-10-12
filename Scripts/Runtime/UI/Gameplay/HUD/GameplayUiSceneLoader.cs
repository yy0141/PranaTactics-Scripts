using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

using PranaTactics.UI.Screens;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// ゲームシーンに所属し、共通UIシーンの寿命と表示項目を管理します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayUiSceneLoader : MonoBehaviour
    {
        [SerializeField]
        private string _sceneName = "GameplayUIScene";

        [SerializeField]
        private bool _settingsOnly;

        [SerializeField]
        private GameplayUiVisibility _visibility = new();

        /// <remarks>
        /// <para>前のシーンの読み込み・破棄が終わるまで次のUIを生成しません。</para>
        /// </remarks>
        private static readonly SemaphoreSlim s_loadingGate = new(1, 1);

        private Task _loadTask;

        private Scene _uiScene;

        private Scene _macroScene;

        private const string k_MacroSceneName = "MacroPhaseUIScene";

        private Exception _initializationError;

        private async void Start()
        {
            try
            {
                await GetLoadTask();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// ボードや兵装の初期化前に共通UIの準備を待ちます。
        /// </summary>
        public static async Task EnsureLoadedAsync(CancellationToken cancellationToken)
        {
            GameplayUiSceneLoader loader = null;

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                loader = root.GetComponentInChildren<GameplayUiSceneLoader>(true);

                if (loader != null)
                {
                    break;
                }
            }

            if (loader == null)
            {
                throw new InvalidOperationException("ゲームシーンにGameplayUiSceneLoaderがありません。");
            }

            Task task = loader.GetLoadTask();

            while (!task.IsCompleted)
            {
                await Awaitable.NextFrameAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await task;

            if (loader == null ||
                (!loader._settingsOnly &&
                (!loader._uiScene.isLoaded ||
                !loader._macroScene.isLoaded)) ||
                SettingsUIManager.Instance == null)
            {
                throw new OperationCanceledException("共通UIの読み込み中にゲームシーンが終了しました。");
            }
        }

        private Task GetLoadTask() => _loadTask ??= LoadAsync();

        /// <remarks>
        /// <para>UIはローカルに追加し、ゲームシーンをアクティブのまま維持します。</para>
        /// </remarks>
        private async Task LoadAsync()
        {
            await s_loadingGate.WaitAsync();

            try
            {
                if (this == null)
                {
                    return;
                }

                Scene existing = SceneManager.GetSceneByName(_sceneName);

                if (existing.IsValid() && existing.isLoaded)
                {
                    await UnloadAsync(existing);
                }

                Scene existingMacro = SceneManager.GetSceneByName(k_MacroSceneName);

                await UnloadAsync(existingMacro);

                if (this == null)
                {
                    return;
                }

                SceneManager.sceneLoaded += OnUiSceneLoaded;

                try
                {
                    if (!_settingsOnly)
                    {
                        await WaitForOperationAsync(
                            SceneManager.LoadSceneAsync(
                            _sceneName,
                            LoadSceneMode.Additive));

                        if (this != null)
                        {
                            await WaitForOperationAsync(
                                SceneManager.LoadSceneAsync(
                                k_MacroSceneName,
                                LoadSceneMode.Additive));
                        }
                    }

                    if (this != null && _initializationError == null)
                    {
                        await SettingsUiBootstrap.EnsureLoadedAsync();


                    }
                }
                finally
                {
                    SceneManager.sceneLoaded -= OnUiSceneLoaded;
                }

                if (this == null)
                {
                    await UnloadAsync(_macroScene);
                    await UnloadAsync(_uiScene);

                    return;
                }

                if (_initializationError != null)
                {
                    await UnloadAsync(_macroScene);
                    await UnloadAsync(_uiScene);

                    throw new InvalidOperationException("共通UIの表示設定を適用できませんでした。", _initializationError);
                }

                if ((!_settingsOnly &&
                    (!_uiScene.IsValid() || !_uiScene.isLoaded ||
                    FindView(_uiScene) == null ||
                    !_macroScene.isLoaded)) ||
                    SettingsUIManager.Instance == null)
                {
                    throw new InvalidOperationException($"{_sceneName}の共通UIを初期化できませんでした。");
                }
            }
            catch
            {

                await UnloadAsync(_macroScene);
                await UnloadAsync(_uiScene);
                throw;
            }
            finally
            {
                s_loadingGate.Release();
            }
        }

        private void OnUiSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Additive)
            {
                return;
            }

            if (scene.name == k_MacroSceneName)
            {
                _macroScene = scene;

                if (this == null)
                {
                    Hide(scene);
                }

                return;
            }

            if (scene.name != _sceneName)
            {
                return;
            }

            _uiScene = scene;

            if (this == null)
            {
                Hide(scene);

                return;
            }

            try
            {
                GameplayUiView view = FindView(scene);

                if (view == null)
                {
                    throw new InvalidOperationException("GameplayUiViewがありません。");
                }

                view.Apply(_visibility);
            }
            catch (Exception exception)
            {
                _initializationError = exception;
                Hide(scene);
            }
        }

        private static GameplayUiView FindView(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameplayUiView view = root.GetComponentInChildren<GameplayUiView>(true);

                if (view != null)
                {
                    return view;
                }
            }

            return null;
        }

        /// <remarks>
        /// <para>Single遷移中はUnityがUIも破棄します。個別Unloadにも対応します。</para>
        /// </remarks>
        private async void OnDestroy()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Hide(_macroScene);
            Hide(_uiScene);

            try
            {
                await s_loadingGate.WaitAsync();

                try
                {
                    await Awaitable.NextFrameAsync(Application.exitCancellationToken);
                    await UnloadAsync(_macroScene);
                    await UnloadAsync(_uiScene);

                }
                finally
                {
                    s_loadingGate.Release();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void Hide(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                root.SetActive(false);
            }
        }

        private static async Task UnloadAsync(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            Hide(scene);
            AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);

            if (operation != null)
            {
                await WaitForOperationAsync(operation);
            }
        }

        private static async Task WaitForOperationAsync(AsyncOperation operation)
        {
            if (operation == null)
            {
                throw new InvalidOperationException("UIシーンの読み込みを開始できませんでした。");
            }

            while (!operation.isDone)
            {
                await Awaitable.NextFrameAsync(Application.exitCancellationToken);
            }
        }
    }
}
