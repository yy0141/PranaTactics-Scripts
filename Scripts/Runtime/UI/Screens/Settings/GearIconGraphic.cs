using UnityEngine;
using UnityEngine.UI;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// フォントや画像素材に依存せず、中央に穴のある八歯の歯車を描画します。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GearIconGraphic : MaskableGraphic
    {
        /// <summary>
        /// 内周と歯形の外周を三角形で接続します。
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            const int k_Segments = 64;

            for (int i = 0; i <= k_Segments; i++)
            {
                float angle = i * Mathf.PI * 2 / k_Segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float outerRadius = i % 8 >= 2 && i % 8 <= 5 ? radius : radius * 0.76f;

                helper.AddVert(rect.center + direction * radius * 0.38f, color, Vector2.zero);
                helper.AddVert(rect.center + direction * outerRadius, color, Vector2.zero);

                if (i > 0)
                {
                    int index = i * 2;

                    helper.AddTriangle(index - 2, index - 1, index);
                    helper.AddTriangle(index - 1, index + 1, index);
                }
            }
        }
    }
}
