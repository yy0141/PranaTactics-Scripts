namespace PranaTactics.UI.Interaction
{
    /// <summary>
    /// UIへ特別な状態を適用しない待機状態です。
    /// </summary>
    /// <remarks>
    /// <see cref="UIState"/>の既定動作をそのまま使用し、
    /// 状態の開始、更新および終了時に処理を行いません。
    /// </remarks>
    public sealed class NoneState : UIState
    {
        /// <summary>
        /// 状態開始時に呼び出されます。
        /// </summary>
        public override void Enter()
        {
        }

        /// <summary>
        /// 状態終了時に呼び出されます。
        /// </summary>
        public override void Exit()
        {
        }
    }
}
