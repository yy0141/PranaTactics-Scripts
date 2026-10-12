using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 一定幅の経路線、独立した方向マーカーと目的地マーカーを単一メッシュへ描画します。
    /// </summary>
    public sealed class MovementPathMesh
    {
        /// <summary>
        /// 外部フォントに依存せず数字を描くための7セグメント定義です。
        /// </summary>
        private static readonly int[] s_digitMasks = { 63, 6, 91, 79, 102, 109, 125, 7, 127, 111 };

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<int> _triangles = new List<int>();

        /// <summary>
        /// ユニットIDから固定色を取得します。
        /// </summary>
        public static Color GetUnitColor(int playerId)
        {
            float hue = Mathf.Repeat(0.08f + playerId * 0.61803398875f, 1.0f);
            return Color.HSVToRGB(hue, 0.72f, 1.0f);
        }

        /// <summary>
        /// 再構築用の頂点をクリアします。
        /// </summary>
        public void Clear()
        {
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();
        }

        /// <summary>
        /// 移動済み区間だけを薄くし、保存した基準色を維持します。
        /// </summary>
        public static Color GetRouteColor(Color color, int step, int completedSteps,
            float completedOpacity, float activeOpacity)
        {
            color.a *= step < completedSteps ? completedOpacity : activeOpacity;
            return color;
        }

        /// <summary>
        /// 一定幅の明るいラインと暗いアウトラインを追加します。
        /// </summary>
        public void AddRouteLine(Vector3 start, Vector3 end, float width, float outlineWidth,
            Color color, Color outline, bool outlineOnly, bool roundedCaps = true)
        {
            outline.a *= color.a;
            Color strokeColor = outlineOnly ? outline : color;
            float strokeWidth = outlineOnly ? outlineWidth : width;
            Vector3 elevation = outlineOnly ? Vector3.zero : Vector3.up * 0.001f;
            AddRibbon(start + elevation, end + elevation, strokeWidth, strokeColor);
            if (roundedCaps)
            {
                AddDisc(start + elevation, strokeWidth * 0.5f, strokeColor);
                AddDisc(end + elevation, strokeWidth * 0.5f, strokeColor);
            }
        }

        /// <summary>
        /// ラインから独立した三角形または山形の方向マーカーを追加します。
        /// </summary>
        public void AddDirectionMarker(Vector3 center, Vector3 direction, float length, float width,
            Color color, Color outline, MovementPathDirectionShape shape)
        {
            direction.y = 0.0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            direction.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            outline.a *= color.a;
            center += Vector3.up * 0.006f;
            if (shape == MovementPathDirectionShape.Chevron)
            {
                Vector3 tip = center + direction * length * 0.5f;
                Vector3 tail = center - direction * length * 0.5f;
                AddRibbon(tail + side * width * 0.5f, tip, width * 0.3f, outline);
                AddRibbon(tail - side * width * 0.5f, tip, width * 0.3f, outline);
                AddRibbon(tail + side * width * 0.5f, tip, width * 0.16f, color);
                AddRibbon(tail - side * width * 0.5f, tip, width * 0.16f, color);
                return;
            }

            AddMarkerTriangle(center, direction, side, length * 1.15f, width * 1.15f, outline);
            AddMarkerTriangle(center + Vector3.up * 0.001f, direction, side, length, width, color);
        }

        /// <summary>
        /// 進行方向を示す単純な三角形を追加します。
        /// </summary>
        private void AddMarkerTriangle(Vector3 center, Vector3 direction, Vector3 side,
            float length, float width, Color color)
        {
            Vector3 tail = center - direction * length * 0.5f;
            AddTriangle(center + direction * length * 0.5f,
                tail + side * width * 0.5f, tail - side * width * 0.5f, color);
        }

        /// <summary>
        /// 開始地点を小さい円、到達地点を大きい円または六角形で表示します。
        /// </summary>
        public void AddEndpoint(Vector3 position, float size, Color color, Color outline,
            bool isDestination, MovementPathGoalShape shape)
        {
            outline.a *= color.a;
            int sides = isDestination && shape == MovementPathGoalShape.Hexagon ? 6 : 16;
            AddDisc(position, size * 1.18f, outline, sides);
            AddDisc(position + Vector3.up * 0.001f, size, color, sides);
            if (isDestination)
            {
                AddDisc(position + Vector3.up * 0.002f, size * 0.62f, outline, sides);
                AddDisc(position + Vector3.up * 0.003f, size * 0.3f, color, sides);
            }
        }

        /// <summary>
        /// 端点と独立した位置へユニット番号のバッジを描画します。
        /// </summary>
        public void AddUnitLabel(Vector3 position, float size, int playerId, Color color, Color outline)
        {
            Vector3 label = position + Vector3.right * size * 1.65f;
            float height = size * 2.0f;
            int digitCount = Mathf.Max(0, playerId).ToString().Length;
            float badgeWidth = height * (0.95f + digitCount * 0.7f);
            Vector3 lower = label - new Vector3(height * 0.15f, 0, height * 0.15f);
            Vector3 upper = lower + Vector3.right * badgeWidth + Vector3.forward * height * 1.3f;
            AddTriangle(lower, new Vector3(lower.x, lower.y, upper.z), upper, outline);
            AddTriangle(lower, upper, new Vector3(upper.x, lower.y, lower.z), outline);
            AddRibbon(label, label + Vector3.forward * height, height * 0.1f, color);
            AddRibbon(label, label + Vector3.right * height * 0.45f, height * 0.1f, color);
            AddRibbon(label + Vector3.right * height * 0.45f,
                label + Vector3.right * height * 0.45f + Vector3.forward * height, height * 0.1f, color);
            AddNumber(playerId, label + Vector3.right * height * 0.75f, height, color);
        }

        /// <summary>
        /// 円または正多角形を描画します。
        /// </summary>
        private void AddDisc(Vector3 center, float radius, Color color, int sides = 8)
        {
            for (int index = 0; index < sides; index++)
            {
                float firstAngle = index * Mathf.PI * 2.0f / sides;
                float secondAngle = (index + 1) * Mathf.PI * 2.0f / sides;
                AddTriangle(center,
                    center + new Vector3(Mathf.Cos(firstAngle), 0, Mathf.Sin(firstAngle)) * radius,
                    center + new Vector3(Mathf.Cos(secondAngle), 0, Mathf.Sin(secondAngle)) * radius, color);
            }
        }

        /// <summary>
        /// 蓄積した頂点と色を描画メッシュへ反映します。
        /// </summary>
        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// 任意方向の細い帯を追加します。
        /// </summary>
        private void AddRibbon(Vector3 start, Vector3 end, float width, Color color)
        {
            Vector3 direction = end - start;
            direction.y = 0.0f;
            if (direction.sqrMagnitude < 0.000001f)
            {
                return;
            }

            Vector3 side = Vector3.Cross(Vector3.up, direction.normalized) * width * 0.5f;
            AddTriangle(start - side, start + side, end + side, color);
            AddTriangle(start - side, end + side, end - side, color);
        }

        /// <summary>
        /// 頂点カラーを持つ三角形を追加します。
        /// </summary>
        private void AddTriangle(Vector3 first, Vector3 second, Vector3 third, Color color)
        {
            int start = _vertices.Count;
            _vertices.Add(first);
            _vertices.Add(second);
            _vertices.Add(third);
            _colors.Add(color);
            _colors.Add(color);
            _colors.Add(color);
            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
        }

        /// <summary>
        /// 上から読む数字を7セグメントの線で追加します。
        /// </summary>
        private void AddNumber(int number, Vector3 position, float height, Color color)
        {
            string digits = Mathf.Max(0, number).ToString();
            for (int index = 0; index < digits.Length; index++)
            {
                int mask = s_digitMasks[digits[index] - '0'];
                Vector3 origin = position + Vector3.right * height * 0.7f * index;
                for (int segment = 0; segment < 7; segment++)
                {
                    if ((mask & (1 << segment)) == 0)
                    {
                        continue;
                    }

                    GetDigitStroke(segment, out Vector2 start, out Vector2 end);
                    AddRibbon(origin + new Vector3(start.x, 0.004f, start.y) * height,
                        origin + new Vector3(end.x, 0.004f, end.y) * height,
                        height * 0.09f, color);
                }
            }
        }

        /// <summary>
        /// 数字の各線分を取得します。
        /// </summary>
        private static void GetDigitStroke(int segment, out Vector2 start, out Vector2 end)
        {
            switch (segment)
            {
                case 0:
                    start = new Vector2(0, 1);
                    end = new Vector2(0.5f, 1);
                    break;
                case 1:
                    start = new Vector2(0.5f, 1);
                    end = new Vector2(0.5f, 0.5f);
                    break;
                case 2:
                    start = new Vector2(0.5f, 0.5f);
                    end = new Vector2(0.5f, 0);
                    break;
                case 3:
                    start = new Vector2(0.5f, 0);
                    end = new Vector2(0, 0);
                    break;
                case 4:
                    start = new Vector2(0, 0);
                    end = new Vector2(0, 0.5f);
                    break;
                case 5:
                    start = new Vector2(0, 0.5f);
                    end = new Vector2(0, 1);
                    break;
                default:
                    start = new Vector2(0, 0.5f);
                    end = new Vector2(0.5f, 0.5f);
                    break;
            }
        }
    }
}
