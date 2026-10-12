using UnityEngine.EventSystems;

namespace PranaTactics.UI.Interaction
{
    public interface INone : IEventSystemHandler
    {
        /// <summary>
        /// UIオブジェクトが無操作状態へ遷移したときに呼び出されます。
        /// </summary>
        void Enter();

        /// <summary>
        /// UIオブジェクトが無操作状態である間、
        /// 状態機械の更新時に呼び出されます。
        /// </summary>
        void Update();

        /// <summary>
        /// UIオブジェクトの無操作状態が終了したときに呼び出されます。
        /// </summary>
        void Exit();
    }
}
