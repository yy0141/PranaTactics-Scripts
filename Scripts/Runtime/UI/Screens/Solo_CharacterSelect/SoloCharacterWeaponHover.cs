using UnityEngine;

using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 既存UI入力のマウスオーバー状態を兵装詳細の表示へ接続します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoloCharacterWeaponHover : MouseOverBehaviorBase
    {
        /// <summary>
        /// 兵装詳細を表示する選択画面です。
        /// </summary>
        [SerializeField]
        private SoloCharacterSelectionView _view;

        /// <summary>
        /// Mainの0～3またはSubの4～7を表す装備スロットです。
        /// </summary>
        [SerializeField]
        private int _slot;

        /// <summary>
        /// カーソルが兵装行へ入ったときに詳細を表示します。
        /// </summary>
        protected override void OnMouseOverEnter()
        {
            _view?.ShowWeaponDetails(_slot);
        }

        /// <summary>
        /// キャラクター変更や操作可否の変化をマウスオーバー中にも反映します。
        /// </summary>
        protected override void OnMouseOverUpdate()
        {
            _view?.ShowWeaponDetails(_slot);
        }

        /// <summary>
        /// カーソルが離れたときや行が非表示になったときに詳細を閉じます。
        /// </summary>
        protected override void OnMouseOverExit()
        {
            _view?.HideWeaponDetails(_slot);
        }
    }
}
