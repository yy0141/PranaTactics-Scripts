using UnityEngine.EventSystems;

namespace PranaTactics.UI.Interaction
{
    public interface IMouseOver : IEventSystemHandler
    {
        /// <summary>
        /// UIオブジェクトがマウスオーバー状態へ
        /// 遷移したときに呼び出されます。
        /// </summary>
        void Enter();

        /// <summary>
        /// UIオブジェクトがマウスオーバー状態である間、
        /// 状態機械の更新時に呼び出されます。
        /// </summary>
        void Update();

        /// <summary>
        /// UIオブジェクトのマウスオーバー状態が
        /// 終了したときに呼び出されます。
        /// </summary>
        void Exit();
    }
}
