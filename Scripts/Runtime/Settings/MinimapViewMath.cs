using System;

namespace PranaTactics.Settings
{
    /// <summary>
    /// ミニマップの表示範囲と倍率の計算を担当します。
    /// </summary>
    public static class MinimapViewMath
    {
        /// <summary>
        /// カメラのローカルY座標へ適用する最低高さです。
        /// </summary>
        public const float MinimumCameraHeight = 70.0f;

        /// <summary>
        /// 固定倍率1倍に対応するカメラのローカル高さです。
        /// </summary>
        public const float DefaultCameraHeight = 175.0f;

        /// <summary>
        /// 扇形の外側へ確保する表示範囲の倍率です。
        /// </summary>
        private const float k_RangeMarginMultiplier = 1.2f;

        /// <summary>
        /// 真下を向くカメラへ扇形全体が収まる高さを計算します。
        /// </summary>
        /// <remarks>
        /// ワールド半径に余裕を加え、縦横の狭い方の画角で除算します。
        /// fieldOfViewは度数で表す垂直画角、aspectは描画先の幅と高さの比です。
        /// </remarks>
        public static float CalculateCameraHeight(
            float mainRadius,
            float subRadius,
            float fieldOfView,
            float aspect)
        {
            float radius = Math.Max(Sanitize(mainRadius, 0.0f), Sanitize(subRadius, 0.0f));
            float halfAngle = Clamp(fieldOfView, 1.0f, 179.0f, 60.0f) * (float)Math.PI / 360.0f;
            float halfExtent = radius * k_RangeMarginMultiplier / Clamp(aspect, 0.01f, 1.0f, 1.0f);

            return Math.Max(MinimumCameraHeight, halfExtent / (float)Math.Tan(halfAngle));
        }

        /// <summary>
        /// 固定倍率をカメラの高さへ変換します。
        /// </summary>
        public static float CalculateFixedCameraHeight(float zoom)
        {
            return DefaultCameraHeight / Clamp(zoom, 0.25f, DefaultCameraHeight / MinimumCameraHeight, 1.0f);
        }

        /// <summary>
        /// ワールド座標から変換したローカル高さに下限を適用します。
        /// </summary>
        public static float ClampLocalCameraHeight(float localHeight)
        {
            return Math.Max(MinimumCameraHeight, Sanitize(localHeight, MinimumCameraHeight));
        }

        /// <summary>
        /// 拡大前後で右端または上端を維持する位置補正量を計算します。
        /// </summary>
        public static float CalculateEdgeOffset(float size, float originalScale, float pivot, float scale)
        {
            return size * originalScale * (1.0f - pivot) * (scale - 1.0f);
        }

        /// <summary>
        /// 非数値と無限大を既定値へ戻し、指定範囲へ補正します。
        /// </summary>
        public static float Clamp(float value, float minimum, float maximum, float defaultValue)
        {
            return Math.Max(minimum, Math.Min(maximum, Sanitize(value, defaultValue)));
        }

        /// <summary>
        /// 有限の数値を取得します。
        /// </summary>
        private static float Sanitize(float value, float defaultValue)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? defaultValue : value;
        }
    }
}
