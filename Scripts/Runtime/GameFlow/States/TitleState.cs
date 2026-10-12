using System;
using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Core;
using PranaTactics.Networking;
using PranaTactics.Settings;

namespace PranaTactics.GameFlow.States
{
    /// <summary>
    /// タイトルの決定操作で初回訓練またはロビーへ遷移し、オンライン接続を並行して開始します。
    /// </summary>
    public sealed class TitleState : GameState
    {
        /// <summary>
        /// タイトル画面の決定操作に使用するマウスボタン番号です。
        /// </summary>
        private const int k_ConfirmMouseButton = 0;

        /// <summary>
        /// タイトルからの重複遷移を防ぎます。
        /// </summary>
        private bool _hasTransitioned;

        /// <summary>
        /// データの準備中に受け付けた決定操作を保持します。
        /// </summary>
        private bool _isTransitionRequested;

        /// <summary>
        /// 決定操作を受け付け、通常ビルドではオンライン接続を要求します。
        /// </summary>
        /// <remarks>
        /// ロビーの閲覧に接続完了は不要です。オンライン操作の可否はロビー側で確認します。
        /// </remarks>
        public override void Enter()
        {
            _hasTransitioned = false;
            _isTransitionRequested = false;
            if (GameBuildSettings.IsSoloOnly ||
                Application.internetReachability == NetworkReachability.NotReachable)
            {
                return;
            }

            try
            {
                NetworkServiceLocator.Online?.Connect();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// タイトルを離れた後の決定操作を無効にします。
        /// </summary>
        public override void Exit()
        {
            _hasTransitioned = true;
            _isTransitionRequested = false;
        }

        /// <summary>
        /// 左クリックを保持し、データの準備後に初回訓練またはロビーへ遷移します。
        /// </summary>
        /// <remarks>
        /// 初回訓練のEnterはUIデータを参照するため、状態を変更する前に読み込み完了を確認します。
        /// 準備中にボタンを離しても、再クリックせずに進めます。
        /// </remarks>
        public override void UpdateState()
        {
            if (_hasTransitioned)
            {
                return;
            }

            _isTransitionRequested |= Input.GetMouseButtonDown(k_ConfirmMouseButton);
            if (!_isTransitionRequested || !CanTransition())
            {
                return;
            }

            if (GameFlowManager.Instance == null)
            {
                _isTransitionRequested = false;
                Debug.LogError($"{nameof(TitleState)}: GameFlowManagerが存在しないためロビーへ遷移できません。");
                return;
            }

            _hasTransitioned = true;
            _isTransitionRequested = false;
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySE(SESoundData.SE.Click);
            }

            GameState nextState = SettingsManager.Current.HasFinishedTutorial
                ? new LobbyState()
                : new TutorialState();
            GameFlowManager.Instance.ChangeState(nextState);
        }

        /// <summary>
        /// 遷移先が利用するデータの読み込み状態を確認します。
        /// </summary>
        private bool CanTransition()
        {
            GameDataRepository repository = GameDataRepository.Instance;
            if (repository == null)
            {
                _isTransitionRequested = false;
                Debug.LogError($"{nameof(TitleState)}: ゲームデータ管理が存在しないため遷移できません。");
                return false;
            }

            if (repository.InitializationError != null)
            {
                _isTransitionRequested = false;
                Debug.LogError($"{nameof(TitleState)}: ゲームデータの読み込みに失敗したため遷移できません。");
                return false;
            }

            return repository.IsLoaded;
        }
    }
}
