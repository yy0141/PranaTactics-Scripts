using UnityEngine;

using PranaTactics.Settings;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// ミニマップの右上の余白を維持して表示サイズを変更します。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class MinimapUiScaler : MonoBehaviour
    {
        private RectTransform _rectTransform;

        private Vector3 _originalScale;

        private Vector2 _originalPosition;

        /// <summary>
        /// 右上アンカーの基準位置とスケールを保存します。
        /// </summary>
        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
            _originalScale = _rectTransform.localScale;
            _originalPosition = _rectTransform.anchoredPosition;
        }

        /// <summary>
        /// 拡大による右端と上端の移動量を位置から差し引きます。
        /// </summary>
        private void LateUpdate()
        {
            float scale = SettingsManager.Current.MinimapUiScale;
            Vector2 size = _rectTransform.rect.size;
            Vector2 pivot = _rectTransform.pivot;

            _rectTransform.localScale = _originalScale * scale;
            _rectTransform.anchoredPosition = _originalPosition - new Vector2(
                MinimapViewMath.CalculateEdgeOffset(size.x, _originalScale.x, pivot.x, scale),
                MinimapViewMath.CalculateEdgeOffset(size.y, _originalScale.y, pivot.y, scale));
        }
    }
}
