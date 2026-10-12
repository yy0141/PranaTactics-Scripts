using UnityEngine;
using UnityEngine.UI;

namespace PranaTactics.Tutorial
{
    /// <summary>
    /// フォント・画像アセットに依存しない丸・チェック付き進捗バー。
    /// </summary>
    public sealed class TutorialProgressGraphic : MaskableGraphic
    {
        protected override void Awake()
        {
            base.Awake();
            useLegacyMeshGeneration = false;
            raycastTarget = false;
        }

        private bool[] _completed;

        private int _current;

        private TutorialTheme _theme;

        public void SetProgress(TutorialStep step, bool[] completed, TutorialTheme theme)
        {
            _completed = (bool[])completed.Clone();
            _current = (int)step - 1;
            _theme = theme;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (_completed == null || _completed.Length == 0)
            {
                return;
            }

            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(_theme.ProgressNodeSize, rect.height) * 0.5f;
            float left = rect.xMin + radius;
            float gap = (rect.width - radius * 2) / Mathf.Max(1, _completed.Length - 1);

            for (int i = 0; i < _completed.Length - 1; i++)
            {
                Line(
                    vh,
                    new Vector2(left + gap * i, rect.center.y),
                    new Vector2(
                    left + gap * (i + 1),
                    rect.center.y),
                    _theme.ProgressLineWidth,
                    _completed[i] ? _theme.ProgressColor : _theme.ProgressTrackColor);
            }

            for (int i = 0; i < _completed.Length; i++)
            {
                Vector2 center = new Vector2(left + gap * i, rect.center.y);
                Color ring = _completed[i] || i == _current ? _theme.ProgressColor : _theme.ProgressTrackColor;

                Disk(vh, center, radius, ring);
                Disk(
                    vh,
                    center,
                    Mathf.Max(0, radius - _theme.ProgressLineWidth),
                    _theme.ObjectivePanel.Background);

                if (!_completed[i])
                {
                    continue;
                }

                Line(
                    vh,
                    center + new Vector2(-radius * .5f, 0),
                    center +
                    new Vector2(
                    -radius * .12f,
                    -radius * .35f),
                    _theme.ProgressLineWidth,
                    _theme.ProgressColor);
                Line(
                    vh,
                    center + new Vector2(-radius * .12f, -radius * .35f),
                    center +
                    new Vector2(
                    radius * .5f,
                    radius * .4f),
                    _theme.ProgressLineWidth,
                    _theme.ProgressColor);
            }
        }

        private static void Disk(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            int start = vh.currentVertCount;

            vh.AddVert(center, color, Vector2.zero);
            const int segments = 32;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;

                vh.AddVert(
                    center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    color,
                    Vector2.zero);

                if (i > 0)
                {
                    vh.AddTriangle(start, start + i, start + i + 1);
                }
            }
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 direction = (b - a).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * width * .5f;
            int start = vh.currentVertCount;

            vh.AddVert(a - normal, color, Vector2.zero);
            vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero);
            vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
