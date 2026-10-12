using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.UI
{
    /// <summary>
    /// TextMeshProUGUIに表示する文字列、文字色および
    /// 透明度を外部から変更します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChangeTextMeshProUGUI : MonoBehaviour
    {
        /// <summary>
        /// 表示内容を変更するTextMeshProUGUIです。
        /// </summary>
        [FormerlySerializedAs("_textUI")]
        [SerializeField]
        private TextMeshProUGUI _targetText;

        public TextMeshProUGUI TargetText
        {
            get
            {
                return _targetText;
            }
        }

        /// <summary>
        /// 必要なコンポーネントへの参照を取得します。
        /// </summary>
        private void Awake()
        {
            CacheTextReference();
        }

        /// <summary>
        /// 対象テキストへ文字列を設定します。
        /// </summary>
        /// <param name="text">
        /// 表示する文字列です。
        /// <c>null</c>の場合は空文字列を設定します。
        /// </param>
        public void SetText(
            string text)
        {
            if (!TryGetTargetText(
                    out TextMeshProUGUI targetText))
            {
                return;
            }

            targetText.text =
                text ??
                string.Empty;
        }

        /// <summary>
        /// 対象テキストへ文字色を設定します。
        /// </summary>
        /// <param name="color">
        /// 設定する文字色です。
        /// </param>
        public void SetColor(
            Color color)
        {
            if (!TryGetTargetText(
                    out TextMeshProUGUI targetText))
            {
                return;
            }

            targetText.color =
                color;
        }

        /// <summary>
        /// 現在のRGB値を維持したまま、
        /// 対象テキストの透明度を設定します。
        /// </summary>
        /// <param name="alpha">
        /// 設定する透明度です。
        /// 0から1の範囲へ補正されます。
        /// </param>
        public void SetAlpha(
            float alpha)
        {
            if (!TryGetTargetText(
                    out TextMeshProUGUI targetText))
            {
                return;
            }

            Color textColor =
                targetText.color;

            textColor.a =
                Mathf.Clamp01(
                    alpha);

            targetText.color =
                textColor;
        }

        /// <summary>
        /// 対象テキストへ文字列と文字色を同時に設定します。
        /// </summary>
        /// <param name="text">
        /// 表示する文字列です。
        /// </param>
        /// <param name="color">
        /// 設定する文字色です。
        /// </param>
        public void SetTextAndColor(
            string text,
            Color color)
        {
            if (!TryGetTargetText(
                    out TextMeshProUGUI targetText))
            {
                return;
            }

            targetText.text =
                text ??
                string.Empty;

            targetText.color =
                color;
        }

        /// <summary>
        /// 対象となるTextMeshProUGUIを取得します。
        /// </summary>
        /// <param name="targetText">
        /// 取得されたTextMeshProUGUIです。
        /// </param>
        /// <returns>
        /// 対象を取得できた場合は<c>true</c>です。
        /// </returns>
        private bool TryGetTargetText(
            out TextMeshProUGUI targetText)
        {
            if (_targetText == null)
            {
                CacheTextReference();
            }

            targetText =
                _targetText;

            return targetText != null;
        }

        /// <summary>
        /// 自身または子オブジェクトから
        /// TextMeshProUGUIを取得します。
        /// </summary>
        private void CacheTextReference()
        {
            if (_targetText != null)
            {
                return;
            }

            _targetText =
                GetComponent<TextMeshProUGUI>();

            if (_targetText == null)
            {
                _targetText =
                    GetComponentInChildren<TextMeshProUGUI>(
                        true);
            }

            if (_targetText == null)
            {
                Debug.LogError(
                    $"{nameof(ChangeTextMeshProUGUI)}: " +
                    $"{nameof(TextMeshProUGUI)}が設定されていません。",
                    this);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上で対象テキストを自動設定します。
        /// </summary>
        private void OnValidate()
        {
            if (_targetText == null)
            {
                _targetText =
                    GetComponent<TextMeshProUGUI>();
            }

            if (_targetText == null)
            {
                _targetText =
                    GetComponentInChildren<TextMeshProUGUI>(
                        true);
            }
        }
#endif
    }
}
