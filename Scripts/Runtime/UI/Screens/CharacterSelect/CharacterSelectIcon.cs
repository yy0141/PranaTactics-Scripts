using UnityEngine;

using PranaTactics.Core;
using PranaTactics.UI;
using PranaTactics.UI.Interaction;
using PranaTactics.Unit;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// キャラクター選択画面に表示する
    /// キャラクターアイコンのクリック処理を管理します。
    /// </summary>
    /// <remarks>
    /// 現在有効な
    /// <see cref="CharacterSelectUIManagerBase"/>へ
    /// キャラクター選択を通知します。
    ///
    /// オンライン用とソロ用の
    /// どちらのキャラクター選択画面でも使用できます。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterSelectIcon :
        ClickBehaviorBase,
        IClickableCondition
    {
        /// <summary>
        /// 選択状態に応じて背景色を変更するコンポーネントです。
        /// </summary>
        [Header("Appearance")]
        [SerializeField]
        private ChangeImageColor _backgroundColorChanger;

        /// <summary>
        /// このアイコンに対応するキャラクター名です。
        /// </summary>
        /// <remarks>
        /// 未設定の場合はゲームオブジェクト名を使用します。
        /// </remarks>
        [Header("Character")]
        [SerializeField]
        private string _characterName =
            string.Empty;

        /// <summary>
        /// このアイコンに対応するキャラクターデータです。
        /// </summary>
        private UnitParams _characterData;

        public UnitParams CharacterData
        {
            get
            {
                return _characterData;
            }
        }

        /// <summary>
        /// Ready確定前であればクリックを許可します。
        /// </summary>
        /// <returns>
        /// キャラクターを変更できる場合は<c>true</c>です。
        /// </returns>
        public bool CanClick()
        {
            CharacterSelectUIManagerBase uiManager =
                CharacterSelectUIManagerBase.ActiveInstance;

            return uiManager != null &&
                uiManager.CanSelectCharacter;
        }

        /// <summary>
        /// アイコンがクリックされたとき、
        /// 対応するキャラクターを選択します。
        /// </summary>
        protected override void OnClickEnter()
        {
            CharacterSelectUIManagerBase uiManager =
                CharacterSelectUIManagerBase.ActiveInstance;

            if (uiManager == null)
            {
                Debug.LogError(
                    $"{nameof(CharacterSelectIcon)}: " +
                    $"{nameof(CharacterSelectUIManagerBase)}が" +
                    "存在しないため、" +
                    "キャラクターを選択できません。",
                    this);

                return;
            }

            if (!uiManager.CanSelectCharacter)
            {
                return;
            }

            if (!TryGetCharacterData(
                    out UnitParams characterData))
            {
                return;
            }

            uiManager.TrySelectCharacter(
                this,
                characterData);
        }

        /// <summary>
        /// 外部処理から、このアイコンを選択表示へ変更します。
        /// </summary>
        public void ForceSelect()
        {
            SetSelectedAppearance(
                true);
        }

        /// <summary>
        /// 外部処理から、このアイコンを未選択表示へ変更します。
        /// </summary>
        public void ForceDeselect()
        {
            SetSelectedAppearance(
                false);
        }

        /// <summary>
        /// このアイコンの選択表示を変更します。
        /// </summary>
        /// <param name="isSelected">
        /// 選択状態にする場合は<c>true</c>です。
        /// </param>
        public void SetSelected(
            bool isSelected)
        {
            SetSelectedAppearance(
                isSelected);
        }

        /// <summary>
        /// 対応するキャラクターデータを取得します。
        /// </summary>
        private bool TryGetCharacterData(
            out UnitParams characterData)
        {
            if (_characterData != null)
            {
                characterData =
                    _characterData;

                return true;
            }

            GameDataRepository repository =
                GameDataRepository.Instance;

            if (repository == null)
            {
                Debug.LogError(
                    $"{nameof(CharacterSelectIcon)}: " +
                    $"{nameof(GameDataRepository)}が存在しないため、" +
                    "キャラクターデータを取得できません。",
                    this);

                characterData =
                    null;

                return false;
            }

            string characterName =
                GetCharacterName();

            if (string.IsNullOrEmpty(
                    characterName))
            {
                Debug.LogError(
                    $"{nameof(CharacterSelectIcon)}: " +
                    "キャラクター名が設定されていません。",
                    this);

                characterData =
                    null;

                return false;
            }

            _characterData =
                repository.GetCharacterData(
                    characterName);

            if (_characterData == null)
            {
                Debug.LogError(
                    $"{nameof(CharacterSelectIcon)}: " +
                    "指定されたキャラクターデータが見つかりません。" +
                    $" CharacterName={characterName}",
                    this);

                characterData =
                    null;

                return false;
            }

            characterData =
                _characterData;

            return true;
        }

        /// <summary>
        /// データ検索に使用するキャラクター名を取得します。
        /// </summary>
        private string GetCharacterName()
        {
            if (!string.IsNullOrWhiteSpace(
                    _characterName))
            {
                return _characterName.Trim();
            }

            return gameObject.name;
        }

        /// <summary>
        /// 背景色を選択状態に対応する色へ変更します。
        /// </summary>
        private void SetSelectedAppearance(
            bool isSelected)
        {
            if (_backgroundColorChanger == null)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterSelectIcon)}: " +
                    $"{nameof(ChangeImageColor)}が設定されていないため、" +
                    "選択表示を変更できません。",
                    this);

                return;
            }

            _backgroundColorChanger.SetSelected(
                isSelected);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定された参照とキャラクター名を検証します。
        /// </summary>
        private void OnValidate()
        {
            if (_backgroundColorChanger == null)
            {
                _backgroundColorChanger =
                    GetComponent<ChangeImageColor>();
            }

            _characterName =
                _characterName?.Trim() ??
                string.Empty;
        }
#endif
    }
}
