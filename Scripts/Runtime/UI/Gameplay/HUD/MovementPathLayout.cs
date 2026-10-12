using System;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 同一区間の重複と折り返しを検出し、経路を独立した表示レーンへ割り当てます。
    /// </summary>
    public static class MovementPathLayout
    {
        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly Vector2Int _first;
            private readonly Vector2Int _second;

            public EdgeKey(Vector2Int start, Vector2Int end)
            {
                bool isForward = CompareCoordinates(start, end) <= 0;
                _first = isForward ? start : end;
                _second = isForward ? end : start;
            }

            public bool Equals(EdgeKey other)
            {
                return _first == other._first && _second == other._second;
            }

            public override bool Equals(object other)
            {
                return other is EdgeKey key && Equals(key);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return _first.GetHashCode() * 397 ^ _second.GetHashCode();
                }
            }
        }

        /// <summary>
        /// 単位IDと移動順で安定したレーンを生成します。逆方向の通過も同じ区間として扱います。
        /// </summary>
        public static IReadOnlyList<MovementPathSegment> Build(IReadOnlyList<MovementPathSnapshot> paths)
        {
            var edges = new Dictionary<EdgeKey, List<MovementPathSegment>>();
            if (paths == null)
            {
                return new List<MovementPathSegment>();
            }

            foreach (MovementPathSnapshot path in paths)
            {
                if (path == null)
                {
                    continue;
                }

                for (int step = 0; step < path.Coordinates.Count - 1; step++)
                {
                    Vector2Int start = path.Coordinates[step];
                    Vector2Int end = path.Coordinates[step + 1];
                    if (start == end)
                    {
                        continue;
                    }

                    var key = new EdgeKey(start, end);
                    if (!edges.TryGetValue(key, out List<MovementPathSegment> segments))
                    {
                        segments = new List<MovementPathSegment>();
                        edges.Add(key, segments);
                    }

                    segments.Add(new MovementPathSegment(path.PlayerId, step, start, end, 0, 1));
                }
            }

            var result = new List<MovementPathSegment>();
            foreach (List<MovementPathSegment> segments in edges.Values)
            {
                segments.Sort((left, right) => left.PlayerId != right.PlayerId
                    ? left.PlayerId.CompareTo(right.PlayerId) : left.StepIndex.CompareTo(right.StepIndex));
                for (int lane = 0; lane < segments.Count; lane++)
                {
                    MovementPathSegment segment = segments[lane];
                    result.Add(new MovementPathSegment(segment.PlayerId, segment.StepIndex,
                        segment.Start, segment.End, lane, segments.Count));
                }
            }

            result.Sort((left, right) => left.PlayerId != right.PlayerId
                ? left.PlayerId.CompareTo(right.PlayerId) : left.StepIndex.CompareTo(right.StepIndex));
            return result;
        }

        /// <summary>
        /// 区間の方向に依存しないレーンの左右方向を決めます。
        /// </summary>
        public static int CompareCoordinates(Vector2Int left, Vector2Int right)
        {
            return left.x != right.x ? left.x.CompareTo(right.x) : left.y.CompareTo(right.y);
        }
    }

    public readonly struct MovementPathSegment
    {
        public int PlayerId { get; }
        public int StepIndex { get; }
        public Vector2Int Start { get; }
        public Vector2Int End { get; }
        public int LaneIndex { get; }
        public int LaneCount { get; }

        /// <summary>
        /// 進行順とレーン割り当てを保持します。
        /// </summary>
        public MovementPathSegment(int playerId, int stepIndex,
            Vector2Int start, Vector2Int end, int laneIndex, int laneCount)
        {
            PlayerId = playerId;
            StepIndex = stepIndex;
            Start = start;
            End = end;
            LaneIndex = laneIndex;
            LaneCount = laneCount;
        }
    }
}
