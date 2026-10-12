using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PranaTactics.GameFlow.States
{
    /// <summary>
    /// ローカルシーンからタイトルを再読み込みして状態を復元します。
    /// </summary>
    public sealed class ReturnToTitleState : GameState
    {
        private bool _cancelled;

        public override void Enter()
        {
            _ = LoadAsync();
        }

        public override void Exit()
        {
            _cancelled = true;
        }

        private async Awaitable LoadAsync()
        {
            try
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync("TitleScene");

                while (operation != null && !operation.isDone)
                {
                    await Awaitable.NextFrameAsync();
                }

                if (!_cancelled && GameFlowManager.Instance != null)
                {
                    GameFlowManager.Instance.ChangeState(new TitleState());
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
