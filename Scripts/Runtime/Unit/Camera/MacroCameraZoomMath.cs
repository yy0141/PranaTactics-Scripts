using System;

namespace PranaTactics.Unit
{
    /// <summary>
    /// Macroカメラのスクロール範囲と、盤面を収める透視投影の高さを計算します。
    /// </summary>
    public static class MacroCameraZoomMath
    {
        /// <summary>
        /// 下スクロールで引き、上スクロールで戻す正規化ズーム値を返します。
        /// </summary>
        public static float ApplyScroll(float zoom, float scroll, float sensitivity)
        {
            return Math.Max(0.0f, Math.Min(1.0f, zoom - scroll * sensitivity));
        }

        /// <summary>
        /// 縦横の画角を満たし、初期高度を下回らない高さを返します。
        /// </summary>
        public static float CalculateFitHeight(
            float width,
            float depth,
            float fieldOfView,
            float aspect,
            float minimumHeight)
        {
            double tangent = Math.Tan(Math.Max(1.0, Math.Min(179.0, fieldOfView)) * Math.PI / 360.0);
            double halfSize = Math.Max(
                Math.Max(0.0f, depth) * 0.5,
                Math.Max(0.0f, width) * 0.5 / Math.Max(
                0.01f,
                aspect));

            return Math.Max(minimumHeight, (float)(halfSize * 1.08 / tangent));
        }
    }
}
