using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace PranaTactics.UI
{
    /// <summary>
    /// UIの選択状態に応じて、
    /// 対象画像の色を切り替えます。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChangeImageColor : MonoBehaviour
    {
        /// <summary>
        /// 色を変更する対象画像です。
        /// </summary>
        [FormerlySerializedAs("_image")]
        [SerializeField]
        private Image _targetImage;

        /// <summary>
        /// 未選択状態で適用する色です。
        /// </summary>
        [FormerlySerializedAs("_baseColor")]
        [SerializeField]
        private Color _normalColor =
            Color.white;

        /// <summary>
        /// 選択状態で適用する色です。
        /// </summary>
        [FormerlySerializedAs("_changeColor")]
        [SerializeField]
        private Color _selectedColor =
            Color.white;

        /// <summary>
        /// 現在選択状態かどうかを示します。
        /// </summary>
        private bool _isSelected;

        public bool IsSelected
        {
            get
            {
                return _isSelected;
            }
        }

        /// <summary>
        /// 必要なコンポーネントへの参照を取得します。
        /// </summary>
        private void Awake()
        {
            CacheReferences();
        }

        /// <summary>
        /// コンポーネントが有効になったとき、
        /// 現在の選択状態に対応する色を適用します。
        /// </summary>
        private void OnEnable()
        {
            ApplyCurrentColor();
        }

        /// <summary>
        /// UIの選択状態を設定し、
        /// 対象画像の色を更新します。
        /// </summary>
        /// <param name="isSelected">
        /// 選択状態にする場合は<c>true</c>、
        /// 通常状態に戻す場合は<c>false</c>です。
        /// </param>
        public void SetSelected(
            bool isSelected)
        {
            if (_isSelected ==
                isSelected)
            {
                ApplyCurrentColor();

                return;
            }

            _isSelected =
                isSelected;

            ApplyCurrentColor();
        }

        /// <summary>
        /// 選択状態と未選択状態を切り替えます。
        /// </summary>
        public void ToggleSelected()
        {
            SetSelected(
                !_isSelected);
        }

        /// <summary>
        /// 現在の選択状態に対応する色を対象画像へ適用します。
        /// </summary>
        private void ApplyCurrentColor()
        {
            if (_targetImage == null)
            {
                CacheReferences();
            }

            if (_targetImage == null)
            {
                return;
            }

            _targetImage.color =
                _isSelected
                    ? _selectedColor
                    : _normalColor;
        }

        /// <summary>
        /// 対象画像への参照を取得します。
        /// </summary>
        private void CacheReferences()
        {
            if (_targetImage != null)
            {
                return;
            }

            _targetImage =
                GetComponent<Image>();

            if (_targetImage == null)
            {
                Debug.LogError(
                    $"{nameof(ChangeImageColor)}: " +
                    $"{nameof(Image)}が設定されていません。",
                    this);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上の参照を自動設定します。
        /// </summary>
        private void OnValidate()
        {
            if (_targetImage == null)
            {
                _targetImage =
                    GetComponent<Image>();
            }
        }
#endif
    }
}
