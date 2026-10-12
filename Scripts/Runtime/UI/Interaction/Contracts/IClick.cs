using UnityEngine.EventSystems;

namespace PranaTactics.UI.Interaction
{
    public interface IClick : IEventSystemHandler
    {
        /// <summary>
        /// UIオブジェクトがクリック状態になったときに呼び出されます。
        /// </summary>
        void Enter();

        /// <summary>
        /// UIオブジェクトがクリック状態である間、
        /// 状態機械の更新時に呼び出されます。
        /// </summary>
        void Update();

        /// <summary>
        /// UIオブジェクトのクリック状態が
        /// 解除されたときに呼び出されます。
        /// </summary>
        void Exit();
    }
}
