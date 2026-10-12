using System;

namespace PranaTactics.UI.Navigation
{
    public interface IBeforeSceneTransitionProcess
    {
        /// <summary>
        /// シーン遷移前の処理を実行します。
        /// </summary>
        /// <param name="onCompleted">
        /// 処理が完了したときに呼び出すコールバックです。
        /// </param>
        void Execute(
            Action onCompleted);
    }
}
