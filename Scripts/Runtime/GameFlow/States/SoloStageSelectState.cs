using UnityEngine;
using UnityEngine.SceneManagement;

namespace PranaTactics.GameFlow.States
{
    /// <summary>
    /// ソロせん滅の専用ステージ選択シーンへ遷移します。
    /// </summary>
    public sealed class SoloStageSelectState : GameState
    {
        /// <summary>
        /// 前回の試合選択を解除し、カーソルとステージ選択を準備します。
        /// </summary>
        public override void Enter()
        {
            GameFlowManager.Instance.ResetAfterGame();
            GameFlowManager.Instance.ClearSoloStageSelection();
            SceneManager.LoadScene("SoloStageSelectScene", LoadSceneMode.Single);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// 選択画面の終了処理を行います。
        /// </summary>
        public override void Exit()
        {
        }
    }
}
