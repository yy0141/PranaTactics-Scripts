using UnityEngine;
using UnityEngine.UI;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 戦術HUDの切り欠きパネル、背景ゲージおよび円周ゲージを描画します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TacticalHudGraphic : MaskableGraphic
    {
        [SerializeField]
        private bool _isRing;

        /// <summary>
        /// リングの区間に隙間を設けず、連続した実線で描画するかどうかです。
        /// </summary>
        [SerializeField]
        private bool _isSolidRing;

        /// <summary>
        /// パネルの背景色を横方向のゲージとして描画するかどうかです。
        /// </summary>
        [SerializeField]
        private bool _isSurfaceGauge;

        [SerializeField]
        private bool _mirror;

        [SerializeField, Min(0.0f)]
        private float _corner = 12.0f;

        [SerializeField, Min(0.5f)]
        private float _thickness = 2.0f;

        [SerializeField]
        private Color _surfaceColor = new(0.025f, 0.045f, 0.055f, 0.88f);

        [SerializeField, Range(0.0f, 1.0f)]
        private float _fill = 1.0f;

        /// <summary>
        /// 兵装の選択状態に合わせて背景へアクセント色を加えるかどうかです。
        /// </summary>
        private bool _isHighlighted;

        /// <summary>
        /// 選択中の兵装を色付きの背景で強調します。
        /// </summary>
        public void SetHighlighted(bool isHighlighted)
        {
            if (_isHighlighted == isHighlighted)
            {
                return;
            }

            _isHighlighted = isHighlighted;
            SetVerticesDirty();
        }

        /// <summary>
        /// 円周または背景ゲージの割合を設定し、変化がある場合だけ再描画します。
        /// </summary>
        public void SetFill(float fill)
        {
            float validFill = Mathf.Clamp01(fill);
            if (Mathf.Approximately(_fill, validFill))
            {
                return;
            }

            _fill = validFill;
            SetVerticesDirty();
        }

        /// <summary>
        /// Inspector設定に従ってパネルまたはリングのメッシュを生成します。
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (_isRing)
            {
                DrawRing(helper);
                return;
            }

            DrawPanel(helper);
        }

        /// <summary>
        /// 左右反転可能な切り欠き枠と半透明の面を描画します。
        /// </summary>
        private void DrawPanel(VertexHelper helper)
        {
            Rect rect = rectTransform.rect;
            float corner = Mathf.Min(_corner, Mathf.Min(rect.width, rect.height) * 0.3f);
            Vector2[] points =
            {
                new(rect.xMin + corner, rect.yMin),
                new(rect.xMax - corner, rect.yMin),
                new(rect.xMax, rect.yMin + corner),
                new(rect.xMax, rect.yMax - corner),
                new(rect.xMax - corner * 2.0f, rect.yMax - corner),
                new(rect.xMax - corner * 3.0f, rect.yMax),
                new(rect.xMin + corner, rect.yMax),
                new(rect.xMin, rect.yMax - corner),
                new(rect.xMin, rect.yMin + corner)
            };

            for (int index = 0; index < points.Length; index++)
            {
                if (_mirror)
                {
                    points[index].x = rect.center.x * 2.0f - points[index].x;
                }
            }

            Color surface = _isHighlighted && !_isSurfaceGauge
                ? Color.Lerp(_surfaceColor, new Color(color.r, color.g, color.b, 0.95f), 0.36f)
                : _surfaceColor;
            surface.a *= color.a;
            helper.AddVert(rect.center, surface, Vector2.zero);
            foreach (Vector2 point in points)
            {
                helper.AddVert(point, surface, Vector2.zero);
            }

            for (int index = 0; index < points.Length; index++)
            {
                int next = (index + 1) % points.Length;
                helper.AddTriangle(0, index + 1, next + 1);
            }

            if (_isSurfaceGauge && _fill > 0.0f)
            {
                DrawSurfaceGauge(helper, points, rect);
            }

            for (int index = 0; index < points.Length; index++)
            {
                int next = (index + 1) % points.Length;
                Vector2 start = points[index];
                Vector2 end = points[next];
                Vector2 direction = (end - start).normalized;
                Vector2 offset = new(-direction.y, direction.x);
                DrawQuad(helper, start, end, end + offset * _thickness,
                    start + offset * _thickness, color);
            }
        }

        /// <summary>
        /// 切り欠き形状を維持して、背景の左端から指定割合までアクセント色を描画します。
        /// </summary>
        private void DrawSurfaceGauge(VertexHelper helper, Vector2[] points, Rect rect)
        {
            Color tint = Color.Lerp(_surfaceColor,
                new Color(color.r, color.g, color.b, 0.95f), _isHighlighted ? 0.36f : 0.22f);
            tint.a *= color.a;
            float boundary = Mathf.Lerp(rect.xMin, rect.xMax, _fill);
            Vector2[] triangle = new Vector2[3];
            Vector2[] clipped = new Vector2[4];
            for (int index = 0; index < points.Length; index++)
            {
                triangle[0] = rect.center;
                triangle[1] = points[index];
                triangle[2] = points[(index + 1) % points.Length];
                int count = ClipSurfaceTriangle(triangle, clipped, boundary);
                int first = helper.currentVertCount;
                for (int vertex = 0; vertex < count; vertex++)
                {
                    helper.AddVert(clipped[vertex], tint, Vector2.zero);
                }

                for (int vertex = 1; vertex < count - 1; vertex++)
                {
                    helper.AddTriangle(first, first + vertex, first + vertex + 1);
                }
            }
        }

        /// <summary>
        /// 背景の三角形をゲージ境界の左側へ切り取り、最大4頂点を返します。
        /// </summary>
        private static int ClipSurfaceTriangle(Vector2[] triangle, Vector2[] clipped, float boundary)
        {
            int count = 0;
            Vector2 previous = triangle[triangle.Length - 1];
            foreach (Vector2 current in triangle)
            {
                bool previousInside = previous.x <= boundary;
                bool currentInside = current.x <= boundary;
                if (previousInside != currentInside)
                {
                    float ratio = (boundary - previous.x) / (current.x - previous.x);
                    clipped[count++] = Vector2.Lerp(previous, current, ratio);
                }

                if (currentInside)
                {
                    clipped[count++] = current;
                }

                previous = current;
            }

            return count;
        }

        /// <summary>
        /// 12時を起点に時計回りに減る細分化したリングを描画します。
        /// </summary>
        private void DrawRing(VertexHelper helper)
        {
            const int k_SegmentCount = 120;
            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            float innerRadius = Mathf.Max(0.0f, radius - _thickness);
            float segments = _fill * k_SegmentCount;
            float segmentLength = _isSolidRing ? 1.0f : 0.86f;
            for (int index = 0; index < Mathf.CeilToInt(segments); index++)
            {
                float startAngle = (90.0f - index * 360.0f / k_SegmentCount) * Mathf.Deg2Rad;
                float endAngle = (90.0f - Mathf.Min(index + segmentLength, segments) *
                    360.0f / k_SegmentCount) * Mathf.Deg2Rad;
                Vector2 start = new(Mathf.Cos(startAngle), Mathf.Sin(startAngle));
                Vector2 end = new(Mathf.Cos(endAngle), Mathf.Sin(endAngle));
                DrawQuad(helper, rect.center + start * radius, rect.center + end * radius,
                    rect.center + end * innerRadius, rect.center + start * innerRadius, color);
            }
        }

        /// <summary>
        /// 指定された四角形を2つの三角形として追加します。
        /// </summary>
        private static void DrawQuad(VertexHelper helper, Vector2 first, Vector2 second,
            Vector2 third, Vector2 fourth, Color tint)
        {
            int offset = helper.currentVertCount;
            helper.AddVert(first, tint, Vector2.zero);
            helper.AddVert(second, tint, Vector2.zero);
            helper.AddVert(third, tint, Vector2.zero);
            helper.AddVert(fourth, tint, Vector2.zero);
            helper.AddTriangle(offset, offset + 1, offset + 2);
            helper.AddTriangle(offset, offset + 2, offset + 3);
        }
    }
}
