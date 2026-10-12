using UnityEngine;

using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens.SoloStageSelect
{
    public enum StageSelectAction
    {
        Select,
        Begin,
        Home,
        PreviousPage,
        NextPage
    }

    /// <summary>
    /// ステージ選択操作を既存のクリック状態機械へ接続します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoloStageSelectClick : RepeatClickBehaviorBase, IClickableCondition
    {
        [SerializeField]
        private SoloStageSelectView _view;

        [SerializeField]
        private StageSelectAction _action;

        [SerializeField]
        private int _rowIndex;

        /// <summary>
        /// ステージの解放状態やページ範囲に応じて操作を制限します。
        /// </summary>
        public bool CanClick()
        {
            return _view != null && _view.CanExecute(_action, _rowIndex);
        }

        /// <summary>
        /// 操作可能な場合だけ画面へクリックを通知します。
        /// </summary>
        protected override void OnClickEnter()
        {
            if (CanClick())
            {
                _view.Execute(_action, _rowIndex);
            }
        }
    }
}
