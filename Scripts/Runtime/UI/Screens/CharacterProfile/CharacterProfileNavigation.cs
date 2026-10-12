using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens.CharacterProfile
{
    /// <summary>
    /// ロビーを保持したままプロフィールシーンを開閉します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterProfileNavigation : RepeatClickBehaviorBase, IClickableCondition
    {
        internal const string k_SceneName = "CharacterProfile";

        private bool _isLoading;

        /// <summary>
        /// 接続状態を変更せず、個人用のプロフィールを追加読み込みします。
        /// </summary>
        protected override void OnClickEnter()
        {
            if (!CanClick())
            {
                return;
            }

            StartCoroutine(LoadProfile());
        }

        /// <summary>
        /// ロビーからプロフィールを開けるか判定します。
        /// </summary>
        public bool CanClick()
        {
            return !_isLoading && !SceneManager.GetSceneByName(k_SceneName).isLoaded &&
                (LobbyUIManager.Instance == null || !LobbyUIManager.Instance.IsMatchmaking) &&
                (SettingsUIManager.Instance == null || !SettingsUIManager.Instance.IsOpen);
        }

        /// <summary>
        /// 読み込み完了後にプロフィールをアクティブシーンへ設定します。
        /// </summary>
        private IEnumerator LoadProfile()
        {
            _isLoading = true;
            AsyncOperation operation = SceneManager.LoadSceneAsync(k_SceneName, LoadSceneMode.Additive);

            if (operation != null)
            {
                yield return operation;
                Scene profile = SceneManager.GetSceneByName(k_SceneName);

                if (profile.IsValid() && profile.isLoaded)
                {
                    SceneManager.SetActiveScene(profile);

                    if (EventSystem.current != null)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                    }
                }
            }

            _isLoading = false;
        }
    }
}
