using System;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

using PranaTactics.GameFlow;
using PranaTactics.GameFlow.States;
using PranaTactics.Match.Phases;
using PranaTactics.Settings;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 永続設定画面の生成とゲームシーンへの接続を担当します。
    /// </summary>
    public sealed class SettingsUIManager : MonoBehaviour
    {
        [SerializeField]
        private TMP_FontAsset _font;

        private SettingsScreenView _view;

        private GameMenuView _menu;

        private MenuConfirmationView _confirmation;

        private bool _isPreviousCursorVisible;

        private CursorLockMode _previousCursorLockMode;

        private bool _hasStoredCursorState;

        public static SettingsUIManager Instance
        {
            get; private set;
        }

        public bool IsOpen => (_view != null && _view.Root.gameObject.activeSelf) ||
            (_menu != null && _menu.Root.gameObject.activeSelf);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);

                return;
            }

            var catalog = new SettingsCatalog();
            TextAsset asset = Resources.Load<TextAsset>("Settings/settings-view");

            if (asset == null)
            {
                throw new InvalidOperationException("settings-view.jsonがありません。");
            }

            SettingsViewDefinition definition = JsonConvert.DeserializeObject<SettingsViewDefinition>(
                asset.text);

            definition.Validate(catalog);
            var presenter = new SettingsPresenter(
                catalog,
                () => SettingsManager.Current,
                SettingsManager.TrySave,
                SettingsManager.NotifyChanged);

            _view = new SettingsScreenView(
                transform,
                _font,
                definition,
                presenter,
                ReturnToMenu,
                BackToLobby);
            _menu = new GameMenuView(
                transform,
                _font,
                OpenSettings,
                Close,
                RequestQuit,
                StartTutorial,
                RequestBackToLobby);
            _confirmation = new MenuConfirmationView(transform, _font);
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void Update()
        {
            if (Training.TrainingCharacterSelectionView.IsOpen ||
                SceneManager.GetSceneByName(CharacterProfile.CharacterProfileNavigation.k_SceneName).isLoaded ||
                PranamProfile.PranamProfileNavigation.IsOpenOrLoading)
            {
                return;
            }

            if (_view != null && _view.PollKeyCapture())
            {
                return;
            }

            bool shouldToggle = Input.GetKeyDown(KeyCode.Escape);

            if (shouldToggle)
            {
                Toggle();
            }
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            _view?.Dispose();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// シーン遷移時はUIを保持したまま閉じ、シーン固有操作を無効にします。
        /// </summary>
        private void OnActiveSceneChanged(Scene previous, Scene current)
        {
            _confirmation.Hide();

            if (IsOpen)
            {
                _view.FlushAndSave();
                _view.Root.gameObject.SetActive(false);
                _menu.Root.gameObject.SetActive(false);
                RestoreCursorState();
            }

            _view.ConfigureVisibility(false);
        }

        public void Initialize()
        {
            _confirmation.Hide();
            _view.Root.gameObject.SetActive(false);
            _menu.Root.gameObject.SetActive(false);
        }

        public void Toggle()
        {
            if (_confirmation.IsOpen)
            {
                _confirmation.Hide();

                return;
            }

            if (IsOpen)
            {
                if (_view.Root.gameObject.activeSelf)
                {
                    ReturnToMenu();
                }
                else
                {
                    Close();
                }
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            StoreCursorState();
            ConfigureMenu();
            _menu.Root.gameObject.SetActive(true);
            VisualizeCursor(true);
        }

        /// <summary>
        /// メニューから生成済みの設定画面へ切り替えます。
        /// </summary>
        private void OpenSettings()
        {
            _menu.Root.gameObject.SetActive(false);
            _view.Root.gameObject.SetActive(true);
            _view.Refresh();
        }

        /// <summary>
        /// 入力中の設定を保存してメニューへ戻ります。
        /// </summary>
        private void ReturnToMenu()
        {
            if (!_view.FlushAndSave())
            {
                return;
            }

            _view.Root.gameObject.SetActive(false);
            ConfigureMenu();
            _menu.Root.gameObject.SetActive(true);
        }

        /// <summary>
        /// シーンとプレイモードに応じてメニュー項目を更新します。
        /// </summary>
        private void ConfigureMenu()
        {
            string scene = SceneManager.GetActiveScene().name;

            _menu.Configure(MenuScenePolicy.CanStartTutorial(scene), CanReturnToLobby(), CanLeaveGame());
        }

        /// <summary>
        /// 対戦中のメニューからの退出を許可するか確認します。
        /// </summary>
        private bool CanLeaveGame()
        {
            return MenuScenePolicy.CanLeaveGame(SceneManager.GetActiveScene().name,
                GameFlowManager.Instance != null && GameFlowManager.Instance.IsSoloPlay);
        }

        /// <summary>
        /// 現在のシーンからロビーへ戻れるか確認します。
        /// </summary>
        private bool CanReturnToLobby()
        {
            return MenuScenePolicy.CanReturnToLobby(SceneManager.GetActiveScene().name,
                GameFlowManager.Instance != null && GameFlowManager.Instance.IsSoloPlay);
        }

        /// <summary>
        /// ロビー限定でチュートリアルへ進みます。
        /// </summary>
        private void StartTutorial()
        {
            if (!MenuScenePolicy.CanStartTutorial(SceneManager.GetActiveScene().name))
            {
                return;
            }

            Close();

            if (!IsOpen)
            {
                GameFlowManager.Instance?.ChangeState(new TutorialState());
            }
        }

        /// <summary>
        /// ロビーへ戻る前に確認します。
        /// </summary>
        private void RequestBackToLobby()
        {
            if (CanReturnToLobby())
            {
                _confirmation.Show("ロビーへ戻りますか？\n現在のプレイを終了します。", BackToLobby);
            }
        }

        /// <summary>
        /// アプリを終了する前に確認します。
        /// </summary>
        private void RequestQuit()
        {
            if (!CanLeaveGame())
            {
                return;
            }

            _confirmation.Show("ゲームを終了して\nデスクトップへ戻りますか？", QuitGame);
        }

        /// <summary>
        /// 設定を保存してアプリを終了します。Editorでは再生を終了します。
        /// </summary>
        private void QuitGame()
        {
            if (!CanLeaveGame())
            {
                return;
            }

            Close();

            if (IsOpen)
            {
                return;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void Close()
        {
            if (!IsOpen || !_view.FlushAndSave())
            {
                return;
            }

            _view.Root.gameObject.SetActive(false);
            _menu.Root.gameObject.SetActive(false);
            _confirmation.Hide();
            RestoreCursorState();
        }

        public void EnableUI(bool enable)
        {
            if (enable)
            {
                Open();
            }
            else
            {
                Close();
            }
        }

        public bool IsOpenedUI()
        {
            return IsOpen;
        }

        public void SaveAllInputFields()
        {
            _view.FlushAndSave();
        }

        public void VisualizeCursor(bool enable)
        {
            if (enable)
            {
                StoreCursorState();
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                RestoreCursorState();
            }
        }

        private void StoreCursorState()
        {
            if (_hasStoredCursorState)
            {
                return;
            }

            _isPreviousCursorVisible = Cursor.visible;
            _previousCursorLockMode = Cursor.lockState;
            _hasStoredCursorState = true;
        }

        private void RestoreCursorState()
        {
            if (!_hasStoredCursorState)
            {
                return;
            }

            Cursor.visible = _isPreviousCursorVisible;
            Cursor.lockState = _previousCursorLockMode;
            _hasStoredCursorState = false;

            if (MatchPhaseManager.Instance != null &&
                MatchPhaseManager.Instance.CurrentPhase != MatchPhase.None)
            {
                MatchPhaseManager.Instance.RefreshCursorState();
            }
        }

        private void BackToLobby()
        {
            if (!CanReturnToLobby())
            {
                return;
            }

            Close();

            if (!IsOpen)
            {
                GameFlowManager.Instance?.ChangeState(new LobbyState());
            }
        }
    }
}
