using UnityEngine;

using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 既存UI状態機械のクリックをリザルト操作へ接続します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResultNavigationClick : RepeatClickBehaviorBase, IClickableCondition
    {
        /// <summary>
        /// 共通リザルトの操作先です。
        /// </summary>
        [SerializeField]
        private ResultUIManager _view;
        /// <summary>
        /// 集計、戦績、前行、次行、復帰の順の操作です。
        /// </summary>
        [SerializeField]
        private ResultAction _action;

        /// <summary>
        /// 復帰中の操作を拒否します。
        /// </summary>
        public bool CanClick()
        {
            return _view != null && _view.CanInteract;
        }

        /// <summary>
        /// 設定された操作を実行します。
        /// </summary>
        protected override void OnClickEnter()
        {
            if (!CanClick())
            {
                return;
            }
            switch (_action)
            {
                case ResultAction.Summary:
                    _view.SelectPage(0);
                    break;
                case ResultAction.UnitRanking:
                    _view.SelectPage(1);
                    break;
                case ResultAction.PreviousRows:
                    _view.StepRows(-1);
                    break;
                case ResultAction.NextRows:
                    _view.StepRows(1);
                    break;
                case ResultAction.Return:
                    _view.Return();
                    break;
            }
        }

        private enum ResultAction
        {
            Summary,
            UnitRanking,
            PreviousRows,
            NextRows,
            Return
        }
    }
}
