using UnityEngine;

using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens.CharacterProfile
{
    /// <summary>
    /// 既存UI状態機械のクリックをプロフィールの操作へ接続します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterProfileClick : RepeatClickBehaviorBase, IClickableCondition
    {
        [SerializeField]
        private CharacterProfileView _view;

        [SerializeField]
        private ProfileAction _action;

        [Min(0)]
        [SerializeField]
        private int _thumbnailIndex;

        /// <summary>
        /// 閉じる処理中の操作を拒否します。
        /// </summary>
        public bool CanClick()
        {
            return _view != null && _view.CanInteract;
        }

        /// <summary>
        /// Inspectorに設定されたプロフィール操作を実行します。
        /// </summary>
        protected override void OnClickEnter()
        {
            if (!CanClick())
            {
                return;
            }

            switch (_action)
            {
                case ProfileAction.SelectThumbnail:
                    _view.SelectThumbnail(_thumbnailIndex);
                    break;
                case ProfileAction.Previous:
                    _view.Previous();
                    break;
                case ProfileAction.Next:
                    _view.Next();
                    break;
            }
        }

        private enum ProfileAction
        {
            SelectThumbnail,
            Previous,
            Next
        }
    }
}
