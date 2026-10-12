namespace PranaTactics.Core
{
    public interface IStateMachine<TState>
where TState : IState
    {
        TState CurrentState
        {
            get;
        }

        /// <summary>
        /// 現在の状態を終了し、指定された新しい状態へ遷移します。
        /// </summary>
        /// <param name="newState">
        /// 遷移先となる新しい状態です。
        /// </param>
        void ChangeState(TState newState);

        /// <summary>
        /// 現在の状態に必要な更新処理を実行します。
        /// </summary>
        void UpdateState();
    }
}
