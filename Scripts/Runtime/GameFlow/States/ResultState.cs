using UnityEngine;

using PranaTactics.Match;
using PranaTactics.Networking;

namespace PranaTactics.GameFlow.States
{
    /// <summary>
    /// リザルト画面への遷移と、
    /// リザルト画面でのカーソル状態を管理します。
    /// </summary>
    public sealed class ResultState : GameState
    {
        /// <summary>
        /// リザルト画面として読み込むシーンの名前です。
        /// </summary>
        private const string k_ResultSceneName = "ResultScene";

        /// <summary>
        /// リザルト状態へ遷移したとき、
        /// リザルトシーンを読み込み、カーソルを操作可能な状態にします。
        /// </summary>
        public override void Enter()
        {
            MatchManager.Instance?.SaveResultSnapshot();
            NetworkSceneLoader.LoadScene(k_ResultSceneName);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        /// <summary>
        /// リザルト状態から別の状態へ遷移するときの
        /// 終了処理を実行します。
        /// </summary>
        /// <remarks>
        /// 現在、リザルト状態の終了時に必要な処理はありません。
        /// </remarks>
        public override void Exit()
        {
        }
    }
}
