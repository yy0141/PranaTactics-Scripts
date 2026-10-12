using UnityEngine;

using PranaTactics.Match.Modes;
using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens.GamePlayMode
{
    /// <summary>
    /// モードカードのクリックを既存UI入力から受け取ります。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamePlayModeClick : RepeatClickBehaviorBase, IClickableCondition
    {
        /// <summary>
        /// モード選択と画面遷移を担当する表示です。
        /// </summary>
        [SerializeField]
        private GamePlayModeView _view;

        /// <summary>
        /// カードが属する接続区分です。
        /// </summary>
        private PlayCategory _category;

        /// <summary>
        /// カードが選択するゲームルールのIDです。
        /// </summary>
        private string _gameModeId;

        /// <summary>
        /// カタログから生成されたカードの選択情報を設定します。
        /// </summary>
        public void Initialize(GamePlayModeView view, PlayCategory category, string gameModeId)
        {
            _view = view;
            _category = category;
            _gameModeId = gameModeId;
        }

        /// <summary>
        /// 設定画面およびマッチング中の変更を拒否します。
        /// </summary>
        public bool CanClick()
        {
            return _view != null && _view.CanInteract;
        }

        /// <summary>
        /// 選択したモードを適用します。
        /// </summary>
        protected override void OnClickEnter()
        {
            if (CanClick())
            {
                _view.SelectMode(_category, _gameModeId);
            }
        }
    }
}
