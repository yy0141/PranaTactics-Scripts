using UnityEngine;

using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens.PranamProfile
{
    /// <summary>
    /// 既存UI状態機械から兵装選択とロール絞り込みを呼び出します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PranamProfileClick : RepeatClickBehaviorBase, IClickableCondition
    {
        [SerializeField]
        private PranamProfileView _view;

        [SerializeField]
        private ProfileAction _action;

        [SerializeField]
        private int _rowIndex;

        [SerializeField]
        private PranamRole _role;

        /// <summary>
        /// 複製された一覧行のクリック先を設定します。
        /// </summary>
        public void ConfigureRow(PranamProfileView view, int rowIndex)
        {
            _view = view;
            _rowIndex = rowIndex;
            _action = ProfileAction.Select;
        }

        /// <summary>
        /// 対象画面とリストの範囲に基づき、操作可能か判定します。
        /// </summary>
        public bool CanClick()
        {
            return _view != null && _view.CanInteract &&
                (_action != ProfileAction.Select || _view.CanSelectRow(_rowIndex));
        }

        /// <summary>
        /// Inspectorで指定された操作を実行します。
        /// </summary>
        protected override void OnClickEnter()
        {
            if (!CanClick())
            {
                return;
            }

            switch (_action)
            {
                case ProfileAction.Select:
                    _view.SelectRow(_rowIndex);
                    break;
                case ProfileAction.Filter:
                    _view.SelectRole(_role);
                    break;
            }
        }

        private enum ProfileAction
        {
            Select = 0,
            Filter = 4
        }
    }
}
