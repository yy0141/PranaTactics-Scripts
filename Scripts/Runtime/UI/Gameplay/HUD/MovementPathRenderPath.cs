using System;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// レーンを反映した連続経路と、方向マーカーの移動距離を保持します。
    /// </summary>
    public sealed class MovementPathRenderPath
    {
        /// <summary>
        /// 一区間の表示位置とピクセル換算情報です。
        /// </summary>
        public sealed class Stroke
        {
            public Vector3 Start;
            public Vector3 End;
            public float CellSize;
            public float PixelsPerCell;
            public float Width;
            public float OutlineWidth;
            public float StartDistance;
            public float Length;
            public int Step;
            public bool IsTurn;
        }

        private readonly List<Stroke> _strokes = new List<Stroke>();
        public IReadOnlyList<Stroke> Strokes => _strokes;
        public float Length { get; private set; }
        public Vector3 Origin { get; set; }
        public Vector3 Destination { get; set; }
        public Vector3 LabelPosition { get; set; }
        public float StartRadius { get; set; }
        public float GoalRadius { get; set; }
        public bool ShowLabels { get; set; }
        public bool IsPlayer { get; set; }
        public int PlayerId { get; set; }
        public int CompletedSteps { get; set; }
        public Color LineColor { get; set; }
        public Color DirectionColor { get; set; }

        /// <summary>
        /// レーン区間を登録し、距離をセル単位で累積します。
        /// </summary>
        public void Add(Vector3 start, Vector3 end, float cellSize, float pixels,
            float width, float outlineWidth, int step, bool isTurn = false)
        {
            float length = (end - start).magnitude / Math.Max(0.1f, cellSize);
            if (length < 0.0001f)
            {
                return;
            }

            _strokes.Add(new Stroke
            {
                Start = start, End = end, CellSize = cellSize, PixelsPerCell = pixels,
                Width = width, OutlineWidth = outlineWidth, StartDistance = Length,
                Length = length, Step = step, IsTurn = isTurn
            });
            Length += length;
        }

        /// <summary>
        /// 距離に対応する区間を取得します。距離は終点から始点へ循環します。
        /// </summary>
        public bool TrySample(float distance, out Stroke stroke, out Vector3 position, out Vector3 direction)
        {
            stroke = null;
            position = Vector3.zero;
            direction = Vector3.zero;
            if (Length <= 0.0f || float.IsNaN(distance) || float.IsInfinity(distance))
            {
                return false;
            }

            float wrapped = distance % Length;
            if (wrapped < 0.0f)
            {
                wrapped += Length;
            }

            foreach (Stroke candidate in _strokes)
            {
                if (wrapped < candidate.StartDistance + candidate.Length)
                {
                    stroke = candidate;
                    position = Vector3.Lerp(stroke.Start, stroke.End,
                        (wrapped - stroke.StartDistance) / stroke.Length);
                    direction = (stroke.End - stroke.Start).normalized;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 線本体を生成し、AIの破線の位相を曲がり角でも継続します。
        /// </summary>
        public void BuildLines(MovementPathMesh builder, MovementPathData data)
        {
            builder.Clear();
            if (!data.ShowRouteLines)
            {
                return;
            }

            for (int pass = 0; pass < 2; pass++)
            {
                foreach (Stroke stroke in _strokes)
                {
                    Color color = MovementPathMesh.GetRouteColor(LineColor, stroke.Step, CompletedSteps,
                        data.CompletedOpacity, data.ActiveOpacity);
                    if (IsPlayer || !data.DashAllyRoutes)
                    {
                        builder.AddRouteLine(stroke.Start, stroke.End, stroke.Width, stroke.OutlineWidth,
                            color, data.OutlineColor, pass == 0);
                        continue;
                    }

                    float period = data.DashLengthCells + data.DashGapCells;
                    float offset = 0.0f;
                    while (offset < stroke.Length)
                    {
                        float phase = (stroke.StartDistance + offset) % period;
                        bool visible = phase < data.DashLengthCells;
                        float remaining = visible ? data.DashLengthCells - phase : period - phase;
                        float next = Mathf.Min(stroke.Length, offset + Mathf.Max(0.001f, remaining));
                        if (visible)
                        {
                            builder.AddRouteLine(Vector3.Lerp(stroke.Start, stroke.End, offset / stroke.Length),
                                Vector3.Lerp(stroke.Start, stroke.End, next / stroke.Length),
                                stroke.Width, stroke.OutlineWidth, color, data.OutlineColor, pass == 0, false);
                        }

                        offset = next;
                    }
                }
            }
        }

        /// <summary>
        /// 距離帯の表示密度から、流れるマーカーの個数を取得します。
        /// </summary>
        public int GetDirectionMarkerCount(MovementPathData data)
        {
            if (!data.ShowDirectionMarkers || _strokes.Count == 0)
            {
                return 0;
            }

            float spacing = data.GetMarkerSpacing(_strokes[0].PixelsPerCell);
            int turns = 0;
            if (data.HighlightTurns &&
                (data.HighlightTurnsAtFarDistance || _strokes[0].PixelsPerCell >= data.MiddleCellPixels))
            {
                foreach (Stroke stroke in _strokes)
                {
                    if (stroke.IsTurn)
                    {
                        turns++;
                    }
                }
            }

            return Math.Min(Math.Max(1, data.MaximumDirectionMarkers - turns),
                Math.Max(1, (int)Math.Ceiling(Length / spacing)));
        }

        /// <summary>
        /// マーカー位置を進行方向へ動かし、曲がり角と目的地も別メッシュへ描画します。
        /// </summary>
        public void BuildMarkers(MovementPathMesh builder, MovementPathData data, float time, bool animate)
        {
            builder.Clear();
            if (_strokes.Count == 0)
            {
                return;
            }

            int count = GetDirectionMarkerCount(data);
            float offset = animate && data.AnimateDirectionMarkers ? time * data.DirectionSpeedCellsPerSecond : 0.0f;
            for (int index = 0; index < count; index++)
            {
                float distance = (index + 0.5f) * Length / count + offset;
                if (TrySample(distance, out Stroke stroke, out Vector3 position, out Vector3 direction))
                {
                    AddMarker(builder, data, stroke, position, direction);
                }
            }

            if (data.ShowDirectionMarkers && data.HighlightTurns &&
                (data.HighlightTurnsAtFarDistance || _strokes[0].PixelsPerCell >= data.MiddleCellPixels))
            {
                int remaining = data.MaximumDirectionMarkers - count;
                foreach (Stroke stroke in _strokes)
                {
                    if (remaining > 0 && stroke.IsTurn)
                    {
                        AddMarker(builder, data, stroke, Vector3.Lerp(stroke.Start, stroke.End, 0.25f),
                            (stroke.End - stroke.Start).normalized, data.TurnMarkerScale);
                        remaining--;
                    }
                }
            }

            if (data.ShowStartMarker)
            {
                builder.AddEndpoint(Origin, StartRadius, LineColor, data.OutlineColor, false, data.GoalShape);
            }
            float pulse = animate && data.PulseGoal
                ? 1.0f + data.GoalPulseAmplitude * (0.5f - 0.5f * Mathf.Cos(time * Mathf.PI * 2 * data.GoalPulseCyclesPerSecond))
                : 1.0f;
            if (data.ShowGoalMarker)
            {
                builder.AddEndpoint(Destination, GoalRadius * pulse, LineColor, data.OutlineColor, true, data.GoalShape);
            }
            if (ShowLabels)
            {
                builder.AddUnitLabel(LabelPosition, StartRadius, PlayerId, DirectionColor, data.OutlineColor);
            }
        }

        /// <summary>
        /// 区間の進行方向と画面サイズに合わせて単純な方向マーカーを追加します。
        /// </summary>
        private void AddMarker(MovementPathMesh builder, MovementPathData data, Stroke stroke,
            Vector3 position, Vector3 direction, float scale = 1.0f)
        {
            float pixels = Mathf.Max(1.0f, stroke.PixelsPerCell);
            float length = Mathf.Min(data.MaximumMarkerCells, data.DirectionMarkerLengthPixels * scale / pixels) * stroke.CellSize;
            float width = Mathf.Min(data.MaximumMarkerCells, data.DirectionMarkerWidthPixels * scale / pixels) * stroke.CellSize;
            Color color = MovementPathMesh.GetRouteColor(DirectionColor, stroke.Step, CompletedSteps,
                data.CompletedOpacity, data.ActiveOpacity);
            builder.AddDirectionMarker(position, direction, length, width, color, data.OutlineColor, data.DirectionShape);
        }
    }
}
