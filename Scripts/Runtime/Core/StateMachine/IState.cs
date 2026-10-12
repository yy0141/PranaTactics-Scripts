namespace PranaTactics.Core
{
    public interface IState
    {
        /// <summary>
        /// この状態へ遷移したときの初期化処理を実行します。
        /// </summary>
        void Enter();

        /// <summary>
        /// この状態から別の状態へ遷移するときの終了処理を実行します。
        /// </summary>
        void Exit();
    }
}
