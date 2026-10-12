using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using PranaTactics.Settings;
using PranaTactics.GameFlow;
using PranaTactics.GameFlow.States;
using PranaTactics.Match.Modes;
using PranaTactics.UI.Screens.PranamProfile;
using PranaTactics.UI.Screens.GamePlayMode;

namespace PranaTactics.UI.Screens.Header
{
    /// <summary>
    /// 共通ヘッダーの表示と、ローカル閲覧シーン間の遷移を管理します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeaderNavigationView : MonoBehaviour
    {
        [SerializeField]
        private Canvas _canvas;

        [SerializeField]
        private GameObject[] _selectionMarkers;

        [SerializeField]
        private TMP_Text _playLabel;

        [SerializeField]
        private Matchmaking _matchmaking;

        [SerializeField]
        private Graphic _playBackground;

        /// <summary>
        /// ソロ専用時に非表示にするモード選択ボタンです。
        /// </summary>
        [SerializeField]
        private GameObject _modeButton;

        private Color _idlePlayColor;

        private Color _idlePlayTextColor;

        private bool _isNavigating;

        /// <summary>
        /// 既存の入力管理を共有します。
        /// </summary>
        private void Awake()
        {
            if (_modeButton != null)
            {
                _modeButton.SetActive(!GameBuildSettings.IsSoloOnly);
            }

            _idlePlayColor = _playBackground.color;
            _idlePlayTextColor = _playLabel.color;
            if (UIManager.Instance == null)
            {
                new GameObject("UIManager").AddComponent<UIManager>();
            }
        }

        /// <summary>
        /// アクティブな画面に合わせて選択表示を更新します。
        /// </summary>
        private void Update()
        {
            string scene = SceneManager.GetActiveScene().name;
            _canvas.enabled = IsSupportedScene(scene);
            bool isMatching = scene == "LobbyScene" && _matchmaking != null &&
                (_matchmaking.IsMatching || _matchmaking.IsReady);
            _playLabel.text = scene != "LobbyScene" ? "HOME" : isMatching ? "PLAY　×" : "PLAY";
            ApplyPlayColor(_playBackground, isMatching ? Color.black : _idlePlayColor);
            ApplyPlayColor(_playLabel, isMatching ? Color.white : _idlePlayTextColor);
            for (int i = 0; i < _selectionMarkers.Length; i++)
            {
                if (_selectionMarkers[i] == null)
                {
                    continue;
                }

                _selectionMarkers[i].SetActive(
                    (i == 1 && scene == "CharacterProfile") ||
                    (i == 2 && scene == "PranamProfile") ||
                    (i == 5 && scene == "GamePlayModeUI"));
            }
        }

        /// <summary>
        /// 既存ホバー処理の透明度を保ったまま、背景と文字の色を切り替えます。
        /// </summary>
        private static void ApplyPlayColor(Graphic graphic, Color color)
        {
            color.a = graphic.color.a;
            graphic.color = color;
        }

        /// <summary>
        /// 共通ヘッダーを表示する画面か判定します。
        /// </summary>
        private static bool IsSupportedScene(string scene)
        {
            return scene == "LobbyScene" || scene == "CharacterProfile" || scene == "PranamProfile" || scene == "GamePlayModeUI";
        }

        /// <summary>
        /// 設定表示中、マッチング中および遷移中の重複操作を防ぎます。
        /// </summary>
        public bool CanNavigate(HeaderDestination destination)
        {
            bool isLobbyPlay = destination == HeaderDestination.Play &&
                SceneManager.GetActiveScene().name == "LobbyScene";
            bool isMatching = (LobbyUIManager.Instance != null && LobbyUIManager.Instance.IsMatchmaking) ||
                (_matchmaking != null && (_matchmaking.IsReady || _matchmaking.IsMatching));
            return (destination != HeaderDestination.Mode || !GameBuildSettings.IsSoloOnly) &&
                !_isNavigating && !PranamProfileNavigation.IsLoading && _canvas.enabled &&
                IsSupportedScene(SceneManager.GetActiveScene().name) &&
                (SettingsUIManager.Instance == null || !SettingsUIManager.Instance.IsOpen) &&
                (!isMatching || isLobbyPlay) &&
                (!isLobbyPlay || (GameFlowManager.Instance != null && _matchmaking != null &&
                    (PlayModeSelection.IsSoloPlay || _matchmaking.CanToggleMatching))) &&
                (destination != HeaderDestination.Training || GameFlowManager.Instance != null);
        }

        /// <summary>
        /// 画面遷移または設定表示を開始します。
        /// </summary>
        public void Navigate(HeaderDestination destination)
        {
            if (!CanNavigate(destination))
            {
                return;
            }

            if (destination == HeaderDestination.Settings)
            {
                OpenSettings();
                return;
            }

            if (destination == HeaderDestination.Play && SceneManager.GetActiveScene().name == "LobbyScene")
            {
                StartSelectedGame();
                return;
            }

            StartCoroutine(ChangeScreen(destination));
        }

        /// <summary>
        /// ソロせん滅はステージ選択へ進み、マルチは既存のマッチング操作を実行します。
        /// </summary>
        private void StartSelectedGame()
        {
            if (PlayModeSelection.IsSoloPlay)
            {
                _isNavigating = true;
                try
                {
                    GameFlowManager.Instance.ClearSoloStageSelection();
                    GameState nextState = GameFlowManager.Instance.GameModeId == GameModeRegistry.k_DefaultModeId
                        ? new SoloStageSelectState()
                        : new Solo_CharacterSelectState();
                    GameFlowManager.Instance.ChangeState(nextState);
                }
                catch (Exception exception)
                {
                    _isNavigating = false;
                    Debug.LogException(exception);
                }

                return;
            }

            if (!_matchmaking.IsMatching && !_matchmaking.IsReady && !PlayModeSelection.CanPlayOnline)
            {
                LobbyConnectionWarning.Show(transform);
                PlayModeSelection.TryReconnect();
                return;
            }

            _matchmaking.ToggleMatching();
        }

        /// <summary>
        /// 設定シーンの読み込みを待って既存メニューを開きます。
        /// </summary>
        private async void OpenSettings()
        {
            _isNavigating = true;
            try
            {
                await SettingsUiBootstrap.EnsureLoadedAsync();
                if (this != null && IsSupportedScene(SceneManager.GetActiveScene().name))
                {
                    SettingsUIManager.Instance.Open();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                _isNavigating = false;
            }
        }

        /// <summary>
        /// 上に重ねた兵装画面から順に閉じ、既存ロビーとユニット選択を再利用します。
        /// </summary>
        private IEnumerator ChangeScreen(HeaderDestination destination)
        {
            _isNavigating = true;
            try
            {
                string target = destination == HeaderDestination.Unit ? "CharacterProfile" :
                    destination == HeaderDestination.Pranam ? "PranamProfile" :
                    destination == HeaderDestination.Mode ? "GamePlayModeUI" : "LobbyScene";
                if (SceneManager.GetActiveScene().name == target && destination != HeaderDestination.Training)
                {
                    yield break;
                }

                if (target != "GamePlayModeUI")
                {
                    GamePlayModeView mode = FindFirstObjectByType<GamePlayModeView>();
                    if (mode != null)
                    {
                        Scene previous = SceneManager.GetSceneByName(mode.ReturnSceneName);
                        if (previous.IsValid() && previous.isLoaded)
                        {
                            SceneManager.SetActiveScene(previous);
                        }
                    }

                    yield return UnloadIfLoaded("GamePlayModeUI");
                }

                if (target != "PranamProfile" && target != "GamePlayModeUI")
                {
                    yield return UnloadIfLoaded("PranamProfile");
                }

                if (target == "LobbyScene")
                {
                    yield return UnloadIfLoaded("CharacterProfile");
                }

                Scene scene = SceneManager.GetSceneByName(target);
                if (!scene.isLoaded)
                {
                    if (target == "PranamProfile")
                    {
                        PranamProfileNavigation.PrepareRequest(null, SceneManager.GetActiveScene().name);
                    }

                    yield return SceneManager.LoadSceneAsync(target, LoadSceneMode.Additive);
                    scene = SceneManager.GetSceneByName(target);
                }

                if (scene.IsValid() && scene.isLoaded)
                {
                    SceneManager.SetActiveScene(scene);
                }

                if (destination == HeaderDestination.Training && GameFlowManager.Instance != null)
                {
                    GameFlowManager.Instance.ChangeState(new SettingsState());
                }
            }
            finally
            {
                _isNavigating = false;
            }
        }

        /// <summary>
        /// シーン破棄による入力復元が完了するまで待機します。
        /// </summary>
        private static IEnumerator UnloadIfLoaded(string name)
        {
            Scene scene = SceneManager.GetSceneByName(name);
            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
