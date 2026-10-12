using UnityEngine;

using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// ソロ選択画面のタブ、一覧送りと戻る操作を既存UI入力へ接続します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoloCharacterSelectionClick : RepeatClickBehaviorBase, IClickableCondition
    {
        /// <summary>
        /// クリック操作を受け取る表示コンポーネントです。
        /// </summary>
        [SerializeField]
        private SoloCharacterSelectionView _view;

        /// <summary>
        /// タブ、前後への移動または戻る操作の識別番号です。
        /// </summary>
        [SerializeField]
        private int _action;

        /// <summary>
        /// 操作対象のタブの番号です。
        /// </summary>
        [SerializeField]
        private int _index;

        /// <summary>
        /// キャラクター選択が確定する前だけ操作を許可します。
        /// </summary>
        public bool CanClick()
        {
            return _view != null && _view.CanInteract;
        }

        /// <summary>
        /// シーンで指定された画面操作を実行します。
        /// </summary>
        protected override void OnClickEnter()
        {
            if (CanClick())
            {
                _view.Execute(_action, _index);
            }
        }
    }
}
