using UnityEngine;
using UnityEngine.SceneManagement;

using PranaTactics.Networking;

namespace PranaTactics.GameFlow.States
{
    /// <summary>
    /// ゲームプレイ画面への遷移と、
    /// ゲームプレイ中の状態を管理します。
    /// </summary>
    public sealed class GameplayState : GameState
    {
        /// <summary>
        /// ゲームプレイ画面として読み込むシーンの名前です。
        /// </summary>
        private const string k_GameplaySceneName = "InGameScene";

        /// <summary>
        /// ゲームプレイ状態へ遷移したとき、
        /// 選択されたプレイモードに応じて
        /// ゲームプレイシーンを読み込みます。
        /// </summary>
        public override void Enter()
        {
            GameFlowManager gameFlowManager = GameFlowManager.Instance;
            bool isSoloPlay = gameFlowManager != null && gameFlowManager.IsSoloPlay;

            Debug.Log($"{nameof(GameplayState)}: " + $"IsSoloPlay={isSoloPlay}");

            if (isSoloPlay)
            {
                gameFlowManager.ReloadSoloBattleConfiguration();
                SceneManager.LoadScene(k_GameplaySceneName, LoadSceneMode.Single);

                return;
            }

            NetworkSceneLoader.LoadScene(k_GameplaySceneName);
        }

        /// <summary>
        /// ゲームプレイ状態を終了します。
        /// </summary>
        public override void Exit()
        {
        }
    }
}
