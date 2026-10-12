using UnityEngine;
using UnityEngine.UI;

namespace PranaTactics.UI.Screens.PranamProfile
{
    /// <summary>
    /// 兵装データベースのパネルに、角を切り落とした細い枠を描画します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PranamProfileFrame : MaskableGraphic
    {
        [Min(0.0f)]
        [SerializeField]
        private float _corner = 12.0f;

        [Min(0.1f)]
        [SerializeField]
        private float _thickness = 1.0f;

        /// <summary>
        /// 八角形の辺を四角形メッシュとして描画します。
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            float corner = Mathf.Min(_corner, Mathf.Min(rect.width, rect.height) * 0.5f);
            Vector2[] points =
            {
                new(rect.xMin + corner, rect.yMin), new(rect.xMax - corner, rect.yMin),
                new(rect.xMax, rect.yMin + corner), new(rect.xMax, rect.yMax - corner),
                new(rect.xMax - corner, rect.yMax), new(rect.xMin + corner, rect.yMax),
                new(rect.xMin, rect.yMax - corner), new(rect.xMin, rect.yMin + corner)
            };

            for (int index = 0; index < points.Length; index++)
            {
                Vector2 start = points[index];
                Vector2 end = points[(index + 1) % points.Length];
                Vector2 direction = (end - start).normalized;
                Vector2 offset = new Vector2(-direction.y, direction.x) * _thickness;
                int first = helper.currentVertCount;
                helper.AddVert(start, color, Vector2.zero);
                helper.AddVert(end, color, Vector2.zero);
                helper.AddVert(end + offset, color, Vector2.zero);
                helper.AddVert(start + offset, color, Vector2.zero);
                helper.AddTriangle(first, first + 1, first + 2);
                helper.AddTriangle(first, first + 2, first + 3);
            }
        }
    }
}
