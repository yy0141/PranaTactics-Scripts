using System;

using PranaTactics.Core;

namespace PranaTactics.Tutorial
{
    /// <summary>
    /// 既存のIStateMachine契約に従う、訓練内のステートマシンです。
    /// </summary>
    public sealed class TutorialStateMachine : IStateMachine<TutorialLessonState>
    {
        public TutorialLessonState CurrentState
        {
            get; private set;
        }

        /// <summary>
        /// 現在の課題を終了して次の課題を開始します。同じインスタンスへの遷移は行いません。
        /// </summary>
        public void ChangeState(TutorialLessonState newState)
        {
            if (newState == null)
            {
                throw new ArgumentNullException(nameof(newState));
            }

            if (ReferenceEquals(CurrentState, newState))
            {
                return;
            }

            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }

        /// <summary>
        /// 実行中の課題があれば、その進捗を更新します。
        /// </summary>
        public void UpdateState()
        {
            CurrentState?.UpdateState();
        }

        /// <summary>
        /// 実行中の課題を終了し、現在の課題への参照を解放します。
        /// </summary>
        public void Stop()
        {
            CurrentState?.Exit();
            CurrentState = null;
        }
    }
}
