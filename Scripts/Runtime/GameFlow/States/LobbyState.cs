using UnityEngine;
using UnityEngine.SceneManagement;

using PranaTactics.Networking;

namespace PranaTactics.GameFlow.States
{
    /// <summary>
    /// ロビーの読み込みとカーソルの表示を管理します。
    /// </summary>
    public sealed class LobbyState : GameState
    {
        private const string k_LobbySceneName = "LobbyScene";

        private bool _isLeaving;

        public override void Enter()
        {
            _isLeaving = false;
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (SceneManager.GetActiveScene().name == k_LobbySceneName)
            {
                OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
            }
            else
            {
                NetworkSceneLoader.LoadScene(k_LobbySceneName);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_isLeaving || scene.name != k_LobbySceneName)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// ロビーでの操作は各UIへ委譲します。
        /// </summary>
        public override void UpdateState()
        {
        }

        public override void Exit()
        {
            _isLeaving = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            HideLobby();
        }

        /// <remarks>
        /// <para>非同期の次シーン準備中も、ロビーを描画・操作させない。</para>
        /// </remarks>
        private static void HideLobby()
        {
            Scene scene = SceneManager.GetSceneByName(k_LobbySceneName);

            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.gameObject.SetActive(false);
                }
            }
        }
    }
}
