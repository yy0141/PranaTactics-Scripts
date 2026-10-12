using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

using PranaTactics.Pranam;
using PranaTactics.UI.Interaction;
using PranaTactics.UI.Screens.CharacterProfile;

namespace PranaTactics.UI.Screens.PranamProfile
{
    /// <summary>
    /// ロビーまたはキャラクターの装備から、兵装プロフィールを追加読み込みします。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PranamProfileNavigation : RepeatClickBehaviorBase, IClickableCondition
    {
        internal const string k_SceneName = "PranamProfile";

        [SerializeField]
        private CharacterProfileView _characterProfile;

        [Range(0, 7)]
        [SerializeField]
        private int _slotIndex;

        private static bool s_isLoading;

        internal static PranamData RequestedWeapon { get; private set; }

        internal static string ReturnSceneName { get; private set; }

        internal static bool IsLoading => s_isLoading;

        internal static bool IsOpenOrLoading => s_isLoading || SceneManager.GetSceneByName(k_SceneName).isLoaded;

        /// <summary>
        /// 兵装閲覧の要求と戻り先を設定します。
        /// </summary>
        internal static void PrepareRequest(PranamData weapon, string returnScene)
        {
            RequestedWeapon = weapon;
            ReturnSceneName = returnScene;
        }

        /// <summary>
        /// ドメイン再読み込みを無効にした再生でも、前回の要求を引き継ぎません。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            s_isLoading = false;
            RequestedWeapon = null;
            ReturnSceneName = null;
        }

        /// <summary>
        /// 空装備、未登録の兵装、マッチング中および重複読み込みを拒否します。
        /// </summary>
        public bool CanClick()
        {
            if (IsOpenOrLoading || (SettingsUIManager.Instance != null && SettingsUIManager.Instance.IsOpen))
            {
                return false;
            }

            if (_characterProfile != null)
            {
                return _characterProfile.CanInteract && _characterProfile.TryGetPranam(_slotIndex, out _);
            }

            return !SceneManager.GetSceneByName("CharacterProfile").isLoaded &&
                (LobbyUIManager.Instance == null || !LobbyUIManager.Instance.IsMatchmaking);
        }

        /// <summary>
        /// 遷移元と選択兵装を保存して閲覧画面を開きます。
        /// </summary>
        protected override void OnClickEnter()
        {
            if (!CanClick())
            {
                return;
            }

            PranamData weapon = null;

            if (_characterProfile != null)
            {
                _characterProfile.TryGetPranam(_slotIndex, out weapon);
            }

            RequestedWeapon = weapon;
            ReturnSceneName = gameObject.scene.name;
            StartCoroutine(LoadProfile());
        }

        /// <summary>
        /// ローカルの追加シーンとして読み込み、通信と装備データを変更しません。
        /// </summary>
        private IEnumerator LoadProfile()
        {
            s_isLoading = true;

            try
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(k_SceneName, LoadSceneMode.Additive);

                if (operation == null)
                {
                    yield break;
                }

                yield return operation;
                Scene scene = SceneManager.GetSceneByName(k_SceneName);

                if (scene.IsValid() && scene.isLoaded)
                {
                    SceneManager.SetActiveScene(scene);
                }
            }
            finally
            {
                s_isLoading = false;
            }
        }
    }
}
