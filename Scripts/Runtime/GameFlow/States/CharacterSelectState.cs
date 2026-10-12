using UnityEngine;
using UnityEngine.SceneManagement;

using PranaTactics.Networking;

namespace PranaTactics.GameFlow.States
{
    /// <summary>
    /// キャラクター選択画面の読み込みと、
    /// 全プレイヤーの準備完了後にゲームプレイ状態へ遷移する処理を管理します。
    /// </summary>
    public sealed class CharacterSelectState : GameState
    {
        /// <summary>
        /// キャラクター選択画面として読み込むシーンの名前です。
        /// </summary>
        private const string k_CharacterSelectSceneName = "CharacterSelectScene";

        /// <summary>
        /// シーンの読み込みを検出してから、
        /// 準備完了状態の確認を開始するまでの待機時間です。
        /// </summary>
        private const float k_StartCheckDelaySeconds = 0.5f;

        /// <summary>
        /// 準備完了待機中のデバッグログを出力する間隔です。
        /// </summary>
        private const float k_ReadyWaitLogIntervalSeconds = 1.0f;

        /// <summary>
        /// シーンの読み込み時刻が未設定であることを示す値です。
        /// </summary>
        private const float k_UninitializedSceneLoadTime = -1.0f;

        /// <summary>
        /// キャラクター選択シーンの読み込みを検出した時刻です。
        /// </summary>
        private float _sceneLoadedTime = k_UninitializedSceneLoadTime;

        /// <summary>
        /// 次に準備完了待機ログを出力できる時刻です。
        /// </summary>
        private float _nextReadyWaitLogTime;

        /// <summary>
        /// ゲームプレイ状態への遷移処理を実行済みかどうかを示します。
        /// </summary>
        private bool _hasTransitionedToGameplay;

        /// <summary>
        /// キャラクター選択状態を初期化し、
        /// プレイヤーの準備状態をリセットしてシーンを読み込みます。
        /// </summary>
        public override void Enter()
        {
            _sceneLoadedTime = k_UninitializedSceneLoadTime;
            _nextReadyWaitLogTime = 0.0f;
            _hasTransitionedToGameplay = false;
            OnlinePlayerPropertyResetter.ResetForCharacterSelect();
            NetworkSceneLoader.LoadScene(k_CharacterSelectSceneName);
        }

        /// <summary>
        /// キャラクター選択シーンの読み込みと、
        /// 全プレイヤーの準備完了状態を監視します。
        /// </summary>
        public override void UpdateState()
        {
            if (!IsCharacterSelectSceneActive())
            {
                return;
            }

            if (!HasStartedReadyCheck())
            {
                _sceneLoadedTime = Time.time;

                return;
            }

            if (!HasReadyCheckDelayElapsed())
            {
                return;
            }

            if (_hasTransitionedToGameplay)
            {
                return;
            }

            TryTransitionToGameplay();
        }

        /// <summary>
        /// キャラクター選択状態を終了し、
        /// ゲーム時間終了状態を初期値へ戻します。
        /// </summary>
        public override void Exit()
        {
            if (GameFlowManager.Instance == null)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterSelectState)}: " + $"{nameof(GameFlowManager)}が存在しないため、" +
                    "時間終了状態をリセットできません。");

                return;
            }

            GameFlowManager.Instance.SetTimeEnded(false);
        }

        /// <summary>
        /// 現在アクティブなシーンがキャラクター選択シーンかどうかを確認します。
        /// </summary>
        /// <returns>
        /// キャラクター選択シーンがアクティブな場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private static bool IsCharacterSelectSceneActive()
        {
            return SceneManager.GetActiveScene().name == k_CharacterSelectSceneName;
        }

        /// <summary>
        /// キャラクター選択シーンの読み込み時刻が
        /// 記録されているかどうかを確認します。
        /// </summary>
        /// <returns>
        /// 読み込み時刻が記録されている場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private bool HasStartedReadyCheck()
        {
            return _sceneLoadedTime >= 0.0f;
        }

        /// <summary>
        /// シーン読み込み後の準備完了確認待機時間が
        /// 経過したかどうかを確認します。
        /// </summary>
        /// <returns>
        /// 待機時間が経過している場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private bool HasReadyCheckDelayElapsed()
        {
            return Time.time - _sceneLoadedTime >= k_StartCheckDelaySeconds;
        }

        /// <summary>
        /// 全プレイヤーの準備が完了しているか、
        /// 制限時間が終了している場合にゲームプレイ状態へ遷移します。
        /// </summary>
        private void TryTransitionToGameplay()
        {
            if (GameFlowManager.Instance == null)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterSelectState)}: " + $"{nameof(GameFlowManager)}が存在しないため、" +
                    $"{nameof(GameplayState)}へ遷移できません。");

                return;
            }

            bool shouldLogReadyWait = ShouldLogReadyWait();
            bool areAllPlayersReady = OnlinePlayerUtility.AllPlayersReadyForCharacterSelect(
                shouldLogReadyWait);
            bool isTimeEnded = GameFlowManager.Instance.IsTimeEnded;

            if (!areAllPlayersReady && !isTimeEnded)
            {
                return;
            }

            _hasTransitionedToGameplay = true;
            Debug.Log(
                $"{nameof(CharacterSelectState)}: " + "全プレイヤーの準備が完了したか、" + "制限時間が終了したため、" +
                $"{nameof(GameplayState)}へ遷移します。");
            GameFlowManager.Instance.ChangeState(new GameplayState());
        }

        /// <summary>
        /// 準備完了待機中のデバッグログを
        /// 出力するタイミングかどうかを判定します。
        /// </summary>
        /// <returns>
        /// デバッグログを出力する場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private bool ShouldLogReadyWait()
        {
            if (Time.time < _nextReadyWaitLogTime)
            {
                return false;
            }

            _nextReadyWaitLogTime = Time.time + k_ReadyWaitLogIntervalSeconds;

            return true;
        }
    }
}
