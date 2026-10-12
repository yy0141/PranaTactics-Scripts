using UnityEngine.EventSystems;

namespace PranaTactics.UI.Interaction
{
    public interface IIdle : IEventSystemHandler
    {
        /// <summary>
        /// UIオブジェクトが待機状態へ遷移したときに呼び出されます。
        /// </summary>
        void Enter();

        /// <summary>
        /// UIオブジェクトが待機状態である間、
        /// 状態機械の更新時に呼び出されます。
        /// </summary>
        void Update();

        /// <summary>
        /// UIオブジェクトの待機状態が終了したときに呼び出されます。
        /// </summary>
        void Exit();
    }
}
