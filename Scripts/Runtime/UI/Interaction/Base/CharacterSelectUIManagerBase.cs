using System;
using UnityEngine;

using PranaTactics.Unit;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// キャラクター選択UIManagerに共通する
    /// 選択状態とアイコン表示を管理する基底クラスです。
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class CharacterSelectUIManagerBase :
        MonoBehaviour
    {
        public static CharacterSelectUIManagerBase ActiveInstance
        {
            get;
            private set;
        }

        protected bool IsPrimaryInstance
        {
            get;
            private set;
        }

        /// <summary>
        /// 現在選択表示になっているキャラクターアイコンです。
        /// </summary>
        private CharacterSelectIcon _currentIcon;

        /// <summary>
        /// キャラクターが選択済みかどうかです。
        /// </summary>
        private bool _isCharacterSelected;

        /// <summary>
        /// Ready確定によってキャラクター選択が
        /// ロックされているかどうかです。
        /// </summary>
        private bool _isReadySelectionLocked;

        public bool IsCharacterSelected
        {
            get
            {
                return _isCharacterSelected;
            }
        }

        public bool IsReadySelectionLocked
        {
            get
            {
                return _isReadySelectionLocked;
            }
        }

        public bool CanSelectCharacter
        {
            get
            {
                return !_isReadySelectionLocked;
            }
        }

        public virtual bool CanStartGame
        {
            get
            {
                return _isCharacterSelected;
            }
        }

        public event Action<bool> CharacterSelectionStateChanged;

        public event Action<bool> ReadyStateChanged;

        /// <summary>
        /// 現在有効なUIManagerとして登録します。
        /// </summary>
        protected virtual void Awake()
        {
            if (ActiveInstance != null &&
                ActiveInstance != this)
            {
                Debug.LogWarning(
                    $"{GetType().Name}が複数存在するため、" +
                    $"{gameObject.name}を破棄します。",
                    this);

                Destroy(
                    gameObject);

                return;
            }

            ActiveInstance =
                this;

            IsPrimaryInstance =
                true;
        }

        /// <summary>
        /// 有効なUIManagerへの参照を解除します。
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance =
                    null;
            }

            IsPrimaryInstance =
                false;
        }

        /// <summary>
        /// 指定されたアイコンに対応するキャラクターを選択します。
        /// </summary>
        /// <param name="icon">
        /// 選択されたキャラクターアイコンです。
        /// </param>
        /// <param name="characterData">
        /// 選択するキャラクターデータです。
        /// </param>
        /// <returns>
        /// 選択に成功した場合は<c>true</c>です。
        /// </returns>
        public bool TrySelectCharacter(
            CharacterSelectIcon icon,
            UnitParams characterData)
        {
            if (icon == null)
            {
                Debug.LogWarning(
                    $"{GetType().Name}: " +
                    "キャラクターアイコンがnullです。",
                    this);

                return false;
            }

            if (characterData == null)
            {
                Debug.LogWarning(
                    $"{GetType().Name}: " +
                    "キャラクターデータがnullです。",
                    this);

                return false;
            }

            if (!CanSelectCharacter)
            {
                Debug.LogWarning(
                    $"{GetType().Name}: " +
                    "Ready確定後のため、" +
                    "キャラクターを変更できません。",
                    this);

                return false;
            }

            if (!TryApplyCharacterSelection(
                    characterData))
            {
                return false;
            }

            SetCharacterSelected(
                true);

            Select(
                icon);

            return true;
        }

        /// <summary>
        /// 派生クラス固有の方法で
        /// キャラクター選択を反映します。
        /// </summary>
        /// <param name="characterData">
        /// 選択するキャラクターデータです。
        /// </param>
        /// <returns>
        /// 選択情報を反映できた場合は<c>true</c>です。
        /// </returns>
        protected abstract bool TryApplyCharacterSelection(
            UnitParams characterData);

        /// <summary>
        /// キャラクター選択済み状態を設定します。
        /// </summary>
        /// <param name="isSelected">
        /// 選択済みにする場合は<c>true</c>です。
        /// </param>
        public void SetCharacterSelected(
            bool isSelected)
        {
            if (_isCharacterSelected ==
                isSelected)
            {
                return;
            }

            _isCharacterSelected =
                isSelected;

            if (!_isCharacterSelected &&
                _isReadySelectionLocked)
            {
                SetReadySelectionLocked(
                    false);
            }

            CharacterSelectionStateChanged?.Invoke(
                _isCharacterSelected);
        }

        /// <summary>
        /// Ready確定による選択ロック状態を設定します。
        /// </summary>
        /// <param name="isLocked">
        /// 選択をロックする場合は<c>true</c>です。
        /// </param>
        public void SetReadySelectionLocked(
            bool isLocked)
        {
            if (isLocked &&
                !CanStartGame)
            {
                Debug.LogWarning(
                    $"{GetType().Name}: " +
                    "キャラクターが選択されていないため、" +
                    "Ready状態にできません。",
                    this);

                return;
            }

            if (_isReadySelectionLocked ==
                isLocked)
            {
                return;
            }

            _isReadySelectionLocked =
                isLocked;

            ReadyStateChanged?.Invoke(
                _isReadySelectionLocked);
        }

        /// <summary>
        /// 指定されたキャラクターアイコンを
        /// 選択表示へ変更します。
        /// </summary>
        /// <param name="icon">
        /// 選択するキャラクターアイコンです。
        /// </param>
        public void Select(
            CharacterSelectIcon icon)
        {
            if (icon == null)
            {
                return;
            }

            if (_currentIcon ==
                icon)
            {
                icon.ForceSelect();

                return;
            }

            _currentIcon?.ForceDeselect();

            _currentIcon =
                icon;

            _currentIcon.ForceSelect();
        }

        /// <summary>
        /// 現在のキャラクター選択を解除します。
        /// </summary>
        public void ClearSelection()
        {
            if (_isReadySelectionLocked)
            {
                Debug.LogWarning(
                    $"{GetType().Name}: " +
                    "Ready確定後のため、" +
                    "キャラクター選択を解除できません。",
                    this);

                return;
            }

            _currentIcon?.ForceDeselect();

            _currentIcon =
                null;

            OnSelectionCleared();

            SetCharacterSelected(
                false);
        }

        /// <summary>
        /// キャラクター選択解除時の
        /// 派生クラス固有処理を実行します。
        /// </summary>
        protected virtual void OnSelectionCleared()
        {
        }

        /// <summary>
        /// 現在のキャラクター選択を解除します。
        /// </summary>
        public void Clear()
        {
            ClearSelection();
        }

        /// <summary>
        /// キャラクター選択済み状態を設定します。
        /// </summary>
        [Obsolete(
            "Use SetCharacterSelected instead.")]
        public void SetIsSelectedCharacter(
            bool value)
        {
            SetCharacterSelected(
                value);
        }

        /// <summary>
        /// キャラクター選択済み状態を取得します。
        /// </summary>
        [Obsolete(
            "Use IsCharacterSelected instead.")]
        public bool GetIsSelectedCharacter()
        {
            return IsCharacterSelected;
        }

        /// <summary>
        /// Ready確定状態を設定します。
        /// </summary>
        [Obsolete(
            "Use SetReadySelectionLocked instead.")]
        public void SetIsClickedReadyUpButton(
            bool value)
        {
            SetReadySelectionLocked(
                value);
        }

        /// <summary>
        /// Ready確定状態を取得します。
        /// </summary>
        [Obsolete(
            "Use IsReadySelectionLocked instead.")]
        public bool GetIsClickedReadyUpButton()
        {
            return IsReadySelectionLocked;
        }
    }
}
