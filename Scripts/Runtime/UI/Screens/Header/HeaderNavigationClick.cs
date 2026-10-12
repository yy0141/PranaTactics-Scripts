using UnityEngine;

using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens.Header
{
    /// <summary>
    /// 共通ヘッダーのクリックを既存UI入力から受け取ります。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeaderNavigationClick : RepeatClickBehaviorBase, IClickableCondition
    {
        [SerializeField]
        private HeaderNavigationView _view;

        [SerializeField]
        private HeaderDestination _destination;

        /// <summary>
        /// 遷移中と設定表示中の操作を拒否します。
        /// </summary>
        public bool CanClick()
        {
            return _view != null && _view.CanNavigate(_destination);
        }

        /// <summary>
        /// 選択された画面を開きます。
        /// </summary>
        protected override void OnClickEnter()
        {
            if (CanClick())
            {
                _view.Navigate(_destination);
            }
        }
    }

    public enum HeaderDestination
    {
        Training,
        Unit,
        Pranam,
        Play,
        Settings,
        Mode
    }
}
