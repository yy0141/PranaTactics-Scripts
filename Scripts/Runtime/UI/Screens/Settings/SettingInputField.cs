using TMPro;
using UnityEngine;

using PranaTactics.Settings;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 入力フィールドを通して、指定されたユーザー設定値を
    /// 表示および保存します。
    /// </summary>
    /// <remarks>
    /// 入力中の値は一時的に保持し、
    /// 入力が確定した時点で<see cref="SettingsManager"/>へ保存します。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SettingInputField : MonoBehaviour
    {
        /// <summary>
        /// 設定値の入力に使用する入力フィールドです。
        /// </summary>
        [SerializeField]
        private TMP_InputField _inputField;

        /// <summary>
        /// この入力フィールドで変更する設定項目です。
        /// </summary>
        [SerializeField]
        private SettingType _settingType;

        /// <summary>
        /// 現在入力されている設定値です。
        /// </summary>
        private string _currentValue =
            string.Empty;

        /// <summary>
        /// 入力フィールドのイベントを購読しているかどうかを示します。
        /// </summary>
        private bool _isSubscribed;

        /// <summary>
        /// 必要なコンポーネントを取得し、
        /// 入力フィールドの動作を設定します。
        /// </summary>
        private void Awake()
        {
            CacheInputField();

            if (_inputField == null)
            {
                return;
            }

            _inputField.restoreOriginalTextOnEscape =
                false;
        }

        /// <summary>
        /// 現在保存されている設定値を表示し、
        /// 入力フィールドのイベントを購読します。
        /// </summary>
        private void OnEnable()
        {
            if (!CacheInputField())
            {
                return;
            }

            RefreshDisplayValue();
            SubscribeEvents();
        }

        /// <summary>
        /// 入力フィールドのイベント購読を解除します。
        /// </summary>
        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        /// <summary>
        /// 入力フィールドの値が変更されたとき、
        /// 未保存の入力値を更新します。
        /// </summary>
        /// <param name="value">
        /// 入力フィールドに入力された値です。
        /// </param>
        public void OnValueChanged(
            string value)
        {
            _currentValue =
                value ??
                string.Empty;
        }

        /// <summary>
        /// 現在入力されている値を設定として保存します。
        /// </summary>
        /// <remarks>
        /// 保存処理によって値が補正された場合に備え、
        /// 保存後に表示値を再取得します。
        /// </remarks>
        public void SaveSetting()
        {
            if (_inputField == null)
            {
                return;
            }

            SettingsManager.SetValue(
                _settingType,
                _currentValue);

            RefreshDisplayValue();
        }

        /// <summary>
        /// 入力確定通知を受け取り、
        /// 現在の設定値を保存します。
        /// </summary>
        /// <param name="value">
        /// 入力確定時の文字列です。
        /// </param>
        private void HandleEndEdit(
            string value)
        {
            _currentValue =
                value ??
                string.Empty;

            SaveSetting();
        }

        /// <summary>
        /// 保存されている設定値を取得し、
        /// 入力フィールドへ反映します。
        /// </summary>
        private void RefreshDisplayValue()
        {
            if (_inputField == null)
            {
                return;
            }

            string displayValue =
                SettingsManager.GetDisplayValue(
                    _settingType) ??
                string.Empty;

            _currentValue =
                displayValue;

            _inputField.SetTextWithoutNotify(
                displayValue);
        }

        /// <summary>
        /// 入力フィールドのイベントを購読します。
        /// </summary>
        private void SubscribeEvents()
        {
            if (_inputField == null ||
                _isSubscribed)
            {
                return;
            }

            _inputField.onValueChanged.AddListener(
                OnValueChanged);

            _inputField.onEndEdit.AddListener(
                HandleEndEdit);

            _isSubscribed =
                true;
        }

        /// <summary>
        /// このコンポーネントが登録したイベントだけを解除します。
        /// </summary>
        private void UnsubscribeEvents()
        {
            if (_inputField == null ||
                !_isSubscribed)
            {
                return;
            }

            _inputField.onValueChanged.RemoveListener(
                OnValueChanged);

            _inputField.onEndEdit.RemoveListener(
                HandleEndEdit);

            _isSubscribed =
                false;
        }

        /// <summary>
        /// 設定値の入力に使用するTMP_InputFieldを取得します。
        /// </summary>
        /// <returns>
        /// 入力フィールドを取得できた場合は<c>true</c>です。
        /// </returns>
        private bool CacheInputField()
        {
            if (_inputField == null)
            {
                _inputField =
                    GetComponent<TMP_InputField>();
            }

            if (_inputField != null)
            {
                return true;
            }

            Debug.LogError(
                $"{nameof(SettingInputField)}: " +
                $"{nameof(TMP_InputField)}が設定されていません。",
                this);

            return false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上で入力フィールド参照を自動設定します。
        /// </summary>
        private void OnValidate()
        {
            if (_inputField == null)
            {
                _inputField =
                    GetComponent<TMP_InputField>();
            }
        }
#endif
    }
}
