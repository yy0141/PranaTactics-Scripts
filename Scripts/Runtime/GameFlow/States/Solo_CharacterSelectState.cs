using UnityEngine;
using UnityEngine.SceneManagement;

using PranaTactics.UI.Screens;

namespace PranaTactics.GameFlow.States
{
    /// <summary>
    /// ソロプレイ用キャラクター選択画面の読み込みと、
    /// 準備完了後のゲームプレイ状態への遷移を管理します。
    /// </summary>
    public sealed class Solo_CharacterSelectState : GameState
    {
        /// <summary>
        /// キャラクター選択画面として読み込むシーンの名前です。
        /// </summary>
        private const string k_CharacterSelectSceneName = "Solo_CharacterSelectScene";

        /// <summary>
        /// シーンの読み込みを検出してから、
        /// キャラクター選択状態の確認を開始するまでの待機時間です。
        /// </summary>
        private const float k_StartCheckDelaySeconds = 0.5f;

        /// <summary>
        /// キャラクター選択待機中のログを出力する間隔です。
        /// </summary>
        private const float k_WaitLogIntervalSeconds = 1.0f;

        /// <summary>
        /// シーンの読み込み時刻が未設定であることを示す値です。
        /// </summary>
        private const float k_UninitializedSceneLoadTime = -1.0f;

        /// <summary>
        /// キャラクター選択シーンの読み込みを検出した時刻です。
        /// </summary>
        private float _sceneLoadedTime = k_UninitializedSceneLoadTime;

        /// <summary>
        /// 次に待機ログを出力できる時刻です。
        /// </summary>
        private float _nextWaitLogTime;

        /// <summary>
        /// ゲームプレイ状態への遷移を
        /// すでに要求したかどうかです。
        /// </summary>
        private bool _isTransitionRequested;

        /// <summary>
        /// キャラクター選択状態を初期化し、
        /// ローカル環境でキャラクター選択シーンを読み込みます。
        /// </summary>
        public override void Enter()
        {
            SoloCharacterSelectionContext.Clear();
            _sceneLoadedTime = k_UninitializedSceneLoadTime;
            _nextWaitLogTime = 0.0f;
            _isTransitionRequested = false;
            SceneManager.LoadScene(k_CharacterSelectSceneName, LoadSceneMode.Single);
        }

        /// <summary>
        /// キャラクター選択シーンの読み込みと、
        /// ローカルプレイヤーの準備完了状態を監視します。
        /// </summary>
        public override void UpdateState()
        {
            if (_isTransitionRequested)
            {
                return;
            }

            if (!IsCharacterSelectSceneActive())
            {
                return;
            }

            if (!HasStartedSelectionCheck())
            {
                _sceneLoadedTime = Time.unscaledTime;

                return;
            }

            if (!HasSelectionCheckDelayElapsed())
            {
                return;
            }

            SoloCharacterSelectUIManager uiManager = SoloCharacterSelectUIManager.Instance;

            if (uiManager == null)
            {
                LogWaitMessage($"{nameof(SoloCharacterSelectUIManager)}の" + "初期化を待機しています。");

                return;
            }

            if (!uiManager.CanStartGame)
            {
                LogWaitMessage("キャラクターが選択されるまで待機しています。");

                return;
            }

            if (!uiManager.IsReadySelectionLocked)
            {
                LogWaitMessage("準備完了ボタンが押されるまで待機しています。");

                return;
            }

            if (!SoloCharacterSelectionContext.HasSelection)
            {
                LogWaitMessage("選択されたキャラクター情報が" + "保存されるまで待機しています。");

                return;
            }

            ChangeToGameplayState();
        }

        /// <summary>
        /// キャラクター選択状態を終了します。
        /// </summary>
        public override void Exit()
        {
        }

        /// <summary>
        /// ゲームプレイ状態へ遷移します。
        /// </summary>
        private void ChangeToGameplayState()
        {
            GameFlowManager gameFlowManager = GameFlowManager.Instance;

            if (gameFlowManager == null)
            {
                LogWaitMessage($"{nameof(GameFlowManager)}が存在しないため、" + "ゲームプレイ状態へ遷移できません。");

                return;
            }

            if (!SoloCharacterSelectionContext.HasSelection)
            {
                LogWaitMessage("キャラクターが選択されていないため、" + "ゲームプレイ状態へ遷移できません。");

                return;
            }

            Debug.Log(
                $"{nameof(Solo_CharacterSelectState)}: " + "ソロプレイを開始します。" + $" CharacterName=" +
                $"{SoloCharacterSelectionContext.CharacterName}");
            _isTransitionRequested = true;
            gameFlowManager.ChangeState(new GameplayState());
        }

        /// <summary>
        /// 現在アクティブなシーンが
        /// キャラクター選択シーンかどうかを確認します。
        /// </summary>
        private static bool IsCharacterSelectSceneActive()
        {
            return SceneManager.GetActiveScene().name == k_CharacterSelectSceneName;
        }

        /// <summary>
        /// キャラクター選択シーンの読み込み時刻が
        /// 記録されているかどうかを確認します。
        /// </summary>
        private bool HasStartedSelectionCheck()
        {
            return _sceneLoadedTime >= 0.0f;
        }

        /// <summary>
        /// シーン読み込み後の確認待機時間が
        /// 経過したかどうかを確認します。
        /// </summary>
        private bool HasSelectionCheckDelayElapsed()
        {
            return Time.unscaledTime - _sceneLoadedTime >= k_StartCheckDelaySeconds;
        }

        /// <summary>
        /// 指定された待機メッセージを
        /// 一定間隔でログへ出力します。
        /// </summary>
        private void LogWaitMessage(string message)
        {
            if (!ShouldLogWait())
            {
                return;
            }

            Debug.Log($"{nameof(Solo_CharacterSelectState)}: " + message);
        }

        /// <summary>
        /// 待機ログを出力するタイミングかどうかを判定します。
        /// </summary>
        private bool ShouldLogWait()
        {
            if (Time.unscaledTime < _nextWaitLogTime)
            {
                return false;
            }

            _nextWaitLogTime = Time.unscaledTime + k_WaitLogIntervalSeconds;

            return true;
        }
    }
}
