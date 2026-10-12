using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.UI.Interaction;

namespace PranaTactics.UI
{
    /// <summary>
    /// クリックされたときに、指定されたUIオブジェクトを閉じます。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CloseUI : ClickBehaviorBase
    {
        /// <summary>
        /// クリック時に非表示にするUIオブジェクトです。
        /// </summary>
        [FormerlySerializedAs("_closeUI")]
        [SerializeField]
        private GameObject _targetUI;

        /// <summary>
        /// 設定されている対象UIを閉じます。
        /// </summary>
        public void Close()
        {
            if (_targetUI == null)
            {
                Debug.LogWarning(
                    $"{nameof(CloseUI)}: " +
                    "閉じる対象のUIオブジェクトが設定されていません。",
                    this);

                return;
            }

            if (_targetUI.activeSelf)
            {
                _targetUI.SetActive(
                    false);
            }
        }

        /// <summary>
        /// クリックされたときに対象UIを閉じます。
        /// </summary>
        protected override void OnClickEnter()
        {
            Close();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上の設定を検証します。
        /// </summary>
        private void OnValidate()
        {
            if (_targetUI == null)
            {
                Debug.LogWarning(
                    $"{nameof(CloseUI)}: " +
                    "閉じる対象のUIオブジェクトを設定してください。",
                    this);
            }
        }
#endif
    }
}
