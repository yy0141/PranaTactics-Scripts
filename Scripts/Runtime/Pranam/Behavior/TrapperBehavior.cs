using UnityEngine;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// ミクロ中の使用入力から現在地への罠設置を要求します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrapperBehavior : TrapperBase
    {
        /// <summary>
        /// ボードへ罠の設置を要求します。
        /// </summary>
        public override void UsePranam()
        {
            RequestTrapPlacement();
        }
    }
}
