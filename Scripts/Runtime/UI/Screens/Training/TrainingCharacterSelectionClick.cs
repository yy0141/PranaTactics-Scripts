using UnityEngine;

using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens.Training
{
    /// <summary>
    /// 既存UI入力から訓練用キャラクターの選択を受け取ります。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainingCharacterSelectionClick : RepeatClickBehaviorBase, IClickableCondition
    {
        private TrainingCharacterSelectionView _view;
        private string _characterName;

        /// <summary>
        /// 選択対象を設定します。
        /// </summary>
        public void Initialize(TrainingCharacterSelectionView view, string characterName)
        {
            _view = view;
            _characterName = characterName;
        }

        /// <summary>
        /// 選択画面が操作可能か確認します。
        /// </summary>
        public bool CanClick()
        {
            return _view != null && _view.CanSelect;
        }

        /// <summary>
        /// 押したカードのキャラクターへ変更します。
        /// </summary>
        protected override void OnClickEnter()
        {
            if (CanClick())
            {
                _view.SelectCharacter(_characterName);
            }
        }
    }
}
